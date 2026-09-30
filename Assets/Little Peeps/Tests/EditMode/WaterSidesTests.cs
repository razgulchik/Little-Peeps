using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // WaterSides decides where a watermill may stand and which art it shows: one WHOLE side along water,
    // the first of South, West, East, never North. Pinned here: each side on its own, north and half a
    // side refused, every corner the river can bend around (the order decides), a shaped footprint's side
    // following its outline rather than its box, and on the grid — what counts as water (a structure whose
    // def is isWater; not the sea, not any other occupant) and CanPlace(def) holding a water building to it.
    //
    // The geometry runs on a plain set of river cells (the predicate overload), so it needs no
    // ScriptableObject; only the grid tests at the bottom create defs.
    //
    // A 2x2 mill at (-3,-2) — negative, as half of every island is — covers x -3..-2, y -2..-1, so its
    // south row is y = -3, north row y = 0, west column x = -4, east column x = -1.
    public class WaterSidesTests
    {
        private static readonly Vector2Int Origin = new Vector2Int(-3, -2);
        private static readonly Footprint Mill = Footprint.Rect(2, 2);

        private static readonly Vector2Int[] SouthRow = { C(-3, -3), C(-2, -3) };
        private static readonly Vector2Int[] NorthRow = { C(-3, 0), C(-2, 0) };
        private static readonly Vector2Int[] WestColumn = { C(-4, -2), C(-4, -1) };
        private static readonly Vector2Int[] EastColumn = { C(-1, -2), C(-1, -1) };

        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        private static HashSet<Vector2Int> River(params Vector2Int[][] runs)
        {
            var river = new HashSet<Vector2Int>();
            foreach (var run in runs) river.UnionWith(run);
            return river;
        }

        private static WaterSide Find(HashSet<Vector2Int> river) => WaterSides.Find(Origin, Mill, river.Contains);

        // --- one side -------------------------------------------------------------------------------

        [Test]
        public void RiverAlongTheSouthSide_IsSouth()
            => Assert.AreEqual(WaterSide.South, Find(River(SouthRow)));

        [Test]
        public void RiverAlongTheWestSide_IsWest()
            => Assert.AreEqual(WaterSide.West, Find(River(WestColumn)));

        [Test]
        public void RiverAlongTheEastSide_IsEast()
            => Assert.AreEqual(WaterSide.East, Find(River(EastColumn)));

        [Test]
        public void RiverAlongTheNorthSide_IsNone_TheWheelWouldHideBehindTheBuilding()
            => Assert.AreEqual(WaterSide.None, Find(River(NorthRow)));

        [Test]
        public void NoRiver_IsNone()
            => Assert.AreEqual(WaterSide.None, Find(River()));

        [Test]
        public void ARiverLongerThanTheSide_StillCounts()
        {
            var longRun = new[] { C(-6, -3), C(-5, -3), C(-4, -3), C(-3, -3), C(-2, -3), C(-1, -3), C(0, -3) };

            Assert.AreEqual(WaterSide.South, Find(River(longRun)));
        }

        // --- half a side ----------------------------------------------------------------------------

        [Test]
        public void HalfASide_IsNone()
            => Assert.AreEqual(WaterSide.None, Find(River(new[] { C(-3, -3) })));

        [Test]
        public void ARiverTurningAtTheCorner_LeavesHalfOfEachSide_IsNone()
        {
            // Down the west side, round the SW corner cell, along the south — but it leaves the west side
            // at y = -2 and the south side at x = -3, one cell of each.
            var turn = new[] { C(-4, -2), C(-4, -3), C(-3, -3) };

            Assert.AreEqual(WaterSide.None, Find(River(turn)));
        }

        // --- corners: two sides qualify, the order decides ------------------------------------------

        [Test]
        public void CornerSouthWest_IsSouth()
            => Assert.AreEqual(WaterSide.South, Find(River(WestColumn, new[] { C(-4, -3) }, SouthRow)));

        [Test]
        public void CornerSouthEast_IsSouth()
            => Assert.AreEqual(WaterSide.South, Find(River(SouthRow, new[] { C(-1, -3) }, EastColumn)));

        [Test]
        public void CornerNorthWest_IsWest()
            => Assert.AreEqual(WaterSide.West, Find(River(NorthRow, new[] { C(-4, 0) }, WestColumn)));

        [Test]
        public void CornerNorthEast_IsEast()
            => Assert.AreEqual(WaterSide.East, Find(River(NorthRow, new[] { C(-1, 0) }, EastColumn)));

        [Test]
        public void BetweenTwoRivers_WestAndEast_IsWest()
            => Assert.AreEqual(WaterSide.West, Find(River(WestColumn, EastColumn)));

        [Test]
        public void RiverAroundThreeSides_IsSouth()
        {
            var u = River(WestColumn, new[] { C(-4, -3) }, SouthRow, new[] { C(-1, -3) }, EastColumn);

            Assert.AreEqual(WaterSide.South, Find(u));
        }

        // --- a shaped footprint ---------------------------------------------------------------------

        [Test]
        public void ShapedFootprint_TheSideFollowsTheOutline_NotTheBox()
        {
            // Drawn: a full top row over a stub at the bottom-left; the notch is local (1,0) = (-2,-2).
            // The south outline is under the stub, (-3,-3), and under the top row's right cell — the
            // notch itself — not the box edge (-2,-3).
            var shape = Footprint.Parse("##",
                                        "#.");

            var alongTheBox = River(new[] { C(-3, -3), C(-2, -3) });
            var alongTheOutline = River(new[] { C(-3, -3), C(-2, -2) });

            Assert.AreEqual(WaterSide.None, WaterSides.Find(Origin, shape, alongTheBox.Contains), "the box edge");
            Assert.AreEqual(WaterSide.South, WaterSides.Find(Origin, shape, alongTheOutline.Contains), "the outline");
        }

        // --- on the grid ----------------------------------------------------------------------------

        // StructureDefs are ScriptableObjects, so they are tracked and destroyed rather than leaked into
        // the editor session.
        private readonly List<StructureDef> defs = new();

        [TearDown]
        public void DestroyCreatedDefs()
        {
            foreach (var def in defs)
                if (def != null) Object.DestroyImmediate(def);
            defs.Clear();
        }

        private StructureDef Def(Vector2Int size, bool isWater = false, bool needsWaterSide = false, int border = 0)
        {
            var def = ScriptableObject.CreateInstance<StructureDef>();
            def.size = size;
            def.isWater = isWater;
            def.needsWaterSide = needsWaterSide;
            def.border = border;
            defs.Add(def);
            return def;
        }

        private static void Occupy(IslandGrid grid, StructureDef def, params Vector2Int[] cells)
        {
            foreach (var cell in cells)
                grid.Place(cell, Footprint.Rect(1, 1), new StructureInstance { Def = def, Cell = cell });
        }

        [Test]
        public void Grid_ACellHoldingAWaterStructure_IsWater()
        {
            var grid = TestIsland.Square(-6, 3);
            Occupy(grid, Def(Vector2Int.one, isWater: true), SouthRow);

            Assert.AreEqual(WaterSide.South, WaterSides.Find(grid, Origin, Mill));
        }

        [Test]
        public void Grid_TheSea_IsNotWater()
        {
            // The island ends right under the mill: its south row is the sea, no cell at all.
            var grid = TestIsland.Rect(C(-6, -2), C(3, 3));

            Assert.AreEqual(WaterSide.None, WaterSides.Find(grid, Origin, Mill));
        }

        [Test]
        public void Grid_AnyOtherOccupant_IsNotWater()
        {
            var grid = TestIsland.Square(-6, 3);
            Occupy(grid, Def(Vector2Int.one), SouthRow[0]);   // a dry structure
            Occupy(grid, null, SouthRow[1]);                  // no def at all

            Assert.AreEqual(WaterSide.None, WaterSides.Find(grid, Origin, Mill));
        }

        [Test]
        public void CanPlace_AWaterBuilding_OnlyWithASideAlongTheRiver()
        {
            var grid = TestIsland.Square(-6, 3);
            Occupy(grid, Def(Vector2Int.one, isWater: true), SouthRow);
            var mill = Def(C(2, 2), needsWaterSide: true);

            Assert.IsTrue(grid.CanPlace(Origin, mill), "along the river");
            Assert.IsFalse(grid.CanPlace(Origin + C(1, 0), mill), "one step east: half the south side");
            Assert.IsFalse(grid.CanPlace(C(0, 0), mill), "away from the river");
        }

        [Test]
        public void CanPlace_AnOrdinaryBuilding_IgnoresWater()
        {
            var grid = TestIsland.Square(-6, 3);
            var house = Def(C(2, 2));

            Assert.IsTrue(grid.CanPlace(C(0, 0), house));
        }

        [Test]
        public void CanPlace_AWaterBuildingWithABorder_NeverFits()
        {
            // The border ring must be free, and the river's cells are taken — which is why the def's
            // tooltip asks for Border 0.
            var grid = TestIsland.Square(-6, 3);
            Occupy(grid, Def(Vector2Int.one, isWater: true), SouthRow);
            var mill = Def(C(2, 2), needsWaterSide: true, border: 1);

            Assert.IsFalse(grid.CanPlace(Origin, mill));
        }
    }
}
