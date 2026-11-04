using System;
using UnityEngine;

namespace LittlePeeps
{
    // The nine pieces of the massif sheet (mountain_9tiles), in its reading order — slice _0 is TopLeft,
    // _8 BottomRight. A piece is named by the cell's open edges: Top = no mountain to the north, Left = none
    // to the west, Centre = mountain on all four sides.
    public enum MountainPiece { TopLeft, Top, TopRight, Left, Centre, Right, BottomLeft, Bottom, BottomRight }

    // The inner corners drawn over a piece, where the massif turns in: mountain on both sides of the
    // corner, but not on the diagonal between them.
    [Flags]
    public enum MountainCorners { None = 0, NorthWest = 1, NorthEast = 2, SouthWest = 4, SouthEast = 8 }

    // A mountain cell's look: its piece and the inner corners over it (MountainVisual holds the sprites).
    public readonly struct MountainShape
    {
        public readonly MountainPiece Piece;
        public readonly MountainCorners Corners;

        public MountainShape(MountainPiece piece, MountainCorners corners = MountainCorners.None)
        {
            Piece = piece;
            Corners = corners;
        }

        public bool Has(MountainCorners corner) => (Corners & corner) == corner;

        public override string ToString() => Corners == MountainCorners.None ? $"{Piece}" : $"{Piece} + {Corners}";
    }

    // Which piece a mountain cell shows and which inner corners go over it, from which of its eight
    // neighbours are mountain too. The generator (IslandContent.AddMountains) grows a mountain out of 2×2
    // blocks, so every mountain cell lies in a 2×2 block of mountain, and that is what makes nine pieces and
    // four corners enough:
    //   - a cell has mountain on at least one side north–south and one side east–west, so the piece is
    //     always found — top, middle or bottom row; left, middle or right column;
    //   - an inner corner is a missing diagonal with mountain on both sides of it. Two corners on one side
    //     never happen: missing north-west and north-east, the cell to the north would be in no 2×2 block.
    //     So a cell carries no corner, one, or two opposite ones (NW + SE, NE + SW), and an overlay per
    //     corner draws them all.
    // Mountains of neighbouring zones that touch still make such a shape: each cell keeps its block.
    //
    // A cell the generator never makes still gets a piece: with no mountain north or south it shows the top
    // row, with none west or east the left column — the massif sheet has no one-cell-wide pieces.
    //
    // What counts as mountain is a predicate, like RiverShapes: the placed island asks the grid (a cell
    // holding a structure drawn as mountain), a generator or a test can hand in its own cell set.
    public static class MountainShapes
    {
        // Everything a mountain cell's look depends on: its four sides and the four diagonals between them.
        // Placing or removing a mountain re-picks the look of the cells at these offsets.
        public static readonly Vector2Int[] Around =
        {
            new(0, 1), new(1, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1),
        };

        // The look of `cell` on the placed island. The closure allocates per call — fine on the placement
        // paths, which is the only place this runs.
        public static MountainShape At(IslandGrid grid, Vector2Int cell) => Of(cell, c => IsMountain(grid, c));

        // A cell of the placed island is mountain when the structure standing on it is drawn as one — its
        // object carries a MountainVisual. The prefab says it once, instead of a def flag beside it that
        // could disagree. A construction site isn't a mountain yet: its object is the site.
        public static bool IsMountain(IslandGrid grid, Vector2Int cell)
        {
            var structure = grid.GetCell(cell)?.occupant?.RuntimeObject;
            return structure != null && structure.TryGetComponent<MountainVisual>(out _);
        }

        public static MountainShape Of(Vector2Int cell, Func<Vector2Int, bool> isMountain)
            => Pick(isMountain(cell + new Vector2Int(0, 1)), isMountain(cell + new Vector2Int(1, 0)),
                    isMountain(cell + new Vector2Int(0, -1)), isMountain(cell + new Vector2Int(-1, 0)),
                    isMountain(cell + new Vector2Int(1, 1)), isMountain(cell + new Vector2Int(1, -1)),
                    isMountain(cell + new Vector2Int(-1, -1)), isMountain(cell + new Vector2Int(-1, 1)));

        public static MountainShape Pick(bool north, bool east, bool south, bool west,
                                         bool northEast, bool southEast, bool southWest, bool northWest)
        {
            int row = !north ? 0 : !south ? 2 : 1;
            int column = !west ? 0 : !east ? 2 : 1;

            var corners = MountainCorners.None;
            if (north && west && !northWest) corners |= MountainCorners.NorthWest;
            if (north && east && !northEast) corners |= MountainCorners.NorthEast;
            if (south && west && !southWest) corners |= MountainCorners.SouthWest;
            if (south && east && !southEast) corners |= MountainCorners.SouthEast;

            return new MountainShape((MountainPiece)(row * 3 + column), corners);
        }
    }
}
