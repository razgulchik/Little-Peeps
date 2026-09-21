using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // How a structure snaps to the grid:
    //  - Cell: occupies a footprint of cells (houses, trees, fields) — the default.
    //  - Edge: sits on the boundary line between two cells (fences). size/border are unused for Edge.
    public enum PlacementKind { Cell, Edge }

    [CreateAssetMenu(menuName = "LittlePeeps/StructureDef")]
    public class StructureDef : ScriptableObject
    {
        public string id;
        public string displayName;
        public Sprite icon;
        public GameObject prefab;
        public PlacementKind placement = PlacementKind.Cell;
        public Vector2Int size = Vector2Int.one;
        public List<ResourceCost> cost;

        [Tooltip("Run age at which this structure becomes available in the build palette. " +
                 "Zero keeps it available from the beginning.")]
        [Min(0)] public int requiredAge;

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
        // the collider, so the side stays shut (Spawner.CollectAllowedDirections). Physics only —
        // what animals do about it is `animalsAvoid`.
        public bool passable = false;

        // Wandering animals stop and turn away instead of walking across this structure's cells.
        // Tick for solid buildings (stable, smithy, market...); leave off for trees/fields so
        // forest animals keep roaming through them. AI only — independent of `passable`: a tree is
        // solid for units yet forest animals roam among trees.
        [UnityEngine.Serialization.FormerlySerializedAs("impassable")]
        public bool animalsAvoid = false;

        // Required clear margin (in cells) around the footprint: those cells must be on-island and free
        // of OTHER bordered structures. Keeps spawner-buildings off the map edge and apart from each
        // other (e.g. house = 1); passive structures like trees/fields use 0 (no margin, may sit anywhere).
        public int border = 0;
    }
}
