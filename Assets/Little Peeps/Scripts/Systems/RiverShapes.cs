using System;
using UnityEngine;

namespace LittlePeeps
{
    // The three pieces a river is drawn from (RiverVisual holds the sprites).
    public enum RiverPiece { End, Straight, Corner }

    // A river cell's look: which piece, turned how many quarter turns counter-clockwise (0..3) from the
    // way the art draws it.
    public readonly struct RiverShape
    {
        public readonly RiverPiece Piece;
        public readonly int QuarterTurns;

        public RiverShape(RiverPiece piece, int quarterTurns)
        {
            Piece = piece;
            QuarterTurns = quarterTurns;
        }

        public override string ToString() => $"{Piece} +{QuarterTurns * 90}°";
    }

    // Which piece a river cell shows, from which of its four neighbours are river too. The generator
    // (IslandContent.AddRiver) lays a river one cell wide that never touches itself or another zone's
    // river, with both ends inland — so every river cell has one river neighbour (an end) or two
    // (opposite: a straight run; at a right angle: a bend), and three pieces draw any river.
    //
    // Each piece is drawn once, in one orientation, and turned into place:
    //   - End:      open to the WEST — the river comes in from the west, the rounded cap faces east;
    //   - Straight: WEST to EAST;
    //   - Corner:   joins EAST and SOUTH.
    // A cell the generator never makes still gets a piece: no river neighbour (a lone pool) or a
    // junction of three or four shows a straight run, along a full axis where there is one.
    //
    // What counts as river is a predicate, like WaterSides: the placed island asks the grid (a cell
    // holding an isWater structure), a generator or a preview can hand in its own cell set.
    public static class RiverShapes
    {
        // The piece for `cell` on the placed island. The closure allocates per call — fine on the
        // placement paths, which is the only place this runs.
        public static RiverShape At(IslandGrid grid, Vector2Int cell)
            => Of(cell, c => WaterSides.IsWater(grid, c));

        public static RiverShape Of(Vector2Int cell, Func<Vector2Int, bool> isRiver)
            => Pick(isRiver(cell + Vector2Int.up), isRiver(cell + Vector2Int.right),
                    isRiver(cell + Vector2Int.down), isRiver(cell + Vector2Int.left));

        public static RiverShape Pick(bool north, bool east, bool south, bool west)
        {
            int links = (north ? 1 : 0) + (east ? 1 : 0) + (south ? 1 : 0) + (west ? 1 : 0);

            if (links == 1)
                return new RiverShape(RiverPiece.End, west ? 0 : south ? 1 : east ? 2 : 3);

            if (links == 2 && !(east && west) && !(north && south))
                return new RiverShape(RiverPiece.Corner, east && south ? 0 : north && east ? 1 : west && north ? 2 : 3);

            return new RiverShape(RiverPiece.Straight, north && south && !(east && west) ? 1 : 0);
        }
    }
}
