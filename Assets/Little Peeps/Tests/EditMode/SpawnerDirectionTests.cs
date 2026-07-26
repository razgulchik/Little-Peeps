using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // Spawner.CollectAllowedDirections decides where a unit may be launched: it walks the ring of cells
    // one step outside the structure's claimed territory and keeps the directions toward cells that are
    // land, unoccupied and not fenced off.
    //
    // The fence check is the fragile part. Each side reads a DIFFERENT edge anchor (south and west use
    // the territory's own min corner, north and east use one past its max), so an implementation that
    // reused a single anchor would still look right on one or two sides. Every side therefore gets its
    // own test with the anchor written out.
    //
    // The 1x1 cases sit at (-3,-2) — negative coordinates, since the island is centred on the origin —
    // where the expected directions are still the exact cardinals, so nothing has to be re-derived from
    // the implementation to state what the answer should be.
    public class SpawnerDirectionTests
    {
        private static readonly Vector2Int Origin = new Vector2Int(-3, -2);
        private static readonly Vector2Int Size1x1 = new Vector2Int(1, 1);

        private static readonly Vector2 South = new Vector2(0f, -1f);
        private static readonly Vector2 North = new Vector2(0f, 1f);
        private static readonly Vector2 West = new Vector2(-1f, 0f);
        private static readonly Vector2 East = new Vector2(1f, 0f);

        // Mirrors production: one buffer reused across launches. NUnit shares the fixture instance
        // between tests, so it is emptied up front rather than trusted to be empty.
        private readonly List<Vector2> dirs = new();

        [SetUp]
        public void ClearBuffer() => dirs.Clear();

        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        // Claim a single cell for some other structure. Def stays null, which the grid reads as border 0.
        private static void Occupy(IslandGrid grid, Vector2Int cell)
            => grid.Place(cell, Vector2Int.one, new StructureInstance { Cell = cell });

        private void Collect(IslandGrid grid, Vector2Int origin, Vector2Int size, int border = 0)
            => Spawner.CollectAllowedDirections(grid, origin, size, border, dirs);

        private void AssertHas(Vector2 expected, string what)
            => Assert.IsTrue(dirs.Exists(d => (d - expected).sqrMagnitude < 1e-6f),
                             $"expected a direction toward {what} ({expected}); got [{string.Join(", ", dirs)}]");

        private void AssertLacks(Vector2 unexpected, string what)
            => Assert.IsFalse(dirs.Exists(d => (d - unexpected).sqrMagnitude < 1e-6f),
                              $"{what} should be blocked ({unexpected}); got [{string.Join(", ", dirs)}]");

        private void AssertAllNormalised()
        {
            foreach (var d in dirs)
                Assert.That(d.magnitude, Is.EqualTo(1f).Within(1e-4f), $"direction {d} is not a unit vector");
        }

        // --- open perimeter ------------------------------------------------------------------------

        [Test]
        public void InteriorStructure_OpensAllFourCardinalDirections()
        {
            // The structure itself is never placed into the grid: this function only ever reads the
            // ring OUTSIDE the claimed territory, and the tests hold it to that.
            var grid = TestIsland.Square(-5, 5);

            Collect(grid, Origin, Size1x1);

            Assert.AreEqual(4, dirs.Count);
            AssertHas(South, "the cell below");
            AssertHas(North, "the cell above");
            AssertHas(West, "the cell to the left");
            AssertHas(East, "the cell to the right");
            AssertAllNormalised();
        }

        [Test]
        public void StructureInTheIslandCorner_DropsTheOffIslandSides()
        {
            var grid = TestIsland.Square(-5, 5);

            // (-5,-5) is the island's bottom-left cell, so south and west lead off the map.
            Collect(grid, C(-5, -5), Size1x1);

            Assert.AreEqual(2, dirs.Count);
            AssertHas(North, "the cell above");
            AssertHas(East, "the cell to the right");
        }

        [Test]
        public void OccupiedNeighbour_IsExcluded()
        {
            var grid = TestIsland.Square(-5, 5);
            Occupy(grid, C(-2, -2));   // the cell east of the structure

            Collect(grid, Origin, Size1x1);

            Assert.AreEqual(3, dirs.Count);
            AssertLacks(East, "a neighbour's cell");
            AssertHas(West, "the cell to the left");
        }

        [Test]
        public void FullyEnclosedStructure_YieldsNoDirections()
        {
            var grid = TestIsland.Square(-5, 5);
            Occupy(grid, C(-3, -3));
            Occupy(grid, C(-3, -1));
            Occupy(grid, C(-4, -2));
            Occupy(grid, C(-2, -2));

            Collect(grid, Origin, Size1x1);

            // This is the "keep resting and try again" path in LaunchFromSlot.
            Assert.IsEmpty(dirs);
        }

        // --- fences: one edge anchor per side --------------------------------------------------------

        [Test]
        public void FenceOnTheSouthEdge_BlocksOnlyTheSouthDirection()
        {
            var grid = TestIsland.Square(-5, 5);
            // Horizontal edge anchored at the territory's own min corner: it separates cell (-3,-2)
            // above from cell (-3,-3) below.
            grid.PlaceEdge(new Edge(C(-3, -2), true), new EdgeInstance());

            Collect(grid, Origin, Size1x1);

            Assert.AreEqual(3, dirs.Count);
            AssertLacks(South, "the fenced south side");
            AssertHas(North, "the cell above");
            AssertHas(West, "the cell to the left");
            AssertHas(East, "the cell to the right");
        }

        [Test]
        public void FenceOnTheNorthEdge_BlocksOnlyTheNorthDirection()
        {
            var grid = TestIsland.Square(-5, 5);
            // Horizontal edge one row ABOVE the territory: cell (-3,-1) above, cell (-3,-2) below.
            grid.PlaceEdge(new Edge(C(-3, -1), true), new EdgeInstance());

            Collect(grid, Origin, Size1x1);

            Assert.AreEqual(3, dirs.Count);
            AssertLacks(North, "the fenced north side");
            AssertHas(South, "the cell below");
        }

        [Test]
        public void FenceOnTheWestEdge_BlocksOnlyTheWestDirection()
        {
            var grid = TestIsland.Square(-5, 5);
            // Vertical edge anchored at the territory's own min corner: cell (-3,-2) to its right,
            // cell (-4,-2) to its left.
            grid.PlaceEdge(new Edge(C(-3, -2), false), new EdgeInstance());

            Collect(grid, Origin, Size1x1);

            Assert.AreEqual(3, dirs.Count);
            AssertLacks(West, "the fenced west side");
            AssertHas(East, "the cell to the right");
        }

        [Test]
        public void FenceOnTheEastEdge_BlocksOnlyTheEastDirection()
        {
            var grid = TestIsland.Square(-5, 5);
            // Vertical edge one column PAST the territory: cell (-2,-2) to its right, cell (-3,-2) to
            // its left. Sharing the south anchor here would fence the wrong side.
            grid.PlaceEdge(new Edge(C(-2, -2), false), new EdgeInstance());

            Collect(grid, Origin, Size1x1);

            Assert.AreEqual(3, dirs.Count);
            AssertLacks(East, "the fenced east side");
            AssertHas(West, "the cell to the left");
        }

        [Test]
        public void FenceOnAnEdgeElsewhere_BlocksNothing()
        {
            var grid = TestIsland.Square(-5, 5);
            grid.PlaceEdge(new Edge(C(3, 3), true), new EdgeInstance());

            Collect(grid, Origin, Size1x1);

            Assert.AreEqual(4, dirs.Count);
        }

        // --- footprint and border geometry ------------------------------------------------------------

        [Test]
        public void BorderedStructure_LooksAtTheRingBeyondItsBorder()
        {
            var grid = TestIsland.Square(-5, 5);

            // 1x1 at the origin with border 1 claims the -1..1 box, so the perimeter it examines is the
            // -2..2 ring: 3 cells per side, 12 in all. The unit still lands inside the border ring.
            Collect(grid, C(0, 0), Size1x1, border: 1);

            Assert.AreEqual(12, dirs.Count);
            AssertHas(South, "the cell two rows below, straight down");
            AssertHas(West, "the cell two columns left, straight across");
            AssertAllNormalised();
        }

        [Test]
        public void BorderedStructure_IgnoresOccupancyInsideItsOwnRing()
        {
            var grid = TestIsland.Square(-5, 5);
            Occupy(grid, C(0, -1));   // inside the claimed border ring, not on the examined perimeter
            Occupy(grid, C(0, -2));   // on the examined perimeter, directly south

            Collect(grid, C(0, 0), Size1x1, border: 1);

            Assert.AreEqual(11, dirs.Count, "only the perimeter cell counts");
            AssertLacks(South, "the occupied perimeter cell");
        }

        [Test]
        public void LargerFootprint_OffersOneDirectionPerPerimeterCell()
        {
            var grid = TestIsland.Square(-5, 5);

            // A 2x2 has two perimeter cells per side, and each gets its own direction — launches from a
            // wide building fan out instead of all leaving through one point.
            Collect(grid, C(-2, -2), new Vector2Int(2, 2));

            Assert.AreEqual(8, dirs.Count);
            AssertAllNormalised();
            for (int i = 0; i < dirs.Count; i++)
                for (int j = i + 1; j < dirs.Count; j++)
                    Assert.That((dirs[i] - dirs[j]).sqrMagnitude, Is.GreaterThan(1e-6f),
                                $"directions {i} and {j} are duplicates ({dirs[i]})");
        }

        [Test]
        public void Collect_ClearsTheBufferItIsGiven()
        {
            var grid = TestIsland.Square(-5, 5);
            dirs.Add(new Vector2(99f, 99f));   // the production buffer is reused across launches

            Collect(grid, Origin, Size1x1);

            Assert.AreEqual(4, dirs.Count);
            AssertLacks(new Vector2(99f, 99f), "stale content from a previous launch");
        }
    }
}
