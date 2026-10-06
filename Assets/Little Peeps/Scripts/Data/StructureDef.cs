using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // How a structure snaps to the grid:
    //  - Cell: occupies a footprint of cells (houses, trees, fields) — the default.
    //  - Edge: sits on the boundary line between two cells (fences). size/border are unused for Edge.
    public enum PlacementKind { Cell, Edge }

    // Why the build palette refuses a structure right now — see StructureDef.LockState. The reason, not
    // just a bool, because the locked card says what would open it. Limit = as many stand as the run
    // allows; selling one (or a StructureLimit modifier) opens it again.
    public enum StructureLock { None, Age, Perk, Limit }

    [CreateAssetMenu(menuName = "LittlePeeps/StructureDef")]
    public class StructureDef : ScriptableObject
    {
        public string id;
        public string displayName;
        [Tooltip("Shown under the name in the build card's hint. Empty = no description.")]
        [TextArea(2, 5)] public string description;
        public Sprite icon;
        public GameObject prefab;
        public PlacementKind placement = PlacementKind.Cell;

        // The footprint's bounding box (cells). Rectangular on its own; the mask below carves a shape
        // out of it. The box, not the shape, is what the root is anchored on and the cursor maps by.
        public Vector2Int size = Vector2Int.one;

        // Which cells of the box the structure stands on, painted in the inspector (y=0 is the BOTTOM
        // row, as on the grid). Unpainted = every cell — the plain rectangle every def had before shapes.
        // Read through `Footprint`, never directly.
        [FootprintMask(nameof(size))] public FootprintMask footprintMask;

        public Footprint Footprint => new Footprint(size, footprintMask.cells);

        // The FIRST one's price. The next ones grow from it (Count & price growth below), so read a price
        // through CostAt, never off this list.
        public List<ResourceCost> cost;

        // The age NUMBER the player sees (Age I = the start), the one the lock text prints — not
        // RunContext.currentAge, which counts transitions from 0. LockState does the conversion.
        [Tooltip("Age at which this structure becomes available in the build palette, numbered as the " +
                 "player sees it: 2 = opens on the Age II. Zero or one keeps it available from the beginning.")]
        [Min(0)] public int requiredAge;

        // The second gate next to requiredAge: a building that stays out of reach in a run where no perk
        // opened it, so runs differ in what they can build. Only the build palette reads it — a
        // generated structure carrying this def still appears on the island.
        [Tooltip("Shut in the build palette until an Unlock Structure Perk opens it, whatever the age. " +
                 "Required Age still applies on top once the perk is taken.")]
        public bool lockedUntilPerk;

        // The one rule the build palette locks a card by. The perk gate is checked FIRST: a perk-locked
        // building stays shut when its age comes, so "Open on the Age N" would be a promise the game does
        // not keep. A null run (no run started yet) has opened nothing. The limit comes last: it is the
        // only one of the three that changes while a card is in hand, so placement checks it again on
        // its own (AtLimit) — the age and perk gates are palette-only.
        public StructureLock LockState(int currentAge, RunContext run)
        {
            if (lockedUntilPerk && (run == null || !run.unlockedStructures.Contains(this)))
                return StructureLock.Perk;
            if (currentAge + 1 < requiredAge) return StructureLock.Age;   // currentAge 0 is the Age I
            return AtLimit(run) ? StructureLock.Limit : StructureLock.None;
        }

        // Biomes this structure may be placed on. Empty/null = any terrain.
        public TerrainType[] allowedTerrain;

        // Fraction of build cost refunded when the structure is sold (0..1) — of the price the sold one would
        // cost to build again, so a build → sell cycle always loses (StructureSystem.RefundCost).
        [Range(0f, 1f)] public float sellRefundPercent = 0.5f;

        // Whether build mode may sell / pick up this structure. Off for generated terrain-like content
        // (mountains, river) that is part of the island rather than something the player owns; on for
        // everything else, generated trees and the starting house included — there is no "player-placed"
        // distinction, only what a def allows.
        public bool canSell = true;
        public bool canMove = true;

        // Units pass through this structure: its collider is a trigger (fields, bushes, tool racks —
        // CollisionTarget's "interactable path"), so it is not an obstacle and never closes a launch
        // direction of a spawner next to it. Off = solid: a unit launched that way would land inside
        // the collider, so the side stays shut (StructureExits.IsOpenExit). Physics only —
        // what animals do about it is `animalsAvoid`.
        public bool passable = false;

        // Wandering animals stop and turn away instead of walking across this structure's cells.
        // Tick for solid buildings (stable, smithy, market...); leave off for trees/fields so
        // forest animals keep roaming through them. AI only — independent of `passable`: a tree is
        // solid for units yet forest animals roam among trees.
        [UnityEngine.Serialization.FormerlySerializedAs("impassable")]
        public bool animalsAvoid = false;

        // The river: its cells count as water for a structure that needs a side along water. Terrain
        // can't say it — a river cell is land with this structure standing on it.
        [Tooltip("This structure is water (the river): a building that needs a side along water counts its cells.")]
        public bool isWater = false;

        // Must stand with one whole side along water — a watermill, its wheel over the river. Which sides
        // count and which one wins is WaterSides; which art shows is the prefab's WaterSideVisual.
        [Tooltip("Placeable only with one whole side (south, west or east) along water. Needs Border 0: " +
                 "the river's cells are taken, so a border ring could never reach them.")]
        public bool needsWaterSide = false;

        // Required clear margin (in cells) around the footprint: those cells must be on-island and free
        // of OTHER bordered structures. Keeps spawner-buildings off the map edge and apart from each
        // other (e.g. house = 1); passive structures like trees/fields use 0 (no margin, may sit anywhere).
        public int border = 0;

        // --- How many may stand, and how the price grows with that count ---------------------------------
        // Both count EVERY standing copy of this def: generated ones included (the starting house), and the
        // one MoveTool is carrying too — a move is not a sale. No player-placed distinction, as everywhere
        // else: only what stands. The count itself lives on the run (RunContext.CountOf).

        [Header("Count & price growth")]
        [Tooltip("How many of this structure may stand at once. 0 = no limit. Counts every standing one, " +
                 "generated ones included (the starting house). A Build Limit modifier raises it, but never " +
                 "limits a structure left at 0.")]
        [Min(0)] public int maxCount;

        [Tooltip("Added to every cost entry for each one already standing: the next costs base + n x flat. " +
                 "Linear in both growth modes.")]
        public float costStepFlat;

        [Tooltip("Price growth per one already standing (0.15 = +15%). Linear: +15% of the BASE each time. " +
                 "Exponential: +15% of the PREVIOUS price each time (x1.15 per building). " +
                 "Does nothing on a base cost of 0 - use Flat there.")]
        public float costStepPercent;

        [Tooltip("Off = linear: (base + n x flat) x (1 + n x percent). " +
                 "On = exponential: (base + n x flat) x (1 + percent)^n. The price preview below shows both.")]
        public bool exponentialCostGrowth;

        // The price of cost entry `entry` for the next one, with `standing` of this def already standing.
        // Every reader of a build price comes through here — placement, the sell refund, the card — so
        // nothing can read `cost` raw and quietly charge the base price for the tenth house.
        public float CostAt(int entry, int standing) =>
            StructurePrice.At(cost[entry].amount, standing, costStepFlat, costStepPercent, exponentialCostGrowth);

        public bool HasLimit => maxCount > 0;

        // How many may stand at once in this run: maxCount through StructureLimit modifiers, rounded DOWN
        // like every count stat and never below zero. Meaningful only with HasLimit — a modifier never
        // turns an unlimited structure into a limited one. A null run (no run yet) reads the bare maxCount.
        public int LimitIn(RunContext run)
        {
            if (!HasLimit) return 0;
            int limit = run != null
                ? run.stats.ApplyCount(maxCount, StatId.StructureLimit, structure: this)
                : maxCount;
            return Mathf.Max(0, limit);
        }

        // As many stand as this run allows: the card locks and placement refuses. Never without a run.
        public bool AtLimit(RunContext run) => HasLimit && run != null && run.CountOf(this) >= LimitIn(run);

        // The name the player reads: displayName, falling back to the asset's when it is blank.
        public string ShownName => !string.IsNullOrEmpty(displayName) ? displayName : name;
    }
}
