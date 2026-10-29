using UnityEngine;

namespace LittlePeeps
{
    // Which grid edge the fence ghost stands on, with a hold and a pull. IslandGrid.WorldToEdge alone
    // answers "the nearest side of the cell under the cursor", and in the middle of a cell all four sides
    // are nearly equally near — a one-pixel move flips the ghost between them. So the ghost moves only
    // when the cursor comes up to another LINE (within the capture distance of it) having left its own;
    // the middle of a cell is a dead zone where it stays put, however the cursor wanders — as a stroke's
    // head waits between nodes (EdgeStroke). Inside its own capture zone it stays too, so on a corner, where
    // two lines' zones overlap, it does not flicker. Cursor gone far from the held edge (a jump, or a move
    // too fast to touch a line on the way) — the hold is pointless there, and the ghost takes the nearest.
    //
    // The pull: near the END of a wall — a cell corner where exactly one fence stops — the ghost goes to
    // that end, and by default carries the wall on straight: a cursor led along the wall, a little above or
    // below its line, keeps being offered the next straight piece — the turn's edge starts right at the
    // corner and would otherwise win the moment the cursor crosses into the next cell. It turns only with
    // the cursor clearly off the wall's line (TurnFraction), to that side. Nothing else competes there, so
    // a new wall (and a stroke, which
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
        // How near another edge's line, as a fraction of a cell, the cursor has to come for the ghost to go
        // there. Fractions, so the hold keeps its feel if the cell size ever changes.
        private const float CaptureFraction = 0.15f;

        // How far from the held edge, as a fraction of a cell, the cursor has to go for the hold to give
        // way to the nearest edge wherever it is. Past 1 - CaptureFraction on purpose: across its own cell
        // the ghost must hold all the way to the far side's capture zone, or the release would take it
        // there first and shrink the dead zone.
        private const float ReleaseFraction = 0.85f;

        // How near a wall end, as a fraction of a cell, the cursor has to come to be pulled to it. Over a
        // cell on purpose: within a cell of a corner the nearest edge mostly leaves from that corner anyway,
        // so a smaller pull changes next to nothing (0.75 was tried — it could not be felt). The pull is
        // felt when the ghost stays on the wall's end while the cursor is a cell away, in the cells around
        // it, middles included.
        private const float WallEndPullFraction = 1.25f;

        // How far off a wall's line, as a fraction of a cell, the cursor has to go for its end to offer the
        // turn instead of the wall carried on. With the hold on top, the ghost actually turns once the
        // cursor is past this AND at the turn's line — led up the turn, not just drifting above the wall.
        private const float TurnFraction = 0.5f;

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

            float capture = grid.CellSize * CaptureFraction;
            float held = grid.DistanceToEdge(Current, world);
            bool atAnotherLine = held >= capture && grid.DistanceToEdge(nearest, world) < capture;
            if (atAnotherLine || held > grid.CellSize * ReleaseFraction) Current = nearest;
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
        // there. `free` is the one the end offers (Offered); if a fence can't go there — the coast, a
        // building — the free edge nearest `world` instead.
        private static bool IsWallEnd(IslandGrid grid, Vector2Int node, Vector2 world, out Edge free)
        {
            free = default;
            int fences = 0;
            int wall = -1;
            float nearest = float.MaxValue;

            for (int i = 0; i < 4; i++)
            {
                Edge edge = EdgeAt(node, i);
                if (grid.GetEdge(edge) != null)
                {
                    fences++;
                    wall = i;
                    continue;
                }
                if (!grid.CanPlaceEdge(edge)) continue;

                float distance = grid.DistanceToEdge(edge, world);
                if (distance >= nearest) continue;
                nearest = distance;
                free = edge;
            }
            if (fences != 1 || nearest == float.MaxValue) return false;

            Edge offered = EdgeAt(node, Offered(grid, node, world, wall));
            if (grid.CanPlaceEdge(offered)) free = offered;
            return true;
        }

        // Which of the four edges at a wall's end (`wall` is the fence's) the end offers: straight on,
        // opposite the wall — or, with the cursor off the wall's line by more than TurnFraction, the turn
        // on the cursor's side.
        private static int Offered(IslandGrid grid, Vector2Int node, Vector2 world, int wall)
        {
            Vector2 off = world - grid.NodeToWorld(node);
            bool alongX = wall % 2 == 0;   // east or west: a horizontal wall
            float across = alongX ? off.y : off.x;

            if (Mathf.Abs(across) <= grid.CellSize * TurnFraction) return (wall + 2) % 4;
            if (alongX) return across > 0f ? 1 : 3;
            return across > 0f ? 0 : 2;
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
