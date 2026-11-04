using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static LittlePeeps.Tests.TestBiomes;

namespace LittlePeeps.Tests
{
    // MountainShapes picks the piece a mountain cell shows (one of the nine slices of the massif sheet) and
    // the inner corners drawn over it, from its eight neighbours. Pinned here: the nine pieces, when a
    // missing diagonal is an inner corner and when it is not, the fallbacks for one-cell-wide cells the
    // generator never makes, that the predicate is asked about the right cells, and — the point of nine
    // pieces and four corners — that every mountain the generator grows is drawn by them: each cell finds
    // its piece, with at most one corner on a side.
    //
    // Pure geometry and the generator: no ScriptableObject, the whole class runs under the offline harness.
    public class MountainShapesTests
    {
        private static readonly Vector2Int N = Vector2Int.up, E = Vector2Int.right, S = Vector2Int.down, W = Vector2Int.left;

        private static MountainShape Shape(MountainPiece piece, MountainCorners corners = MountainCorners.None)
            => new MountainShape(piece, corners);

        // Cells of a shape drawn as text, top row first: '#' = mountain. The first character of the last
        // row is at (0, 0).
        private static HashSet<Vector2Int> Cells(params string[] rows)
        {
            var cells = new HashSet<Vector2Int>();
            for (int r = 0; r < rows.Length; r++)
                for (int x = 0; x < rows[r].Length; x++)
                    if (rows[r][x] == '#') cells.Add(new Vector2Int(x, rows.Length - 1 - r));
            return cells;
        }

        // --- the nine pieces ----------------------------------------------------------------------------

        [TestCase(false, true, true, false, MountainPiece.TopLeft)]
        [TestCase(false, true, true, true, MountainPiece.Top)]
        [TestCase(false, false, true, true, MountainPiece.TopRight)]
        [TestCase(true, true, true, false, MountainPiece.Left)]
        [TestCase(true, true, true, true, MountainPiece.Centre)]
        [TestCase(true, false, true, true, MountainPiece.Right)]
        [TestCase(true, true, false, false, MountainPiece.BottomLeft)]
        [TestCase(true, true, false, true, MountainPiece.Bottom)]
        [TestCase(true, false, false, true, MountainPiece.BottomRight)]
        public void Pick_OpenEdges_NameThePiece(bool north, bool east, bool south, bool west, MountainPiece piece)
            => Assert.AreEqual(Shape(piece), MountainShapes.Pick(north, east, south, west, true, true, true, true));

        [Test]
        public void Pieces_FollowTheSheetsReadingOrder()
        {
            // MountainVisual's slots are filled from mountain_9tiles_0 … _8: row by row from the top-left.
            Assert.AreEqual(0, (int)MountainPiece.TopLeft);
            Assert.AreEqual(2, (int)MountainPiece.TopRight);
            Assert.AreEqual(4, (int)MountainPiece.Centre);
            Assert.AreEqual(6, (int)MountainPiece.BottomLeft);
            Assert.AreEqual(8, (int)MountainPiece.BottomRight);
        }

        // --- inner corners ------------------------------------------------------------------------------

        [TestCase(false, true, true, true, MountainCorners.NorthEast)]
        [TestCase(true, false, true, true, MountainCorners.SouthEast)]
        [TestCase(true, true, false, true, MountainCorners.SouthWest)]
        [TestCase(true, true, true, false, MountainCorners.NorthWest)]
        public void Pick_MissingDiagonalBetweenTwoMountainSides_IsAnInnerCorner(bool northEast, bool southEast, bool southWest, bool northWest, MountainCorners corner)
            => Assert.AreEqual(Shape(MountainPiece.Centre, corner),
                               MountainShapes.Pick(true, true, true, true, northEast, southEast, southWest, northWest));

        [Test]
        public void Pick_MissingDiagonalBesideAnOpenSide_IsNoCorner()
        {
            // The top-left of a block: nothing north, west or north-west — an outer corner, drawn by the piece.
            Assert.AreEqual(Shape(MountainPiece.TopLeft), MountainShapes.Pick(false, true, true, false, false, true, false, false));
            // The top edge: nothing north, so the missing north-east and north-west are no corners either.
            Assert.AreEqual(Shape(MountainPiece.Top), MountainShapes.Pick(false, true, true, true, false, true, true, false));
        }

        [Test]
        public void Pick_TwoOppositeCorners_BothDrawn()
            => Assert.AreEqual(Shape(MountainPiece.Centre, MountainCorners.NorthEast | MountainCorners.SouthWest),
                               MountainShapes.Pick(true, true, true, true, false, true, false, true));

        [Test]
        public void Has_AsksForOneCorner()
        {
            var shape = Shape(MountainPiece.Centre, MountainCorners.NorthEast | MountainCorners.SouthWest);
            Assert.IsTrue(shape.Has(MountainCorners.NorthEast));
            Assert.IsTrue(shape.Has(MountainCorners.SouthWest));
            Assert.IsFalse(shape.Has(MountainCorners.NorthWest));
            Assert.IsFalse(shape.Has(MountainCorners.SouthEast));
        }

        // --- what the generator never makes -------------------------------------------------------------

        [Test]
        public void Pick_OneCellWide_FallsBackToTheTopRowAndTheLeftColumn()
        {
            Assert.AreEqual(Shape(MountainPiece.TopLeft), MountainShapes.Pick(false, false, false, false, false, false, false, false));
            Assert.AreEqual(Shape(MountainPiece.Top), MountainShapes.Pick(false, true, false, true, false, false, false, false));
            Assert.AreEqual(Shape(MountainPiece.Left), MountainShapes.Pick(true, false, true, false, false, false, false, false));
        }

        // --- the predicate ------------------------------------------------------------------------------

        [Test]
        public void Of_AsksAboutTheEightNeighbours()
        {
            // Two 2×2 blocks overlapping on one cell, at negative cells, as half of every island is. The shared
            // cell has mountain on all four sides and two opposite diagonals missing.
            var offset = new Vector2Int(-5, -4);
            var mountain = new HashSet<Vector2Int>();
            foreach (var c in Cells("##.",
                                    "###",
                                    ".##")) mountain.Add(c + offset);

            Assert.AreEqual(Shape(MountainPiece.Centre, MountainCorners.NorthEast | MountainCorners.SouthWest),
                            MountainShapes.Of(new Vector2Int(1, 1) + offset, mountain.Contains));
            Assert.AreEqual(Shape(MountainPiece.TopLeft), MountainShapes.Of(new Vector2Int(0, 2) + offset, mountain.Contains));
            Assert.AreEqual(Shape(MountainPiece.TopRight), MountainShapes.Of(new Vector2Int(1, 2) + offset, mountain.Contains));
            Assert.AreEqual(Shape(MountainPiece.BottomLeft), MountainShapes.Of(new Vector2Int(0, 1) + offset, mountain.Contains));
            Assert.AreEqual(Shape(MountainPiece.TopRight), MountainShapes.Of(new Vector2Int(2, 1) + offset, mountain.Contains));
            Assert.AreEqual(Shape(MountainPiece.BottomLeft), MountainShapes.Of(new Vector2Int(1, 0) + offset, mountain.Contains));
            Assert.AreEqual(Shape(MountainPiece.BottomRight), MountainShapes.Of(new Vector2Int(2, 0) + offset, mountain.Contains));
        }

        [Test]
        public void Around_IsTheEightNeighbours()
        {
            var around = new HashSet<Vector2Int>(MountainShapes.Around);
            Assert.AreEqual(8, around.Count);
            foreach (var d in new[] { N, E, S, W, N + E, S + E, S + W, N + W }) Assert.IsTrue(around.Contains(d), $"{d}");
        }

        // --- the generator's mountains ------------------------------------------------------------------

        [Test]
        public void GeneratedMountains_EveryCellFindsItsPiece_AtMostOneCornerASide()
        {
            int cells = 0, corners = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                var g = new IslandGenerator(new IslandRules { startArea = 36 }, seed);
                var start = g.GenerateStart();
                g.Commit(start, g.Populate(start, StartingGrasslands(), Footprint.Rect(2, 2)));
                for (int zone = 0; zone < 4; zone++) Grow(g, StoneDesert());

                // The whole island's mountain cells at once: mountains of neighbouring zones that touch are
                // drawn as one massif.
                var all = new HashSet<Vector2Int>();
                foreach (var section in g.Sections) if (section.Content != null) all.UnionWith(section.Content.mountains);

                foreach (var cell in all)
                {
                    cells++;
                    bool north = all.Contains(cell + N), south = all.Contains(cell + S);
                    bool east = all.Contains(cell + E), west = all.Contains(cell + W);
                    Assert.IsTrue(north || south, $"seed {seed}, {cell}: one cell tall — no piece draws it");
                    Assert.IsTrue(east || west, $"seed {seed}, {cell}: one cell wide — no piece draws it");

                    var shape = MountainShapes.Of(cell, all.Contains);
                    Assert.IsFalse(shape.Has(MountainCorners.NorthWest | MountainCorners.NorthEast), $"seed {seed}, {cell}: {shape}");
                    Assert.IsFalse(shape.Has(MountainCorners.SouthWest | MountainCorners.SouthEast), $"seed {seed}, {cell}: {shape}");
                    Assert.IsFalse(shape.Has(MountainCorners.NorthWest | MountainCorners.SouthWest), $"seed {seed}, {cell}: {shape}");
                    Assert.IsFalse(shape.Has(MountainCorners.NorthEast | MountainCorners.SouthEast), $"seed {seed}, {cell}: {shape}");
                    if (shape.Corners != MountainCorners.None) corners++;
                }
            }
            Assert.Greater(cells, 100, "too few mountains generated to say anything");
            Assert.Greater(corners, 0, "no inner corner generated — the corner checks said nothing");
        }

        private static void Grow(IslandGenerator g, IslandBiome biome)
        {
            foreach (var candidate in g.Propose(3))
            {
                var content = g.Populate(candidate, biome);
                if (content != null) { g.Commit(candidate, content); return; }
            }
        }
    }
}
