using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // A construction site lets through exactly the units whose bodies touch its area when it is placed,
    // and closes its wall behind each one once its body is clear of the area. Both verdicts are this
    // geometry, so it is pinned here: the area follows the footprint's SHAPE (a hole in the mask is not
    // part of the site), and a body straddling the line counts as inside.
    public class ConstructionAreaTests
    {
        private const float Cell = 1f;
        private IslandGrid grid;
        private readonly List<Rect> rects = new();

        [SetUp]
        public void SetUp() => grid = new IslandGrid(Cell);

        [Test]
        public void CellRects_ARectFootprint_CoversEveryCell()
        {
            ConstructionArea.CellRects(grid, new Vector2Int(2, -1), Footprint.Rect(2, 2), rects);

            Assert.That(rects.Count, Is.EqualTo(4));
            Assert.That(rects, Does.Contain(new Rect(2f, -1f, 1f, 1f)));
            Assert.That(rects, Does.Contain(new Rect(3f, 0f, 1f, 1f)));
        }

        [Test]
        public void CellRects_AShapedFootprint_LeavesTheHoleOut()
        {
            // Top row first: the north-east cell is the hole.
            ConstructionArea.CellRects(grid, Vector2Int.zero, Footprint.Parse("#.", "##"), rects);

            Assert.That(rects.Count, Is.EqualTo(3));
            Assert.IsFalse(rects.Contains(new Rect(1f, 1f, 1f, 1f)), "the hole is not part of the site");
        }

        [Test]
        public void CellRects_ClearsWhatTheListHeldBefore()
        {
            rects.Add(new Rect(9f, 9f, 1f, 1f));

            ConstructionArea.CellRects(grid, Vector2Int.zero, Footprint.Rect(1, 1), rects);

            Assert.That(rects, Is.EqualTo(new List<Rect> { new Rect(0f, 0f, 1f, 1f) }));
        }

        [Test]
        public void EdgeRect_RunsAlongTheEdge_CentredOnItsLine()
        {
            var horizontal = ConstructionArea.EdgeRect(grid, new Edge(new Vector2Int(0, 2), true), 0.2f);
            var vertical = ConstructionArea.EdgeRect(grid, new Edge(new Vector2Int(3, 0), false), 0.2f);

            AssertNear(horizontal.center, new Vector2(0.5f, 2f));
            AssertNear(horizontal.size, new Vector2(1f, 0.2f));
            AssertNear(vertical.center, new Vector2(3f, 0.5f));
            AssertNear(vertical.size, new Vector2(0.2f, 1f));
        }

        [TestCase(0.5f, 0.5f, true)]    // in the middle of the cell
        [TestCase(1.1f, 0.5f, true)]    // centre outside, body across the line: still inside
        [TestCase(1.19f, 0.5f, true)]   // all but touching
        [TestCase(1.25f, 0.5f, false)]  // clear by a hair
        [TestCase(1.15f, 1.15f, false)] // off the corner: within the bounding square, but not within reach
        public void Overlaps_ABodyOfRadiusPoint2_AgainstOneCell(float x, float y, bool expected)
        {
            rects.Add(new Rect(0f, 0f, 1f, 1f));

            Assert.That(ConstructionArea.Overlaps(rects, new Vector2(x, y), 0.2f), Is.EqualTo(expected));
        }

        [Test]
        public void Overlaps_ABodyInTheHoleOfAShape_IsNotInside()
        {
            ConstructionArea.CellRects(grid, Vector2Int.zero, Footprint.Parse("#.", "##"), rects);

            Assert.IsFalse(ConstructionArea.Overlaps(rects, new Vector2(1.6f, 1.6f), 0.2f));
        }

        private static void AssertNear(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-5f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-5f));
        }
    }
}
