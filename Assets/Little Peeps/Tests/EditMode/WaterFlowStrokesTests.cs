using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // WaterFlowStrokes turns the artist's tile of flow strokes into the map the sea's shader reads: for every pixel
    // of a stroke, its offset from the stroke's first pixel and the stroke's length. Pinned here: one stroke, two
    // in a row, a stroke running off the right edge and on at the left (one stroke, starting on the right), a row
    // that is stroke from end to end, the tile's own alpha carried over, and rows kept apart.
    //
    // Pictures read left to right: '#' a stroke pixel, '+' a half-clear one, '.' clear.
    public class WaterFlowStrokesTests
    {
        private static Color32[] Tile(params string[] rowsFromBottom)
        {
            int width = rowsFromBottom[0].Length;
            var pixels = new Color32[width * rowsFromBottom.Length];
            for (int y = 0; y < rowsFromBottom.Length; y++)
                for (int x = 0; x < width; x++)
                {
                    char c = rowsFromBottom[y][x];
                    byte alpha = c == '#' ? (byte)255 : c == '+' ? (byte)128 : (byte)0;
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            return pixels;
        }

        private static void AssertStroke(Color32 texel, int offset, int length, string where)
        {
            Assert.AreEqual(255, texel.a, $"{where}: not marked as a stroke");
            Assert.AreEqual(offset, texel.r, $"{where}: offset");
            Assert.AreEqual(length, texel.g, $"{where}: length");
        }

        [Test]
        public void OneStroke_CountsFromItsFirstPixel()
        {
            var map = WaterFlowStrokes.Build(Tile("..###."), 6, 1);

            AssertStroke(map[2], 0, 3, "x2");
            AssertStroke(map[3], 1, 3, "x3");
            AssertStroke(map[4], 2, 3, "x4");
            foreach (int x in new[] { 0, 1, 5 }) Assert.AreEqual(0, map[x].a, $"x{x} is clear");
        }

        [Test]
        public void TwoStrokes_EachCountsOnItsOwn()
        {
            var map = WaterFlowStrokes.Build(Tile("##..#."), 6, 1);

            AssertStroke(map[0], 0, 2, "x0");
            AssertStroke(map[1], 1, 2, "x1");
            AssertStroke(map[4], 0, 1, "x4");
        }

        [Test]
        public void StrokeOffTheRightEdge_GoesOnAtTheLeft()
        {
            var map = WaterFlowStrokes.Build(Tile("#..###"), 6, 1);

            AssertStroke(map[3], 0, 4, "x3");
            AssertStroke(map[4], 1, 4, "x4");
            AssertStroke(map[5], 2, 4, "x5");
            AssertStroke(map[0], 3, 4, "x0");
        }

        [Test]
        public void RowAllStroke_StartsAtItsFirstPixel()
        {
            var map = WaterFlowStrokes.Build(Tile("####"), 4, 1);

            for (int x = 0; x < 4; x++) AssertStroke(map[x], x, 4, $"x{x}");
        }

        [Test]
        public void TilesOwnAlpha_IsCarried()
        {
            var map = WaterFlowStrokes.Build(Tile(".+#."), 4, 1);

            AssertStroke(map[1], 0, 2, "x1");
            Assert.AreEqual(128, map[1].b, "half-clear pixel");
            Assert.AreEqual(255, map[2].b, "solid pixel");
        }

        [Test]
        public void Rows_AreKeptApart()
        {
            var map = WaterFlowStrokes.Build(Tile("##.", ".##"), 3, 2);

            AssertStroke(map[0], 0, 2, "row 0, x0");
            AssertStroke(map[1], 1, 2, "row 0, x1");
            Assert.AreEqual(0, map[3].a, "row 1, x0 is clear");
            AssertStroke(map[4], 0, 2, "row 1, x1");
            AssertStroke(map[5], 1, 2, "row 1, x2");
        }
    }
}
