using System;
using UnityEngine;

namespace LittlePeeps
{
    // The side of a structure that runs along water, for a building that must stand by the river — a
    // watermill (StructureDef.needsWaterSide). None = no side does, and such a building can't go there.
    public enum WaterSide { None, South, West, East }

    // Which side of a structure runs along water. A side counts only WHOLE: every cell just outside it must
    // be water, so the wheel hangs over the river whichever cell of that side the art puts it on. A river
    // that turns away at the building's corner leaves half a side, and the spot is refused — one step
    // along, it fits.
    //
    // Where the river bends around a corner two sides qualify, and the first of South, West, East wins —
    // the art's order: from the south the wheel faces the camera. North never counts, the wheel would hang
    // behind the building out of sight. The order and the three sides are the watermill's art, not
    // geometry, which is why they live here and not in a def.
    //
    // A side follows the footprint's SHAPE, not its box: the south side is the cell below the lowest
    // footprint cell of every column, the west side the cell left of the leftmost footprint cell of every
    // row, the east side the one right of the rightmost — the outline facing that way. For a rectangle
    // that is simply the row or column along the edge.
    //
    // What counts as water is a predicate, so the one rule serves the placed island (the grid overload: a
    // cell holding an isWater structure) and a generator's own cell sets alike.
    public static class WaterSides
    {
        // The side along water on the placed island. The closure allocates per call — fine on the
        // placement paths (a click, one ghost per frame in build mode), not for anything per hit.
        public static WaterSide Find(IslandGrid grid, Vector2Int origin, Footprint footprint)
            => Find(origin, footprint, cell => IsWater(grid, cell));

        public static WaterSide Find(Vector2Int origin, Footprint footprint, Func<Vector2Int, bool> isWater)
        {
            if (SouthAlongWater(origin, footprint, isWater)) return WaterSide.South;
            if (WestAlongWater(origin, footprint, isWater)) return WaterSide.West;
            if (EastAlongWater(origin, footprint, isWater)) return WaterSide.East;
            return WaterSide.None;
        }

        // A cell of the placed island is water when the structure standing on it is (the river). The sea
        // is no cell at all, so it never counts — a watermill wants a river.
        public static bool IsWater(IslandGrid grid, Vector2Int cell)
        {
            var occupant = grid.GetCell(cell)?.occupant;
            return occupant != null && occupant.Def != null && occupant.Def.isWater;
        }

        // Below the lowest footprint cell of every column.
        private static bool SouthAlongWater(Vector2Int origin, Footprint footprint, Func<Vector2Int, bool> isWater)
        {
            for (int x = 0; x < footprint.Size.x; x++)
                for (int y = 0; y < footprint.Size.y; y++)
                    if (footprint.Contains(x, y))
                    {
                        if (!isWater(new Vector2Int(origin.x + x, origin.y + y - 1))) return false;
                        break;
                    }
            return true;
        }

        // Left of the leftmost footprint cell of every row.
        private static bool WestAlongWater(Vector2Int origin, Footprint footprint, Func<Vector2Int, bool> isWater)
        {
            for (int y = 0; y < footprint.Size.y; y++)
                for (int x = 0; x < footprint.Size.x; x++)
                    if (footprint.Contains(x, y))
                    {
                        if (!isWater(new Vector2Int(origin.x + x - 1, origin.y + y))) return false;
                        break;
                    }
            return true;
        }

        // Right of the rightmost footprint cell of every row.
        private static bool EastAlongWater(Vector2Int origin, Footprint footprint, Func<Vector2Int, bool> isWater)
        {
            for (int y = 0; y < footprint.Size.y; y++)
                for (int x = footprint.Size.x - 1; x >= 0; x--)
                    if (footprint.Contains(x, y))
                    {
                        if (!isWater(new Vector2Int(origin.x + x + 1, origin.y + y))) return false;
                        break;
                    }
            return true;
        }
    }
}
