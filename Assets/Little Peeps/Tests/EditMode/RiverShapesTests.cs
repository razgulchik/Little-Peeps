using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static LittlePeeps.Tests.TestBiomes;

namespace LittlePeeps.Tests
{
    // RiverShapes picks the piece a river cell shows from its four neighbours, turned from the way the art
    // draws it (End open to the west, Straight west–east, Corner east+south; turns counter-clockwise).
    // Pinned here: every neighbour set a river can have, the fallbacks for the ones the generator never
    // makes, that the predicate is asked about the right cells, and — the point of the three pieces —
    // that every river the generator lays is drawn joined: each cell's piece opens exactly towards the
    // cells before and after it on the path.
    //
    // Pure geometry and the generator: no ScriptableObject, the whole class runs under the offline harness.
    public class RiverShapesTests
    {
        private static readonly Vector2Int N = Vector2Int.up, E = Vector2Int.right, S = Vector2Int.down, W = Vector2Int.left;

        private static RiverShape Shape(RiverPiece piece, int quarterTurns) => new RiverShape(piece, quarterTurns);

        // Where a shape opens: the piece's openings as drawn, turned counter-clockwise (x, y) → (−y, x).
        private static HashSet<Vector2Int> Opens(RiverShape shape)
        {
            var drawn = shape.Piece switch
            {
                RiverPiece.End => new[] { W },
                RiverPiece.Straight => new[] { W, E },
                _ => new[] { E, S },
            };
            var opens = new HashSet<Vector2Int>();
            foreach (var d in drawn)
            {
                var v = d;
                for (int i = 0; i < shape.QuarterTurns; i++) v = new Vector2Int(-v.y, v.x);
                opens.Add(v);
            }
            return opens;
        }

        // --- every neighbour set a river has ---------------------------------------------------------

        [TestCase(false, false, false, true, RiverPiece.End, 0, TestName = "End_RiverFromTheWest_AsDrawn")]
        [TestCase(false, false, true, false, RiverPiece.End, 1, TestName = "End_RiverFromTheSouth_QuarterTurn")]
        [TestCase(false, true, false, false, RiverPiece.End, 2, TestName = "End_RiverFromTheEast_HalfTurn")]
        [TestCase(true, false, false, false, RiverPiece.End, 3, TestName = "End_RiverFromTheNorth_ThreeQuarters")]
        [TestCase(false, true, false, true, RiverPiece.Straight, 0, TestName = "Straight_WestEast_AsDrawn")]
        [TestCase(true, false, true, false, RiverPiece.Straight, 1, TestName = "Straight_NorthSouth_QuarterTurn")]
        [TestCase(false, true, true, false, RiverPiece.Corner, 0, TestName = "Corner_EastSouth_AsDrawn")]
        [TestCase(true, true, false, false, RiverPiece.Corner, 1, TestName = "Corner_NorthEast_QuarterTurn")]
        [TestCase(true, false, false, true, RiverPiece.Corner, 2, TestName = "Corner_WestNorth_HalfTurn")]
        [TestCase(false, false, true, true, RiverPiece.Corner, 3, TestName = "Corner_SouthWest_ThreeQuarters")]
        public void Pick_RiverNeighbours(bool north, bool east, bool south, bool west, RiverPiece piece, int quarterTurns)
            => Assert.AreEqual(Shape(piece, quarterTurns), RiverShapes.Pick(north, east, south, west));

        [Test]
        public void Pick_EveryOneOrTwoNeighbours_OpensExactlyTowardsThem()
        {
            var dirs = new[] { N, E, S, W };
            for (int mask = 1; mask < 16; mask++)
            {
                var links = new HashSet<Vector2Int>();
                for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) links.Add(dirs[i]);
                if (links.Count > 2) continue;

                var shape = RiverShapes.Pick(links.Contains(N), links.Contains(E), links.Contains(S), links.Contains(W));
                Assert.IsTrue(links.SetEquals(Opens(shape)), $"mask {mask}: {shape}");
            }
        }

        // --- what the generator never makes ---------------------------------------------------------

        [Test]
        public void Pick_NoNeighbour_IsAStraightRun()
            => Assert.AreEqual(Shape(RiverPiece.Straight, 0), RiverShapes.Pick(false, false, false, false));

        [TestCase(true, true, false, true, 0, TestName = "Junction_NorthEastWest_RunsWestEast")]
        [TestCase(false, true, true, true, 0, TestName = "Junction_EastSouthWest_RunsWestEast")]
        [TestCase(true, true, true, false, 1, TestName = "Junction_NorthEastSouth_RunsNorthSouth")]
        [TestCase(true, false, true, true, 1, TestName = "Junction_NorthSouthWest_RunsNorthSouth")]
        [TestCase(true, true, true, true, 0, TestName = "Junction_AllFour_RunsWestEast")]
        public void Pick_Junction_IsAStraightRunAlongAFullAxis(bool north, bool east, bool south, bool west, int quarterTurns)
            => Assert.AreEqual(Shape(RiverPiece.Straight, quarterTurns), RiverShapes.Pick(north, east, south, west));

        // --- the predicate ----------------------------------------------------------------------------

        [Test]
        public void Of_AsksAboutTheFourNeighbours_NotTheDiagonals()
        {
            // A bend at (-2,-3) — negative, as half of every island is: river to the north and the west,
            // and a diagonal river cell that must not count.
            var cell = new Vector2Int(-2, -3);
            var river = new HashSet<Vector2Int> { cell, cell + N, cell + W, cell + S + E };
            Assert.AreEqual(Shape(RiverPiece.Corner, 2), RiverShapes.Of(cell, river.Contains));
        }

        // --- the generator's rivers -------------------------------------------------------------------

        [Test]
        public void GeneratedRivers_EveryCellOpensTowardsItsPathNeighbours()
        {
            int rivers = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                var g = new IslandGenerator(new IslandRules { startArea = 36 }, seed);
                var start = g.GenerateStart();
                g.Commit(start, g.Populate(start, StartingGrasslands(), Footprint.Rect(2, 2)));
                for (int zone = 0; zone < 3; zone++) Grow(g, Grasslands());

                // The whole island's river cells at once: rivers of different zones never touch, so a
                // neighbour from another zone would show up here as a wrong opening.
                var all = new HashSet<Vector2Int>();
                foreach (var section in g.Sections) if (section.Content != null) all.UnionWith(section.Content.river);

                foreach (var section in g.Sections)
                {
                    var path = section.Content?.river;
                    if (path == null || path.Count == 0) continue;
                    rivers++;
                    for (int i = 0; i < path.Count; i++)
                    {
                        var expected = new HashSet<Vector2Int>();
                        if (i > 0) expected.Add(path[i - 1] - path[i]);
                        if (i + 1 < path.Count) expected.Add(path[i + 1] - path[i]);

                        var shape = RiverShapes.Of(path[i], all.Contains);
                        Assert.IsTrue(expected.SetEquals(Opens(shape)), $"seed {seed}, river cell {i} of {path.Count} at {path[i]}: {shape}");
                    }
                }
            }
            Assert.Greater(rivers, 10, "too few rivers generated to say anything");
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
