using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // CarryTrail says where carried units come down and when the sea lets them go. What the player asked for is
    // pinned here: over a building, a forest, a rock they come down in the cell the cursor CAME FROM, not in
    // whichever free one is nearest; at the shore they stay inside the island; a jerk of the mouse jumps neither.
    //
    // cellSize 1: cell (x, y) spans x..x+1, y..y+1. The island is centred on the origin, so a good half of every
    // case is at negative coordinates.
    public class CarryTrailTests
    {
        private const float Clearance = 0.28f;   // a unit's radius and the gap to the cell's side
        private const float Tolerance = 1e-4f;

        private readonly List<StructureDef> defs = new();

        [TearDown]
        public void DestroyCreatedDefs()
        {
            foreach (var def in defs)
                if (def != null) Object.DestroyImmediate(def);
            defs.Clear();
        }

        // Land from (-6,-6) to (5,5) inclusive: the shore runs along x = -6, x = 6, y = -6 and y = 6.
        private static IslandGrid Island() => TestIsland.Square(-6, 5);

        // Something solid. Def stays null, which the grid reads as border 0 and everyone as solid.
        private static void Occupy(IslandGrid grid, int x, int y, int width = 1, int height = 1)
        {
            var cell = new Vector2Int(x, y);
            grid.Place(cell, Footprint.Rect(width, height), new StructureInstance { Cell = cell });
        }

        private StructureInstance OccupyWith(IslandGrid grid, int x, int y, bool passable, int border = 0)
        {
            var def = ScriptableObject.CreateInstance<StructureDef>();
            def.passable = passable;
            def.border = border;
            defs.Add(def);

            var instance = new StructureInstance { Def = def, Cell = new Vector2Int(x, y) };
            grid.Place(instance.Cell, def.Footprint, instance);
            return instance;
        }

        private static CarryTrail Way(IslandGrid grid, params Vector2[] points)
        {
            var trail = new CarryTrail();
            trail.Begin(grid, points[0]);
            for (int i = 1; i < points.Length; i++) trail.Extend(grid, points[i]);
            return trail;
        }

        private static Vector2 P(float x, float y) => new Vector2(x, y);

        private static Vector2Int DropCell(IslandGrid grid, CarryTrail trail)
            => grid.WorldToGrid(trail.DropPoint(grid, Vector2.zero, Clearance));

        private static void AssertNear(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        }

        // --- open ground ----------------------------------------------------------------------------

        [Test]
        public void OverOpenGround_ComesDownUnderTheCursor()
        {
            var grid = Island();

            var trail = Way(grid, P(-4.5f, -3.5f), P(-0.6f, 1.45f));

            AssertNear(trail.DropPoint(grid, Vector2.zero, Clearance), P(-0.6f, 1.45f));
        }

        [Test]
        public void StandingStill_StaysOnTheIsland()
        {
            var grid = Island();
            var trail = Way(grid, P(-2.5f, 0.5f));

            Assert.IsTrue(trail.Extend(grid, P(-2.5f, 0.5f)));
            Assert.AreEqual(new Vector2Int(-3, 0), DropCell(grid, trail));
        }

        // --- over something solid -------------------------------------------------------------------

        [TestCase(-1.5f, -0.5f, -2, -1)]   // from the west
        [TestCase(0.5f, -0.5f, 0, -1)]     // from the east
        [TestCase(-0.5f, -1.5f, -1, -2)]   // from the south
        [TestCase(-0.5f, 0.5f, -1, 0)]     // from the north
        public void OverABuilding_ComesDownInTheCellTheCursorCameFrom(float x, float y, int cellX, int cellY)
        {
            var grid = Island();
            Occupy(grid, -1, -1);

            var trail = Way(grid, P(x, y), P(-0.5f, -0.5f));

            Assert.AreEqual(new Vector2Int(cellX, cellY), DropCell(grid, trail));
        }

        [Test]
        public void OverTheFarEndOfABuilding_ComesDownWhereItCameFrom_NotAtTheNearestSide()
        {
            var grid = Island();
            Occupy(grid, -1, 0, width: 3);   // cells (-1..1, 0)

            var trail = Way(grid, P(-1.5f, 0.5f), P(1.9f, 0.5f));   // a hair from the free cell (2, 0)

            Assert.AreEqual(new Vector2Int(-2, 0), DropCell(grid, trail));
        }

        [Test]
        public void AcrossABuildingAndBackOntoIt_ComesDownOnTheSideItWasOnLast()
        {
            var grid = Island();
            Occupy(grid, -1, 0, width: 3);

            var trail = Way(grid, P(-1.5f, 0.5f), P(2.5f, 0.5f), P(0.5f, 0.5f));

            Assert.AreEqual(new Vector2Int(2, 0), DropCell(grid, trail));
        }

        // One move from well outside to deep inside: the cells in between were never a cursor position.
        [Test]
        public void AJerkOntoABuilding_ComesDownRightBesideIt()
        {
            var grid = Island();
            Occupy(grid, -1, 0, width: 3);

            var trail = Way(grid, P(-5.5f, 0.5f), P(1.5f, 0.5f));

            Assert.AreEqual(new Vector2Int(-2, 0), DropCell(grid, trail));
        }

        [Test]
        public void OverAForest_AGladeItCameThrough_IsWhereItComesDown()
        {
            var grid = Island();
            for (int x = -2; x <= 0; x++)
                for (int y = -2; y <= 0; y++)
                    if (x != -1 || y != -1) Occupy(grid, x, y);   // a ring of trees round the glade (-1,-1)

            var trail = Way(grid, P(-3.5f, -0.5f), P(-0.5f, -0.5f), P(0.5f, -0.5f));

            Assert.AreEqual(new Vector2Int(-1, -1), DropCell(grid, trail));
        }

        [Test]
        public void BegunOverABuilding_AndNeverOffIt_ComesDownInTheNearestFreeCell()
        {
            var grid = Island();
            Occupy(grid, -2, -2, width: 3, height: 3);   // cells (-2..0, -2..0)

            var trail = Way(grid, P(-0.9f, -0.5f), P(0.8f, -0.5f));

            Assert.AreEqual(new Vector2Int(1, -1), DropCell(grid, trail));
        }

        [Test]
        public void TheCellBuiltOnSince_ComesDownInTheNearestOther()
        {
            var grid = Island();
            Occupy(grid, -1, 0, width: 3);
            var trail = Way(grid, P(-1.5f, 0.5f), P(0.5f, 0.5f));

            Occupy(grid, -2, 0);

            var cell = DropCell(grid, trail);
            Assert.IsTrue(CarryTrail.IsFree(grid, cell), $"came down on {cell}, where something stands");
            Assert.That(cell, Is.EqualTo(new Vector2Int(-3, 0)).Or.EqualTo(new Vector2Int(-2, 1))
                              .Or.EqualTo(new Vector2Int(-2, -1)));
        }

        // --- the sea --------------------------------------------------------------------------------

        [Test]
        public void PastTheShore_TheWayEnds_AndTheyComeDownInside()
        {
            var grid = Island();
            var trail = Way(grid, P(4.5f, 0.5f));

            Assert.IsFalse(trail.Extend(grid, P(7.5f, 0.5f)));

            Assert.IsFalse(trail.OnIsland);
            Assert.That(trail.Head.x, Is.LessThan(6f), "the way ends on its last point of land");
            var drop = trail.DropPoint(grid, Vector2.zero, Clearance);
            Assert.AreEqual(new Vector2Int(5, 0), grid.WorldToGrid(drop));
            Assert.That(drop.x, Is.LessThanOrEqualTo(6f - Clearance + Tolerance), "clear of the shore itself");
        }

        // The shore on the negative side, where a rounding slip would show.
        [Test]
        public void PastTheWesternShore_TheyComeDownInside()
        {
            var grid = Island();
            var trail = Way(grid, P(-5.5f, -2.5f));

            Assert.IsFalse(trail.Extend(grid, P(-6.1f, -2.5f)));
            Assert.AreEqual(new Vector2Int(-6, -3), DropCell(grid, trail));
        }

        [Test]
        public void AJerkAcrossAStrait_DoesNotJumpIt()
        {
            var grid = TestIsland.Rect(new Vector2Int(-4, -2), new Vector2Int(-1, 2));   // x -4..0
            for (int x = 1; x <= 4; x++)                                                 // x 1..5: one cell of sea between
                for (int y = -2; y <= 2; y++)
                    grid.SetCell(new Vector2Int(x, y), TerrainType.Grass);
            var trail = Way(grid, P(-2.5f, 0.5f));

            Assert.IsFalse(trail.Extend(grid, P(3.5f, 0.5f)));

            Assert.AreEqual(new Vector2Int(-1, 0), DropCell(grid, trail));
        }

        [Test]
        public void OverABuildingOnTheShoreAndPastIt_TheyComeDownInFrontOfTheBuilding()
        {
            var grid = Island();
            Occupy(grid, 4, 0, width: 2);   // up to the shore at x = 6
            var trail = Way(grid, P(2.5f, 0.5f));

            Assert.IsFalse(trail.Extend(grid, P(8f, 0.5f)));

            Assert.AreEqual(new Vector2Int(3, 0), DropCell(grid, trail));
        }

        [Test]
        public void BegunInTheSea_IsNotOnTheIsland_UntilItBeginsAgainOnLand()
        {
            var grid = Island();
            var trail = Way(grid, P(8.5f, 0.5f));
            Assert.IsFalse(trail.OnIsland);

            trail.Begin(grid, P(5.5f, 0.5f));

            Assert.IsTrue(trail.OnIsland);
            Assert.AreEqual(new Vector2Int(5, 0), DropCell(grid, trail));
        }

        // A way that came back from the sea is a new one: where the last one was says nothing about it.
        [Test]
        public void BegunAgain_ForgetsWhatTheLastWayCameOver()
        {
            var grid = Island();
            Occupy(grid, 2, 0);
            var trail = Way(grid, P(-4.5f, -4.5f));

            trail.Begin(grid, P(2.5f, 0.5f));

            var cell = DropCell(grid, trail);
            Assert.IsTrue(CarryTrail.IsFree(grid, cell));
            Assert.That((cell - new Vector2Int(2, 0)).sqrMagnitude, Is.EqualTo(1), $"right beside the building, not {cell}");
        }

        // --- inside the cell ------------------------------------------------------------------------

        [TestCase(0.01f, 0.01f)]
        [TestCase(0.99f, 0.5f)]
        [TestCase(0.5f, 0.99f)]
        [TestCase(0.03f, 0.97f)]
        public void KeepsClearOfItsCellsSides_WhateverTheOffset(float dx, float dy)
        {
            var grid = Island();
            var trail = Way(grid, P(-2f + dx, -1f + dy));   // cell (-2,-1)

            foreach (var offset in new[] { Vector2.zero, P(0.2f, 0f), P(-0.2f, 0.2f), P(0f, -0.9f), P(5f, 5f) })
            {
                var drop = trail.DropPoint(grid, offset, Clearance);
                Assert.That(drop.x, Is.InRange(-2f + Clearance - Tolerance, -1f - Clearance + Tolerance), $"offset {offset}");
                Assert.That(drop.y, Is.InRange(-1f + Clearance - Tolerance, 0f - Clearance + Tolerance), $"offset {offset}");
            }
        }

        [Test]
        public void AnOffset_ComesOffTheCursor_WhileThereIsRoom()
        {
            var grid = Island();
            var trail = Way(grid, P(-1.5f, -0.5f));

            AssertNear(trail.DropPoint(grid, P(0.1f, -0.15f), Clearance), P(-1.4f, -0.65f));
        }

        [Test]
        public void ACellTooSmallForTheClearance_GivesItsMiddle()
        {
            var grid = Island();
            var trail = Way(grid, P(-1.9f, -0.1f));

            AssertNear(trail.DropPoint(grid, P(0.3f, 0.3f), 0.6f), P(-1.5f, -0.5f));
        }

        // --- what counts as free --------------------------------------------------------------------

        [Test]
        public void EmptyLandIsFree_TheSeaAndASolidStructureAreNot()
        {
            var grid = Island();
            Occupy(grid, -1, -1);

            Assert.IsTrue(CarryTrail.IsFree(grid, new Vector2Int(-2, -1)));
            Assert.IsFalse(CarryTrail.IsFree(grid, new Vector2Int(-1, -1)));
            Assert.IsFalse(CarryTrail.IsFree(grid, new Vector2Int(6, 0)));
        }

        // A field, a bush, a rack: units walk through them, so one may be put down right there.
        [Test]
        public void WhatUnitsWalkThrough_IsFree()
        {
            var grid = Island();
            OccupyWith(grid, -1, -1, passable: true);

            Assert.IsTrue(CarryTrail.IsFree(grid, new Vector2Int(-1, -1)));
        }

        // Until it stands, whatever is going up is a wall.
        [Test]
        public void AFieldStillUnderConstruction_IsNotFree()
        {
            var grid = Island();
            OccupyWith(grid, -1, -1, passable: true).UnderConstruction = true;

            Assert.IsFalse(CarryTrail.IsFree(grid, new Vector2Int(-1, -1)));
        }

        // A bordered structure claims a ring of cells round itself, and nothing stands on them.
        [Test]
        public void TheRingABorderedStructureClaims_IsFree_ItsOwnCellIsNot()
        {
            var grid = Island();
            OccupyWith(grid, -1, -1, passable: false, border: 1);

            Assert.IsFalse(CarryTrail.IsFree(grid, new Vector2Int(-1, -1)));
            Assert.IsTrue(CarryTrail.IsFree(grid, new Vector2Int(-2, -1)));
            Assert.IsTrue(CarryTrail.IsFree(grid, new Vector2Int(0, 0)));
        }
    }
}
