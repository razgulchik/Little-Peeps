using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Where a unit may leave a structure standing on the grid, and where it is put when it does. A house
    // launches through any open side (CollectAllowedDirections); a building with one fixed door asks about
    // that one cell (IsOpenExit). Both put the unit down the same way (ExitPoint).
    //
    // An EXIT is a cell one step outside the structure's claimed territory (footprint, plus its border if
    // it has one) that
    //   - shares at least one UNFENCED edge with the territory — a cell touching it only at a corner is no
    //     exit, and one touching it along several edges is one exit, not several;
    //   - is land — off-island is closed;
    //   - holds no solid occupant (another structure or its border). A passable one (field, bush, rack —
    //     see StructureDef.passable) leaves it open: the unit lands in its trigger and walks on through.
    // With border 0 (the common case) the exits are the footprint's immediate neighbours, so a corner house
    // simply loses its off-island sides. With border >= 1, requiring the cell BEYOND the border ring to be
    // open gives the "one clear cell from the map edge / a neighbour" rule for free; the unit still lands
    // in the border ring. A shaped footprint's ring hugs the shape, so a notch's cells are exits too — the
    // unit starts at the notch's wall and flies out through it.
    //
    // Static and parameterised (grid, origin, footprint, border) rather than reading any component, so the
    // geometry can be exercised on a bare IslandGrid with no scene, GameObject or spawner behind it.
    public static class StructureExits
    {
        // Fill `buffer` with one direction — from the box centre toward the cell — per exit of the
        // structure. Empty = sealed in on every side; the caller keeps its unit inside and tries later.
        public static void CollectAllowedDirections(IslandGrid grid, Vector2Int origin, Footprint footprint, int border, List<Vector2> buffer)
        {
            buffer.Clear();

            Vector2 center = grid.OriginToWorldCenter(origin, footprint.Size);
            Vector2Int s = footprint.Size;

            // The box grown by border + 1 holds every cell one step outside the claimed territory.
            for (int x = -border - 1; x <= s.x + border; x++)
                for (int y = -border - 1; y <= s.y + border; y++)
                {
                    var outer = new Vector2Int(origin.x + x, origin.y + y);
                    if (!IsOpenExit(grid, origin, footprint, border, outer)) continue;

                    Vector2 dir = grid.GridToWorld(outer) - center;
                    if (dir.sqrMagnitude < 1e-6f) continue;
                    buffer.Add(dir.normalized);
                }
        }

        // Pick one exit at random (CollectAllowedDirections into `buffer`) and turn it by up to
        // ±jitterDegrees, so units leaving one building don't all run on the same few lines. False when
        // the structure is sealed in — the caller keeps its unit inside and tries again later. The way a
        // house launches and a tavern lets its guests out.
        public static bool TryPickDirection(IslandGrid grid, Vector2Int origin, Footprint footprint, int border,
                                            List<Vector2> buffer, float jitterDegrees, out Vector2 dir)
        {
            CollectAllowedDirections(grid, origin, footprint, border, buffer);
            if (buffer.Count == 0) { dir = Vector2.zero; return false; }

            dir = buffer[Random.Range(0, buffer.Count)];
            if (jitterDegrees > 0f) dir = Rotate(dir, Random.Range(-jitterDegrees, jitterDegrees));
            return true;
        }

        // Is `cell` (grid coordinates) an exit of the structure at `origin` — see the header for what that
        // takes. Any cell may be asked: one inside the territory, or not touching it, is simply no exit.
        public static bool IsOpenExit(IslandGrid grid, Vector2Int origin, Footprint footprint, int border, Vector2Int cell)
        {
            int x = cell.x - origin.x, y = cell.y - origin.y;   // local to the footprint's box
            if (footprint.Claims(x, y, border)) return false;
            if (!HasOpenEdgeToTerritory(grid, cell, footprint, border, x, y)) return false;

            var gridCell = grid.GetCell(cell);
            return gridCell != null && !IsSolid(gridCell.occupant);
        }

        // Where a unit leaving along `dir` (unit length) is put: where the ray from the box centre leaves
        // the FOOTPRINT (ExitDistance), plus `clearance` — the unit's radius and a gap, so it starts clear
        // of the walls. The footprint and not a collider box, because that is right for any shape: for an L
        // or a U the box edge can lie in a notch, inside the bounds yet outside the walls.
        public static Vector2 ExitPoint(IslandGrid grid, Vector2Int origin, Footprint footprint, Vector2 dir, float clearance)
        {
            Vector2 center = grid.OriginToWorldCenter(origin, footprint.Size);
            return center + dir * (ExitDistance(grid, origin, footprint, center, dir) + clearance);
        }

        // Distance along `dir` (unit length) from `center` to the point where the ray leaves the LAST
        // footprint cell it crosses — the launch ray's exit from the building. For a rectangle that is the
        // box edge along dir; for a shape the ray may leave a cell, cross a notch and enter another, and
        // the exit is where it finally clears the walls. Zero when the ray crosses no footprint cell (a
        // centre that falls in a notch, aiming away from the walls).
        public static float ExitDistance(IslandGrid grid, Vector2Int origin, Footprint footprint, Vector2 center, Vector2 dir)
        {
            float cs = grid.CellSize;
            float exit = 0f;
            for (int x = 0; x < footprint.Size.x; x++)
                for (int y = 0; y < footprint.Size.y; y++)
                {
                    if (!footprint.Contains(x, y)) continue;
                    float x0 = (origin.x + x) * cs, y0 = (origin.y + y) * cs;
                    if (RayLeavesBox(center, dir, x0, y0, x0 + cs, y0 + cs, out float t) && t > exit) exit = t;
                }
            return exit;
        }

        // Does `outer` (local (x, y) of the box) share at least one UNFENCED edge with a claimed cell?
        // Each side reads the edge between the two cells in its canonical form (see Edge): the bottom
        // edge belongs to the cell above it, the left edge to the cell right of it.
        private static bool HasOpenEdgeToTerritory(IslandGrid grid, Vector2Int outer, Footprint footprint, int border, int x, int y)
        {
            if (footprint.Claims(x, y - 1, border) && grid.GetEdge(new Edge(outer, true)) == null) return true;                                   // south neighbour: our bottom edge
            if (footprint.Claims(x, y + 1, border) && grid.GetEdge(new Edge(new Vector2Int(outer.x, outer.y + 1), true)) == null) return true;    // north neighbour: its bottom edge
            if (footprint.Claims(x - 1, y, border) && grid.GetEdge(new Edge(outer, false)) == null) return true;                                  // west neighbour: our left edge
            if (footprint.Claims(x + 1, y, border) && grid.GetEdge(new Edge(new Vector2Int(outer.x + 1, outer.y), false)) == null) return true;   // east neighbour: its left edge
            return false;
        }

        // An occupant closes a cell unless its def says units pass through it. No def (an occupant
        // registered without one, as the grid tests do) is read as solid — the safe default.
        private static bool IsSolid(StructureInstance occupant)
            => occupant != null && (occupant.Def == null || !occupant.Def.passable);

        // Slab test: does the ray center + dir * t (t >= 0) pass through the box [x0,x1]x[y0,y1], and at
        // what t does it leave? A ray starting inside leaves at its first wall.
        private static bool RayLeavesBox(Vector2 center, Vector2 dir, float x0, float y0, float x1, float y1, out float tExit)
        {
            float tEnter = 0f;
            tExit = float.PositiveInfinity;
            return Slab(center.x, dir.x, x0, x1, ref tEnter, ref tExit)
                && Slab(center.y, dir.y, y0, y1, ref tEnter, ref tExit);
        }

        private static bool Slab(float c, float d, float lo, float hi, ref float tEnter, ref float tExit)
        {
            if (Mathf.Abs(d) < 1e-6f) return c >= lo && c <= hi;   // parallel to this axis: inside its slab or never
            float t0 = (lo - c) / d, t1 = (hi - c) / d;
            if (t0 > t1) (t0, t1) = (t1, t0);
            if (t0 > tEnter) tEnter = t0;
            if (t1 < tExit) tExit = t1;
            return tExit >= tEnter;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}
