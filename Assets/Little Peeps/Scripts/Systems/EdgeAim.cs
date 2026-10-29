using UnityEngine;

namespace LittlePeeps
{
    // Which grid edge the fence ghost stands on, with a hold and a pull. IslandGrid.WorldToEdge alone
    // answers "the nearest side of the cell under the cursor", and in the middle of a cell all four sides
    // are nearly equally near — a one-pixel move flips the ghost between them. So the ghost keeps the edge
    // it is on until another one is nearer by a clear margin; only a deliberate lean towards a side moves
    // it.
    //
    // The pull: near the END of a wall — a cell corner where exactly one fence stops — the ghost goes to
    // that end, onto whichever of its free edges is nearest the cursor: ahead of the wall it carries the
    // wall on, beside the end it turns. Nothing else competes there, so a new wall (and a stroke, which
    // starts on the ghost's edge) joins the old one without having to find the corner by hand. A hold
    // elsewhere, or on the fence itself, gives way at once; among the end's own edges the hold still
    // applies, so the ghost does not flicker on the corner. The middle of a wall and its joints do not
    // pull — a fence laid alongside a wall must stay possible.
    //
    // Distances are to the edge's SEGMENT, not its endless line, so the held edge lets go naturally once
    // the cursor has moved on to another cell — its segment is then simply far away.
    //
    // Pure C# over IslandGrid, so the hold and the pull are tested with no scene. The press reads the same
    // answer (PlaceTool), so a click builds the edge the ghost showed.
    public sealed class EdgeAim
    {
        // How much nearer, as a fraction of a cell, another edge has to be before the ghost leaves the one
        // it holds. A fraction, so the hold keeps its feel if the cell size ever changes.
        private const float SwitchMarginFraction = 0.15f;

        // How near a wall end, as a fraction of a cell, the cursor has to come to be pulled to it. Over a
        // cell on purpose: within a cell of a corner the nearest edge mostly leaves from that corner anyway,
        // so a smaller pull changes next to nothing (0.75 was tried — it could not be felt). The pull is
        // felt when the ghost stays on the wall's end while the cursor is a cell away, in the cells around
        // it, middles included.
        private const float WallEndPullFraction = 1.25f;

        private bool holding;

        // The edge the ghost is on. Meaningful once Update has run.
        public Edge Current { get; private set; }

        // Follow the cursor and return the edge to stand on.
        public Edge Update(IslandGrid grid, Vector2 world)
        {
            bool atWallEnd = TryWallEnd(grid, world, out Vector2Int end, out Edge nearest);
            if (!atWallEnd) nearest = grid.WorldToEdge(world);

            if (!holding || nearest.Equals(Current))
            {
                Current = nearest;
                holding = true;
                return Current;
            }

            // At a wall end only its free edges compete: a hold anywhere else, or on a fence, gives way.
            if (atWallEnd && (!Touches(Current, end) || grid.GetEdge(Current) != null))
            {
                Current = nearest;
                return Current;
            }

            float margin = grid.CellSize * SwitchMarginFraction;
            if (grid.DistanceToEdge(nearest, world) + margin < grid.DistanceToEdge(Current, world)) Current = nearest;
            return Current;
        }

        // The nearest wall end within reach of the cursor, and its free edge nearest the cursor. Every node
        // in the reach's bounding box is asked — a handful at this reach.
        private static bool TryWallEnd(IslandGrid grid, Vector2 world, out Vector2Int end, out Edge edge)
        {
            end = default;
            edge = default;
            if (grid.Edges.Count == 0) return false;

            float cs = grid.CellSize;
            float reach = cs * WallEndPullFraction;
            int minX = Mathf.CeilToInt((world.x - reach) / cs), maxX = Mathf.FloorToInt((world.x + reach) / cs);
            int minY = Mathf.CeilToInt((world.y - reach) / cs), maxY = Mathf.FloorToInt((world.y + reach) / cs);
            float nearestEnd = float.MaxValue;
            bool found = false;

            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                {
                    var node = new Vector2Int(x, y);
                    float distance = (world - grid.NodeToWorld(node)).magnitude;
                    if (distance > reach || distance >= nearestEnd) continue;
                    if (!IsWallEnd(grid, node, world, out Edge free)) continue;

                    nearestEnd = distance;
                    end = node;
                    edge = free;
                    found = true;
                }
            return found;
        }

        // Exactly one fence meets at `node`, and a fence can go on at least one of the other three edges
        // there — `free` is the one of those nearest `world`.
        private static bool IsWallEnd(IslandGrid grid, Vector2Int node, Vector2 world, out Edge free)
        {
            free = default;
            int fences = 0;
            float nearest = float.MaxValue;

            for (int i = 0; i < 4; i++)
            {
                Edge edge = EdgeAt(node, i);
                if (grid.GetEdge(edge) != null)
                {
                    fences++;
                    continue;
                }
                if (!grid.CanPlaceEdge(edge)) continue;

                float distance = grid.DistanceToEdge(edge, world);
                if (distance >= nearest) continue;
                nearest = distance;
                free = edge;
            }
            return fences == 1 && nearest < float.MaxValue;
        }

        // The four edges that meet at a node: east, north, west, south.
        private static Edge EdgeAt(Vector2Int node, int i) => i switch
        {
            0 => new Edge(node, true),
            1 => new Edge(node, false),
            2 => new Edge(node + Vector2Int.left, true),
            _ => new Edge(node + Vector2Int.down, false),
        };

        private static bool Touches(Edge edge, Vector2Int node) => edge.anchor == node || edge.End == node;
    }
}
