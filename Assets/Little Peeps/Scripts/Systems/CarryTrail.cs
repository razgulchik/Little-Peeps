using UnityEngine;

namespace LittlePeeps
{
    // The way the cursor has gone over the island during one press (CarrySystem), and so where the units it
    // carries come down. Two questions, both answered by the grid alone:
    //   - has the way left the island? The sea is the absence of a cell. The whole way is walked, not only its
    //     ends, so a jerk of the mouse cannot jump a strait;
    //   - where does a unit let go now come down? In the last FREE cell the way came over: the one under the
    //     cursor, or — over a building, a forest, a rock, at the shore — the one the cursor came from.
    //
    // A free cell is land with nothing solid standing on it. What units walk through (StructureDef.passable: a
    // field, a bush, a rack) leaves it free; a construction site never does, whatever is going up — it is a
    // wall meanwhile. The cell decides, not the collider: a trunk is far smaller than the cell it stands in,
    // and the cursor is over the forest all the same.
    //
    // Pure C# over IslandGrid, so it is tested without a scene.
    public sealed class CarryTrail
    {
        // How finely the way is walked, as a fraction of a cell: a corner cut shorter than this goes unnoticed.
        private const float StepFraction = 0.25f;

        private Vector2 spot;    // the last point of the way in a free cell...
        private bool hasSpot;    // ...if it has come over one

        // The last point of the way on the island: where the cursor is, or where it left from.
        public Vector2 Head { get; private set; }

        // False once the way has left the island, and until it has come onto it.
        public bool OnIsland { get; private set; }

        // A new way, starting here. What the last one came over is forgotten.
        public void Begin(IslandGrid grid, Vector2 at)
        {
            hasSpot = false;
            Head = at;
            OnIsland = Visit(grid, at);
        }

        // Straight on to `to`. False when the line leaves the island: the way ends on its last point of land,
        // and a new one has to Begin.
        public bool Extend(IslandGrid grid, Vector2 to)
        {
            Vector2 from = Head;
            int steps = Mathf.Max(1, Mathf.CeilToInt((to - from).magnitude / (grid.CellSize * StepFraction)));
            for (int i = 1; i <= steps; i++)
            {
                OnIsland = Visit(grid, Vector2.Lerp(from, to, (float)i / steps));
                if (!OnIsland) return false;
            }
            return true;
        }

        // Where a unit let go now comes down (its body's centre): `offset` off the last point of the way in a
        // free cell, kept inside that cell and `clearance` from its sides — a wall, a fence or the shore may run
        // along any of them. The nearest free cell to that point when it has been built on since, or when the
        // way came over none at all — it began and ended over the same building.
        public Vector2 DropPoint(IslandGrid grid, Vector2 offset, float clearance)
        {
            Vector2 at = hasSpot ? spot : Head;
            Vector2Int cell = grid.WorldToGrid(at);
            if (!IsFree(grid, cell)) cell = NearestFree(grid, at, cell);
            return Inside(grid, cell, at + offset, clearance);
        }

        // Whether a unit can stand in the cell — see the class comment. The ring a bordered structure claims
        // around itself is empty ground, so it is free.
        public static bool IsFree(IslandGrid grid, Vector2Int cell)
        {
            var land = grid.GetCell(cell);
            if (land == null) return false;

            var occupant = land.occupant;
            if (occupant == null) return true;
            if (occupant.Def == null) return false;   // the safe default, as StructureExits reads it

            if (occupant.Def.passable && !occupant.UnderConstruction) return true;
            return !occupant.Def.Footprint.Contains(cell - occupant.Cell);
        }

        // One point of the way. False, and the way does not go there, off the island.
        private bool Visit(IslandGrid grid, Vector2 at)
        {
            Vector2Int cell = grid.WorldToGrid(at);
            if (grid.GetCell(cell) == null) return false;

            Head = at;
            if (IsFree(grid, cell))
            {
                spot = at;
                hasSpot = true;
            }
            return true;
        }

        // The free cell whose centre is nearest to `from`; `fallback` on an island without one. Walks the whole
        // island — a rare path, see DropPoint.
        private static Vector2Int NearestFree(IslandGrid grid, Vector2 from, Vector2Int fallback)
        {
            Vector2Int nearest = fallback;
            float best = float.PositiveInfinity;
            foreach (var coord in grid.Cells.Keys)
            {
                if (!IsFree(grid, coord)) continue;
                float distance = (grid.GridToWorld(coord) - from).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = coord;
            }
            return nearest;
        }

        // `point` moved into the cell, `clearance` or more from each of its sides. A cell too small for that
        // gives its middle.
        private static Vector2 Inside(IslandGrid grid, Vector2Int cell, Vector2 point, float clearance)
        {
            Vector2 middle = grid.GridToWorld(cell);
            float reach = Mathf.Max(0f, grid.CellSize * 0.5f - clearance);
            return new Vector2(Mathf.Clamp(point.x, middle.x - reach, middle.x + reach),
                               Mathf.Clamp(point.y, middle.y - reach, middle.y + reach));
        }
    }
}
