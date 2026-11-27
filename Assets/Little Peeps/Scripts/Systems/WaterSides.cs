using System;
using UnityEngine;

namespace LittlePeeps
{
    // Where a watermill's wheel turns: in a river cell right across one edge of the building. Each such
    // spot has its own art — on the 2x1 mill two along the south, two along the north, one at each end.
    // None = the river touches no edge, and a building that needs it can't go there
    // (StructureDef.needsWaterSide).
    public enum WaterSide { None, SouthRight, SouthLeft, West, East, NorthRight, NorthLeft }

    // Which spot of a structure the river touches. One edge is enough: the wheel needs one river cell to
    // turn in, whatever lies along the rest of the building.
    //
    // The edges are the footprint's OUTLINE, not its box: below the lowest footprint cell of every column,
    // above the highest, left of the leftmost cell of every row, right of the rightmost. For a rectangle
    // that is simply the rows and columns along its edges. Along the south and the north, a column of the
    // box's right half counts for the Right spot, of the left half for the Left one; a column right on
    // the middle line (an odd width) for both. Along the west and east every row counts for the one spot.
    //
    // Where the river touches several spots, Order picks one. The spots and their order are the
    // watermill's art, not geometry, which is why they live here and not in a def.
    //
    // What counts as water is a predicate, so the one rule serves the placed island (the grid overload: a
    // cell holding an isWater structure) and a generator's own cell sets alike.
    public static class WaterSides
    {
        // The art drawn so far comes first, its mirrored stand-ins (WaterSideVisual) after it. Within
        // each, by how much of the wheel shows: the south faces the camera, an end shows it side-on, the
        // north hides it behind the roof.
        private static readonly WaterSide[] Order =
        {
            WaterSide.SouthRight, WaterSide.West, WaterSide.NorthRight,
            WaterSide.SouthLeft, WaterSide.East, WaterSide.NorthLeft,
        };

        // The spot along water on the placed island. The closure allocates per call — fine on the
        // placement paths (a click, one ghost per frame while a card is in hand), not for anything per hit.
        public static WaterSide Find(IslandGrid grid, Vector2Int origin, Footprint footprint)
            => Find(origin, footprint, cell => IsWater(grid, cell));

        public static WaterSide Find(Vector2Int origin, Footprint footprint, Func<Vector2Int, bool> isWater)
        {
            foreach (var spot in Order)
                if (Touches(spot, origin, footprint, isWater)) return spot;
            return WaterSide.None;
        }

        // A cell of the placed island is water when the structure standing on it is (the river), or the one
        // beneath it — the river runs on under a bridge. The sea is no cell at all, so it never counts — a
        // watermill wants a river.
        public static bool IsWater(IslandGrid grid, Vector2Int cell)
        {
            var land = grid.GetCell(cell);
            return land != null && (IsWater(land.occupant) || IsWater(land.beneath));
        }

        public static bool IsWater(StructureInstance structure)
            => structure != null && structure.Def != null && structure.Def.isWater;

        private static bool Touches(WaterSide spot, Vector2Int origin, Footprint footprint, Func<Vector2Int, bool> isWater)
        {
            switch (spot)
            {
                case WaterSide.SouthRight: return AcrossColumns(origin, footprint, isWater, south: true, rightHalf: true);
                case WaterSide.SouthLeft: return AcrossColumns(origin, footprint, isWater, south: true, rightHalf: false);
                case WaterSide.NorthRight: return AcrossColumns(origin, footprint, isWater, south: false, rightHalf: true);
                case WaterSide.NorthLeft: return AcrossColumns(origin, footprint, isWater, south: false, rightHalf: false);
                case WaterSide.West: return AcrossRows(origin, footprint, isWater, west: true);
                case WaterSide.East: return AcrossRows(origin, footprint, isWater, west: false);
                default: return false;
            }
        }

        // Water below the lowest (south) or above the highest (north) footprint cell of a column in the
        // given half of the box. A column's centre is x + 0.5; doubled, the comparison stays in integers.
        private static bool AcrossColumns(Vector2Int origin, Footprint footprint, Func<Vector2Int, bool> isWater,
                                          bool south, bool rightHalf)
        {
            int width = footprint.Size.x, height = footprint.Size.y;
            for (int x = 0; x < width; x++)
            {
                bool inHalf = rightHalf ? 2 * x + 1 >= width : 2 * x + 1 <= width;
                if (!inHalf) continue;

                for (int i = 0; i < height; i++)
                {
                    int y = south ? i : height - 1 - i;
                    if (!footprint.Contains(x, y)) continue;
                    if (isWater(new Vector2Int(origin.x + x, origin.y + y + (south ? -1 : 1)))) return true;
                    break;
                }
            }
            return false;
        }

        // Water left of the leftmost (west) or right of the rightmost (east) footprint cell of any row.
        private static bool AcrossRows(Vector2Int origin, Footprint footprint, Func<Vector2Int, bool> isWater, bool west)
        {
            int width = footprint.Size.x, height = footprint.Size.y;
            for (int y = 0; y < height; y++)
                for (int i = 0; i < width; i++)
                {
                    int x = west ? i : width - 1 - i;
                    if (!footprint.Contains(x, y)) continue;
                    if (isWater(new Vector2Int(origin.x + x + (west ? -1 : 1), origin.y + y))) return true;
                    break;
                }
            return false;
        }
    }
}
