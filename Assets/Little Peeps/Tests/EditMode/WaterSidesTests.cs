using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // WaterSides decides where a watermill may stand and which art it shows: the river right across ANY
    // one edge of the building, each edge its own wheel spot, and where several are touched the drawn art
    // first (SouthRight, West, NorthRight), its mirrored stand-ins after (SouthLeft, East, NorthLeft).
    // Pinned here: each spot on its own, a corner touch refused, the whole order, the outline of a shaped
    // footprint rather than its box, the halves of an odd width — and on the grid, what counts as water (a
    // structure whose def is isWater; not the sea, not any other occupant) and CanPlace(def) holding a
    // water building to it.
    //
    // The geometry runs on a plain set of river cells (the predicate overload), so it needs no
    // ScriptableObject; only the grid tests at the bottom create defs.
    //
    // A 2x1 mill at (-3,-2) — negative, as half of every island is — covers the left cell (-3,-2) and the
    // right cell (-2,-2).
    public class WaterSidesTests
    {
        private static readonly Vector2Int Origin = new Vector2Int(-3, -2);
        private static readonly Footprint Mill = Footprint.Rect(2, 1);

        private static readonly Vector2Int BelowRight = C(-2, -3);
        private static readonly Vector2Int BelowLeft = C(-3, -3);
        private static readonly Vector2Int AboveRight = C(-2, -1);
        private static readonly Vector2Int AboveLeft = C(-3, -1);
        private static readonly Vector2Int PastWest = C(-4, -2);
        private static readonly Vector2Int PastEast = C(-1, -2);

        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        private static HashSet<Vector2Int> River(params Vector2Int[] cells) => new HashSet<Vector2Int>(cells);

        private static WaterSide Find(HashSet<Vector2Int> river) => WaterSides.Find(Origin, Mill, river.Contains);

        // --- one spot -------------------------------------------------------------------------------

        [Test]
        public void RiverBelowTheRightCell_IsSouthRight()
            => Assert.AreEqual(WaterSide.SouthRight, Find(River(BelowRight)));

        [Test]
        public void RiverBelowTheLeftCell_IsSouthLeft()
            => Assert.AreEqual(WaterSide.SouthLeft, Find(River(BelowLeft)));

        [Test]
        public void RiverAboveTheRightCell_IsNorthRight()
            => Assert.AreEqual(WaterSide.NorthRight, Find(River(AboveRight)));

        [Test]
        public void RiverAboveTheLeftCell_IsNorthLeft()
            => Assert.AreEqual(WaterSide.NorthLeft, Find(River(AboveLeft)));

        [Test]
        public void RiverPastTheWestEnd_IsWest()
            => Assert.AreEqual(WaterSide.West, Find(River(PastWest)));

        [Test]
        public void RiverPastTheEastEnd_IsEast()
            => Assert.AreEqual(WaterSide.East, Find(River(PastEast)));

        [Test]
        public void NoRiver_IsNone()
            => Assert.AreEqual(WaterSide.None, Find(River()));

        [Test]
        public void ARiverTouchingOnlyACorner_IsNone()
            => Assert.AreEqual(WaterSide.None, Find(River(C(-4, -3), C(-1, -3), C(-4, -1), C(-1, -1))));

        [Test]
        public void ARiverMilesAway_IsNone()
            => Assert.AreEqual(WaterSide.None, Find(River(C(-3, -5), C(-2, -5))));

        // --- several spots: the order decides ---------------------------------------------------------

        [Test]
        public void TheOrder_DrawnArtFirst_ThenItsMirroredStandIns()
        {
            // Each spot wins over every spot after it: the river touches it and all the later ones.
            var order = new[]
            {
                (WaterSide.SouthRight, BelowRight), (WaterSide.West, PastWest), (WaterSide.NorthRight, AboveRight),
                (WaterSide.SouthLeft, BelowLeft), (WaterSide.East, PastEast), (WaterSide.NorthLeft, AboveLeft),
            };

            for (int i = 0; i < order.Length; i++)
            {
                var river = River();
                for (int j = i; j < order.Length; j++) river.Add(order[j].Item2);

                Assert.AreEqual(order[i].Item1, Find(river), $"from {order[i].Item1} on");
            }
        }

        [Test]
        public void ARiverAlongTheWholeSouth_IsSouthRight()
            => Assert.AreEqual(WaterSide.SouthRight, Find(River(C(-5, -3), C(-4, -3), BelowLeft, BelowRight, C(-1, -3))));

        [Test]
        public void ARiverAlongTheWholeNorth_IsNorthRight()
            => Assert.AreEqual(WaterSide.NorthRight, Find(River(AboveLeft, AboveRight)));

        [Test]
        public void ARiverBendingUnderTheLeftCell_AndUpTheWest_IsWest_TheDrawnArt()
            => Assert.AreEqual(WaterSide.West, Find(River(PastWest, C(-4, -3), BelowLeft)));

        // --- other footprints -----------------------------------------------------------------------

        [Test]
        public void ShapedFootprint_TheEdgesFollowTheOutline_NotTheBox()
        {
            // Drawn: a full top row over a stub at the bottom-left; the notch is local (1,0) = (-2,-2). The
            // right column's lowest cell is the top one, so its south edge looks into the notch — not down
            // at the box edge (-2,-3).
            var shape = Footprint.Parse("##",
                                        "#.");

            var underTheBox = River(C(-2, -3));
            var inTheNotch = River(C(-2, -2));

            Assert.AreEqual(WaterSide.None, WaterSides.Find(Origin, shape, underTheBox.Contains), "under the box");
            Assert.AreEqual(WaterSide.SouthRight, WaterSides.Find(Origin, shape, inTheNotch.Contains), "in the notch");
        }

        [Test]
        public void OddWidth_TheMiddleColumnCountsForBothHalves()
        {
            // 3x1 at (-3,-2): columns x -3, -2, -1; the middle one, -2, is on the middle line.
            var wide = Footprint.Rect(3, 1);

            var underTheMiddle = River(C(-2, -3));
            var underTheLeft = River(C(-3, -3));

            Assert.AreEqual(WaterSide.SouthRight, WaterSides.Find(Origin, wide, underTheMiddle.Contains), "the middle");
            Assert.AreEqual(WaterSide.SouthLeft, WaterSides.Find(Origin, wide, underTheLeft.Contains), "the left");
        }

        [Test]
        public void TallFootprint_AnyRowOfAnEnd_Counts()
        {
            // 2x2 at (-3,-2): the west end runs along x = -4 for y -2 and -1; the upper row alone will do.
            var tall = Footprint.Rect(2, 2);
            var river = River(C(-4, -1));

            Assert.AreEqual(WaterSide.West, WaterSides.Find(Origin, tall, river.Contains));
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
            Occupy(grid, Def(Vector2Int.one, isWater: true), BelowRight);

            Assert.AreEqual(WaterSide.SouthRight, WaterSides.Find(grid, Origin, Mill));
        }

        [Test]
        public void Grid_TheSea_IsNotWater()
        {
            // The island ends right under the mill: below it is the sea, no cell at all.
            var grid = TestIsland.Rect(C(-6, -2), C(3, 3));

            Assert.AreEqual(WaterSide.None, WaterSides.Find(grid, Origin, Mill));
        }

        [Test]
        public void Grid_AnyOtherOccupant_IsNotWater()
        {
            var grid = TestIsland.Square(-6, 3);
            Occupy(grid, Def(Vector2Int.one), BelowRight);   // a dry structure
            Occupy(grid, null, BelowLeft);                   // no def at all

            Assert.AreEqual(WaterSide.None, WaterSides.Find(grid, Origin, Mill));
        }

        [Test]
        public void CanPlace_AWaterBuilding_OnlyWithTheRiverAcrossAnEdge()
        {
            // One river cell, below the left cell at Origin.
            var grid = TestIsland.Square(-6, 3);
            Occupy(grid, Def(Vector2Int.one, isWater: true), BelowLeft);
            var mill = Def(C(2, 1), needsWaterSide: true);

            Assert.IsTrue(grid.CanPlace(Origin, mill), "one cell above it");
            Assert.IsTrue(grid.CanPlace(C(-2, -3), mill), "beside it, the river past the west end");
            Assert.IsFalse(grid.CanPlace(C(-2, -2), mill), "only a corner touching it");
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
            Occupy(grid, Def(Vector2Int.one, isWater: true), BelowLeft, BelowRight);
            var mill = Def(C(2, 1), needsWaterSide: true, border: 1);

            Assert.IsFalse(grid.CanPlace(Origin, mill));
        }
    }
}
