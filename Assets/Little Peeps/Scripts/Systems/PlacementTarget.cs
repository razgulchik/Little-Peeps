using System;
using UnityEngine;

namespace LittlePeeps
{
    // What a building tool is pointing AT: the fence on a grid edge, the structure on a cell, or
    // nothing. The Sell click and the hover highlight both ask exactly this question, and each used to
    // answer it with its own copy of the same lines — including the fence-wins rule, which is a UX
    // decision that MUST stay identical between them: if highlighting and clicking disagreed about what
    // is under the cursor, the player would see one thing tinted and sell another.
    //
    // Pure C# over IslandGrid — no MonoBehaviour, no scene — so the precedence rule is directly testable.
    public readonly struct PlacementTarget : IEquatable<PlacementTarget>
    {
        // How close to a fence the cursor has to be, as a fraction of a cell, to count as aiming at the
        // fence rather than at the cell beside it — measured to the fence's segment. Under half a cell, so
        // the middle of a fenced-in 1x1 building still answers as the building. A fraction, not an absolute
        // distance, so the grab zone keeps its feel if the cell size ever changes.
        private const float FenceReachFraction = 0.35f;

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

        // Resolve what the cursor is on. A fence within reach WINS over the cell beneath it — otherwise a
        // fence running along a structure's cell would be unreachable, because the cell underneath would
        // always be hit first.
        public static PlacementTarget Resolve(IslandGrid grid, Vector2 world)
        {
            var fence = NearestFence(grid, world, out Edge edge);
            if (fence != null) return new PlacementTarget(fence, edge);

            var occupant = grid.GetCell(grid.WorldToGrid(world))?.occupant;
            return occupant != null ? new PlacementTarget(occupant) : default;
        }

        // The nearest FENCE within FenceReachFraction of the cursor, or null. Fences are searched for
        // directly, not "the nearest side of the cell, then is there a fence on it": near a corner another,
        // empty side can be a hair nearer, and the fence right under the cursor would be missed. Every edge
        // that can come within reach of a point in a cell — its four sides and the eight that run on from
        // its corners — is anchored in the 3x3 cells around it.
        private static EdgeInstance NearestFence(IslandGrid grid, Vector2 world, out Edge edge)
        {
            edge = default;
            if (grid.Edges.Count == 0) return null;

            float reach = grid.CellSize * FenceReachFraction;
            Vector2Int cell = grid.WorldToGrid(world);
            EdgeInstance best = null;
            float bestDistance = float.MaxValue;

            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int h = 0; h < 2; h++)
                    {
                        var candidate = new Edge(new Vector2Int(cell.x + dx, cell.y + dy), h == 0);
                        var fence = grid.GetEdge(candidate);
                        if (fence == null) continue;

                        float distance = grid.DistanceToEdge(candidate, world);
                        if (distance > reach || distance >= bestDistance) continue;
                        best = fence;
                        bestDistance = distance;
                        edge = candidate;
                    }
            return best;
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
