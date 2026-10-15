using System;
using UnityEngine;

namespace LittlePeeps
{
    // What a build-mode tool is pointing AT: the fence on a grid edge, the structure on a cell, or
    // nothing. The Sell click and the hover highlight both ask exactly this question, and each used to
    // answer it with its own copy of the same lines — including the fence-wins rule, which is a UX
    // decision that MUST stay identical between them: if highlighting and clicking disagreed about what
    // is under the cursor, the player would see one thing tinted and sell another.
    //
    // Pure C# over IslandGrid — no MonoBehaviour, no scene — so the precedence rule is directly testable.
    public readonly struct PlacementTarget : IEquatable<PlacementTarget>
    {
        // How close to an edge's line the cursor has to be, as a fraction of a cell, to count as aiming at
        // the fence rather than at the cell beside it. A fraction, not an absolute distance, so the grab
        // zone keeps its feel if the cell size ever changes.
        private const float EdgeGrabFraction = 0.3f;

        public readonly EdgeInstance Fence;         // non-null → the cursor is on this fence
        public readonly Edge Edge;                  // the edge Fence sits on; meaningless when Fence is null
        public readonly StructureInstance Instance; // non-null → the cursor is on this structure's territory

        // The scene object to tint, whichever kind this target is — the one standing there WHEN the target
        // was resolved. Held rather than read through the instance, because a construction site is swapped
        // for its building under the same instance, and comparing two targets must see that swap.
        // Null when there is no target.
        public readonly Structure RuntimeObject;

        private PlacementTarget(EdgeInstance fence, Edge edge)
        {
            Fence = fence;
            Edge = edge;
            Instance = null;
            RuntimeObject = fence.RuntimeObject;
        }

        private PlacementTarget(StructureInstance instance)
        {
            Fence = null;
            Edge = default;
            Instance = instance;
            RuntimeObject = instance.RuntimeObject;
        }

        public bool IsNone => Fence == null && Instance == null;
        public bool IsFence => Fence != null;

        public StructureDef Def => Fence != null ? Fence.Def : Instance?.Def;

        // Whether the Sell tool may act on the target — the def decides (generated mountains and rivers
        // say no). Asked by the hover and the click alike, so a thing never lights up as sellable and
        // then refuses.
        public bool CanSell => Def != null && Def.canSell;

        // Resolve what the cursor is on. A fence WINS over the cell beneath it when the cursor is right on
        // the edge line — otherwise a fence running along a structure's cell would be unreachable, because
        // the cell underneath would always be hit first.
        public static PlacementTarget Resolve(IslandGrid grid, Vector2 world)
        {
            Edge edge = grid.WorldToEdge(world);
            var fence = grid.GetEdge(edge);
            if (fence != null && IsAimedAtEdge(grid, world, edge)) return new PlacementTarget(fence, edge);

            var occupant = grid.GetCell(grid.WorldToGrid(world))?.occupant;
            return occupant != null ? new PlacementTarget(occupant) : default;
        }

        // True when the cursor is within EdgeGrabFraction of a cell of the edge's line, measured
        // perpendicular to it.
        private static bool IsAimedAtEdge(IslandGrid grid, Vector2 world, Edge edge)
        {
            Vector2 mid = grid.EdgeToWorld(edge);
            float perp = edge.horizontal ? Mathf.Abs(world.y - mid.y) : Mathf.Abs(world.x - mid.x);
            return perp <= grid.CellSize * EdgeGrabFraction;
        }

        // Identity is the thing targeted, so a tool can ask "still the same target?" with one compare and
        // skip the per-frame work. Edge is derived from Fence, so it is not part of it.
        // RuntimeObject is compared too: a construction site is swapped for its finished building under the
        // SAME instance, and a hover still on the site has to let go of it and tint the building instead.
        // ReferenceEquals, not Unity's ==: a swap is a different object, destroyed or not.
        public bool Equals(PlacementTarget other) => Fence == other.Fence && Instance == other.Instance
                                                     && ReferenceEquals(RuntimeObject, other.RuntimeObject);
        public override bool Equals(object obj) => obj is PlacementTarget other && Equals(other);

        public override int GetHashCode()
        {
            int fence = Fence != null ? Fence.GetHashCode() : 0;
            int instance = Instance != null ? Instance.GetHashCode() : 0;
            return (fence * 397) ^ instance;
        }
    }
}
