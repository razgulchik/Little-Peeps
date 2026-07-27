using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // PlacementTarget.Resolve answers "what is the cursor on?" for hover, sell and move pick-up alike.
    // The interesting half is the precedence rule: a fence sitting on a cell boundary WINS over the cell
    // beneath it, but only while the cursor is close to the edge line. Get it wrong in either direction
    // and a whole class of objects becomes unclickable — too greedy and the cells along every fence stop
    // responding, too shy and a fence laid along a building can never be sold or moved.
    //
    // All of it is pure geometry over IslandGrid, so it runs with no scene. Coordinates are negative
    // throughout: the island is centred on the world origin.
    public class PlacementTargetTests
    {
        // The cell everything is measured around, and the pieces of its geometry the tests need.
        // cellSize 1 → cell (-3,-2) spans x -3..-2, y -2..-1; its centre is (-2.5,-1.5) and its south
        // boundary is the horizontal edge anchored at (-3,-2), whose line lies at y = -2.
        private static readonly Vector2Int Cell = new Vector2Int(-3, -2);
        private static readonly Edge SouthEdge = new Edge(new Vector2Int(-3, -2), true);
        private static readonly Vector2 CellCentre = new Vector2(-2.5f, -1.5f);
        private const float SouthEdgeLineY = -2f;

        private static IslandGrid Island(float cellSize = 1f) => TestIsland.Square(-5, 5, cellSize);

        private static StructureInstance PutStructure(IslandGrid grid, Vector2Int cell)
        {
            var instance = new StructureInstance { Cell = cell };   // Def null → the grid reads border 0
            grid.Place(cell, Vector2Int.one, instance);
            return instance;
        }

        private static EdgeInstance PutFence(IslandGrid grid, Edge edge)
        {
            var fence = new EdgeInstance { Edge = edge };
            grid.PlaceEdge(edge, fence);
            return fence;
        }

        // A point `perp` above the south edge line, horizontally centred on the cell.
        private static Vector2 AboveSouthEdge(float perp) => new Vector2(CellCentre.x, SouthEdgeLineY + perp);

        // --- nothing there ----------------------------------------------------------------------------

        [Test]
        public void Resolve_ReturnsNone_OnAnEmptyCell()
        {
            var target = PlacementTarget.Resolve(Island(), CellCentre);

            Assert.IsTrue(target.IsNone);
            Assert.IsFalse(target.IsFence);
            Assert.IsNull(target.RuntimeObject);
        }

        [Test]
        public void Resolve_ReturnsNone_OffTheIsland()
        {
            var grid = Island();

            // Well past the seeded -5..5 box, so there is no cell and no edge to hit.
            Assert.IsTrue(PlacementTarget.Resolve(grid, new Vector2(-40f, -40f)).IsNone);
        }

        // --- cell structures --------------------------------------------------------------------------

        [Test]
        public void Resolve_ReturnsTheStructureUnderTheCursor()
        {
            var grid = Island();
            var house = PutStructure(grid, Cell);

            var target = PlacementTarget.Resolve(grid, CellCentre);

            Assert.IsFalse(target.IsNone);
            Assert.IsFalse(target.IsFence);
            Assert.AreSame(house, target.Instance);
        }

        [Test]
        public void Resolve_ReturnsTheStructure_WhenTheNearbyEdgeCarriesNoFence()
        {
            var grid = Island();
            var house = PutStructure(grid, Cell);

            // Right on the edge line, but nothing is built there — the rule must not swallow the click.
            var target = PlacementTarget.Resolve(grid, AboveSouthEdge(0.05f));

            Assert.AreSame(house, target.Instance);
        }

        // --- the fence-wins rule ----------------------------------------------------------------------

        [Test]
        public void Resolve_ReturnsTheFence_WhenTheCursorIsOnTheEdgeLine()
        {
            var grid = Island();
            var fence = PutFence(grid, SouthEdge);

            var target = PlacementTarget.Resolve(grid, AboveSouthEdge(0.1f));

            Assert.IsTrue(target.IsFence);
            Assert.AreSame(fence, target.Fence);
            Assert.AreEqual(SouthEdge, target.Edge);
            Assert.IsNull(target.Instance);
        }

        [Test]
        public void Resolve_PrefersTheFence_EvenWhenTheCellBeneathIsOccupied()
        {
            var grid = Island();
            PutStructure(grid, Cell);
            var fence = PutFence(grid, SouthEdge);

            var target = PlacementTarget.Resolve(grid, AboveSouthEdge(0.1f));

            // This is the whole reason the rule exists: without it a fence laid along a building's cell
            // could never be sold or moved, because the cell underneath would always answer first.
            Assert.AreSame(fence, target.Fence);
        }

        [Test]
        public void Resolve_FallsBackToTheCell_WhenTheCursorIsAwayFromTheEdgeLine()
        {
            var grid = Island();
            var house = PutStructure(grid, Cell);
            PutFence(grid, SouthEdge);

            // Mid-cell: the fence is still the nearest edge, but the player is clearly aiming at the cell.
            var target = PlacementTarget.Resolve(grid, CellCentre);

            Assert.IsFalse(target.IsFence);
            Assert.AreSame(house, target.Instance);
        }

        [Test]
        public void Resolve_SwitchesFromFenceToCell_AcrossTheGrabDistance()
        {
            var grid = Island();
            var house = PutStructure(grid, Cell);
            var fence = PutFence(grid, SouthEdge);

            // The grab zone is 0.3 of a cell; 0.25 is inside it and 0.35 is outside. The exact boundary is
            // left alone on purpose — pinning float equality there would test the rounding, not the rule.
            Assert.AreSame(fence, PlacementTarget.Resolve(grid, AboveSouthEdge(0.25f)).Fence, "inside the grab zone");
            Assert.AreSame(house, PlacementTarget.Resolve(grid, AboveSouthEdge(0.35f)).Instance, "outside the grab zone");
        }

        [Test]
        public void Resolve_ScalesTheGrabDistanceWithTheCellSize()
        {
            // The same 0.2 world units from the edge line means different things on different grids,
            // because the grab zone is a FRACTION of a cell: 0.3 on a 1.0 cell, but 0.15 on a 0.5 cell.
            var wide = Island(1f);
            PutStructure(wide, Cell);
            var wideFence = PutFence(wide, SouthEdge);
            Assert.AreSame(wideFence, PlacementTarget.Resolve(wide, AboveSouthEdge(0.2f)).Fence,
                           "0.2 is inside a 1.0 cell's grab zone");

            var tight = Island(0.5f);
            var house = PutStructure(tight, Cell);
            PutFence(tight, SouthEdge);
            // On the half-size grid the same cell sits elsewhere in world space: its south edge line is at
            // y = -2 * 0.5 = -1, and the cell centre at x = -2.5 * 0.5 = -1.25.
            var target = PlacementTarget.Resolve(tight, new Vector2(-1.25f, -1f + 0.2f));
            Assert.AreSame(house, target.Instance, "0.2 is outside a 0.5 cell's grab zone");
        }

        [Test]
        public void Resolve_HandlesVerticalEdges()
        {
            var grid = Island();
            // West boundary of the same cell: the vertical edge anchored at (-3,-2), line at x = -3.
            var westEdge = new Edge(new Vector2Int(-3, -2), false);
            var fence = PutFence(grid, westEdge);

            // Perpendicular distance is measured on X for a vertical edge, not on Y.
            var target = PlacementTarget.Resolve(grid, new Vector2(-3f + 0.1f, CellCentre.y));

            Assert.AreSame(fence, target.Fence);
            Assert.AreEqual(westEdge, target.Edge);
        }

        // --- identity ---------------------------------------------------------------------------------

        [Test]
        public void Resolve_IsStableAcrossCalls_SoHoverCanCompareTargets()
        {
            var grid = Island();
            PutStructure(grid, Cell);

            // The hover path calls this every frame and skips its work when the target is unchanged — so
            // two resolves of the same spot must compare equal.
            var a = PlacementTarget.Resolve(grid, CellCentre);
            var b = PlacementTarget.Resolve(grid, CellCentre + new Vector2(0.01f, 0.01f));

            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void DifferentTargets_AreNotEqual()
        {
            var grid = Island();
            PutStructure(grid, Cell);
            PutStructure(grid, new Vector2Int(0, 0));
            PutFence(grid, SouthEdge);

            var house = PlacementTarget.Resolve(grid, CellCentre);
            var otherHouse = PlacementTarget.Resolve(grid, new Vector2(0.5f, 0.5f));
            var fence = PlacementTarget.Resolve(grid, AboveSouthEdge(0.1f));
            var none = PlacementTarget.Resolve(grid, new Vector2(4.5f, 4.5f));

            Assert.AreNotEqual(house, otherHouse);
            Assert.AreNotEqual(house, fence);
            Assert.AreNotEqual(house, none);
            Assert.AreEqual(none, default(PlacementTarget), "no target must equal the default, so hover starts clean");
        }
    }
}
