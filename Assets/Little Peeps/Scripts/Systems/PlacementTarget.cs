using System;
using UnityEngine;

namespace LittlePeeps
{
    // What a build-mode tool is pointing AT: the fence on a grid edge, the structure on a cell, or
    // nothing. Sell, Move pick-up and the hover highlight all ask exactly this question, and each used to
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

        private PlacementTarget(EdgeInstance fence, Edge edge)
        {
            Fence = fence;
            Edge = edge;
            Instance = null;
        }

        private PlacementTarget(StructureInstance instance)
        {
            Fence = null;
            Edge = default;
            Instance = instance;
        }

        public bool IsNone => Fence == null && Instance == null;
        public bool IsFence => Fence != null;

        // The scene object to tint or grab, whichever kind this target is. Null when there is no target.
        public Structure RuntimeObject => Fence != null ? Fence.RuntimeObject : Instance?.RuntimeObject;

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
        public bool Equals(PlacementTarget other) => Fence == other.Fence && Instance == other.Instance;
        public override bool Equals(object obj) => obj is PlacementTarget other && Equals(other);

        public override int GetHashCode()
        {
            int fence = Fence != null ? Fence.GetHashCode() : 0;
            int instance = Instance != null ? Instance.GetHashCode() : 0;
            return (fence * 397) ^ instance;
        }
    }
}
