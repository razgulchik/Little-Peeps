using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // How a structure snaps to the grid:
    //  - Cell: occupies a footprint of cells (houses, trees, fields) — the default.
    //  - Edge: sits on the boundary line between two cells (fences). size/border are unused for Edge.
    public enum PlacementKind { Cell, Edge }

    // Why the build palette refuses a structure right now — see StructureDef.LockState. The reason, not
    // just a bool, because the locked card says what would open it.
    public enum StructureLock { None, Age, Perk }

    [CreateAssetMenu(menuName = "LittlePeeps/StructureDef")]
    public class StructureDef : ScriptableObject
    {
        public string id;
        public string displayName;
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

        public List<ResourceCost> cost;

        [Tooltip("Run age at which this structure becomes available in the build palette. " +
                 "Zero keeps it available from the beginning.")]
        [Min(0)] public int requiredAge;

        // The second gate next to requiredAge: a building that stays out of reach in a run where no perk
        // opened it, so runs differ in what they can build. Only the build palette reads it — a
        // generated structure carrying this def still appears on the island.
        [Tooltip("Shut in the build palette until an Unlock Structure Perk opens it, whatever the age. " +
                 "Required Age still applies on top once the perk is taken.")]
        public bool lockedUntilPerk;

        // The one rule the build palette locks a card by. The perk gate is checked FIRST: a perk-locked
        // building stays shut when its age comes, so "Open on the Age N" would be a promise the game does
        // not keep. A null run (no run started yet) has opened nothing.
        public StructureLock LockState(int currentAge, RunContext run)
        {
            if (lockedUntilPerk && (run == null || !run.unlockedStructures.Contains(this)))
                return StructureLock.Perk;
            return currentAge < requiredAge ? StructureLock.Age : StructureLock.None;
        }

        // Biomes this structure may be placed on. Empty/null = any terrain.
        public TerrainType[] allowedTerrain;

        // Fraction of build cost refunded when the structure is sold (0..1).
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
    }
}
