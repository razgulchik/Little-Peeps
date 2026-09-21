using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // Footprint is the shape every occupancy sweep reads (CanPlace, Place, Remove, exits, the halo, an
    // animal's ring), so what is pinned here is the contract those sweeps rely on: which local cells a
    // shape contains, which it claims once grown by a border, and — the migration guarantee — that a
    // def with no mask, a stale mask or an unpainted one is still the plain box it always was.
    //
    // Orientation is the trap: Parse takes rows top-first because that is how a shape is drawn in a
    // string, while the data is y-up like the grid. The first test nails that down before anything
    // else leans on it.
    public class FootprintTests
    {
        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        // --- construction -------------------------------------------------------------------------

        [Test]
        public void Parse_ReadsRowsTopFirst_IntoAYUpMask()
        {
            // Drawn: a stub sticking up from the left of a 2-wide base.
            var l = Footprint.Parse("#.",
                                    "##");

            Assert.AreEqual(C(2, 2), l.Size);
            Assert.IsTrue(l.Contains(0, 0), "bottom-left");
            Assert.IsTrue(l.Contains(1, 0), "bottom-right");
            Assert.IsTrue(l.Contains(0, 1), "the stub, top-LEFT — y=1 is the upper row");
            Assert.IsFalse(l.Contains(1, 1), "the notch, top-right");
            Assert.AreEqual(3, l.CellCount);
            Assert.IsFalse(l.IsRect);
        }

        [Test]
        public void Rect_ContainsEveryCellOfTheBox_AndNothingOutside()
        {
            var r = Footprint.Rect(3, 2);

            Assert.IsTrue(r.IsRect);
            Assert.AreEqual(6, r.CellCount);
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 2; y++)
                    Assert.IsTrue(r.Contains(x, y), $"({x},{y})");
            Assert.IsFalse(r.Contains(-1, 0));
            Assert.IsFalse(r.Contains(3, 0));
            Assert.IsFalse(r.Contains(0, 2));
        }

        [Test]
        public void NullMask_IsTheFullBox()
        {
            var fp = new Footprint(C(2, 2), null);
            Assert.IsTrue(fp.IsRect);
            Assert.AreEqual(4, fp.CellCount);
        }

        [Test]
        public void MaskOfTheWrongLength_IsTheFullBox()
        {
            // The box was resized after painting: the stale array must not be read as a shape.
            var fp = new Footprint(C(3, 3), new[] { true, false, true, true });
            Assert.IsTrue(fp.IsRect);
            Assert.AreEqual(9, fp.CellCount);
        }

        [Test]
        public void MaskWithNothingPainted_IsTheFullBox()
        {
            // "No cells" is never a footprint — nothing to place is nothing to claim, and CanPlace
            // would wave it through anywhere.
            var fp = new Footprint(C(2, 2), new[] { false, false, false, false });
            Assert.IsTrue(fp.IsRect);
            Assert.AreEqual(4, fp.CellCount);
        }

        [Test]
        public void MaskWithEverythingPainted_IsTheFullBox()
        {
            var fp = new Footprint(C(2, 1), new[] { true, true });
            Assert.IsTrue(fp.IsRect);
        }

        [Test]
        public void DefaultFootprint_IsEmpty()
        {
            // IslandSectionContent.Starting hangs on this: default(Footprint) is "no house".
            Assert.AreEqual(0, default(Footprint).CellCount);
        }

        // --- Claims: the shape grown by a border ----------------------------------------------------

        [Test]
        public void Claims_WithZeroBorder_IsContains()
        {
            var l = Footprint.Parse("#.", "##");

            Assert.IsTrue(l.Claims(0, 1, 0));
            Assert.IsFalse(l.Claims(1, 1, 0), "the notch is not claimed without a border");
            Assert.IsFalse(l.Claims(-1, 0, 0));
        }

        [Test]
        public void Claims_OnARect_IsTheBoxExpandedByTheBorder()
        {
            // What every bordered house has always claimed; Footprint must not move it by a cell.
            var r = Footprint.Rect(2, 2);

            for (int x = -1; x <= 2; x++)
                for (int y = -1; y <= 2; y++)
                    Assert.IsTrue(r.Claims(x, y, 1), $"({x},{y}) is inside the 4x4");

            Assert.IsFalse(r.Claims(-2, 0, 1));
            Assert.IsFalse(r.Claims(3, 0, 1));
            Assert.IsFalse(r.Claims(0, -2, 1));
            Assert.IsFalse(r.Claims(0, 3, 1));
            Assert.IsFalse(r.Claims(-2, -2, 1), "the far corner is two king's moves away");
        }

        [Test]
        public void Claims_OnAShape_HugsTheShape_DiagonalsIncluded()
        {
            // A 3x3 L: the bottom row and the left column.
            var l = Footprint.Parse("#..",
                                    "#..",
                                    "###");

            Assert.IsTrue(l.Claims(1, 1, 1), "inner corner of the L: beside two painted cells");
            Assert.IsTrue(l.Claims(2, 1, 1), "above the base's end");
            Assert.IsTrue(l.Claims(1, 2, 1), "beside the column's top");
            Assert.IsFalse(l.Claims(2, 2, 1), "the far corner of the box is two moves from any painted cell");
            Assert.IsTrue(l.Claims(3, 1, 1), "diagonal of the base's end, outside the box");
            Assert.IsFalse(l.Claims(3, 2, 1), "two rows above the base, right of the box");
            Assert.IsTrue(l.Claims(-1, -1, 1), "the outer corner ring");
        }

        [Test]
        public void Claims_SwallowsANotchNarrowerThanTheBorderAllows()
        {
            // A U with a one-cell notch: border 1 claims the notch, so nothing can be built in it.
            var u = Footprint.Parse("#.#",
                                    "###");

            Assert.IsFalse(u.Claims(1, 1, 0));
            Assert.IsTrue(u.Claims(1, 1, 1));
        }

        // --- Anchor ---------------------------------------------------------------------------------

        [Test]
        public void Anchor_IsTheLowestLeftmostPaintedCell()
        {
            Assert.AreEqual(C(0, 0), Footprint.Rect(2, 2).Anchor);
            // Bottom-left of the box is a hole: the anchor moves right along the bottom row.
            Assert.AreEqual(C(1, 0), Footprint.Parse("##", ".#").Anchor);
            // Bottom row entirely empty: up to the next row.
            Assert.AreEqual(C(0, 1), Footprint.Parse("##", "..").Anchor);
        }
    }
}
