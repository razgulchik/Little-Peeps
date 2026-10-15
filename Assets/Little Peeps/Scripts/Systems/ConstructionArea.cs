using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The ground a construction site covers, as world rectangles, and the one question asked of it: does a
    // unit's body circle touch it. Pure geometry over the grid — no physics query — so who counts as
    // inside a site is decided one way everywhere and can be tested without a scene.
    public static class ConstructionArea
    {
        // One rect per footprint cell: the mask's cells only, so a staircase site is a staircase. A cell is
        // the square north-east of its lattice point (see Edge), cellSize on a side.
        public static void CellRects(IslandGrid grid, Vector2Int origin, Footprint footprint, List<Rect> rects)
        {
            rects.Clear();
            float cs = grid.CellSize;
            Vector2Int s = footprint.Size;
            for (int x = 0; x < s.x; x++)
                for (int y = 0; y < s.y; y++)
                    if (footprint.Contains(x, y))
                        rects.Add(new Rect((origin.x + x) * cs, (origin.y + y) * cs, cs, cs));
        }

        // A fence's site: a strip along the edge's line, `thickness` world units across, centred on it.
        public static Rect EdgeRect(IslandGrid grid, Edge edge, float thickness)
        {
            float cs = grid.CellSize;
            Vector2 mid = grid.EdgeToWorld(edge);
            Vector2 size = edge.horizontal ? new Vector2(cs, thickness) : new Vector2(thickness, cs);
            return new Rect(mid - size * 0.5f, size);
        }

        // Whether a circle touches any of the rects — touching counts, so a body straddling the line is
        // inside.
        public static bool Overlaps(IReadOnlyList<Rect> rects, Vector2 center, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                float dx = center.x - Mathf.Clamp(center.x, r.xMin, r.xMax);
                float dy = center.y - Mathf.Clamp(center.y, r.yMin, r.yMax);
                if (dx * dx + dy * dy <= r2) return true;
            }
            return false;
        }
    }
}
