using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // EdgeStroke is the path a drawn fence follows from cell corner to cell corner. What matters to the
    // player: a shaking hand changes nothing (the dead zone), going back along the line erases it, a fast
    // flick still gives an unbroken line that turns the way it was heading, and the plan built from the
    // path leaves out what is already fenced and marks what the grid refuses or the purse can't cover.
    //
    // Pure geometry over IslandGrid. cellSize 1, so node (x,y) sits at world (x,y). Negative coordinates
    // throughout: the island is centred on the world origin.
    public class EdgeStrokeTests
    {
        private static IslandGrid Island() => TestIsland.Square(-6, 5);

        private static Vector2 At(int x, int y) => new Vector2(x, y);

        // Presses that make a stroke start exactly at node `s`. A stroke starts on the pressed edge, and
        // takes it in when it sets off ALONG it, so these press an edge ending at `s` that runs ACROSS the
        // way the stroke then goes.
        // On the vertical edge below s — for a stroke that sets off sideways.
        private static Vector2 PressBelow(int x, int y) => new Vector2(x + 0.05f, y - 0.1f);
        // On the horizontal edge left of s — for a stroke that sets off up or down.
        private static Vector2 PressLeft(int x, int y) => new Vector2(x - 0.1f, y + 0.05f);

        // A stroke pressed at `press` and led through `points` in order.
        private static EdgeStroke Drawn(IslandGrid grid, Vector2 press, params Vector2[] points)
        {
            var stroke = new EdgeStroke();
            stroke.Begin(grid.WorldToEdge(press));
            foreach (var p in points) stroke.Extend(grid, p);
            return stroke;
        }

        private static List<Edge> Segments(EdgeStroke stroke)
        {
            var list = new List<Edge>();
            for (int i = 0; i < stroke.SegmentCount; i++) list.Add(stroke.SegmentAt(i));
            return list;
        }

        private static Edge H(int x, int y) => new Edge(new Vector2Int(x, y), true);
        private static Edge V(int x, int y) => new Edge(new Vector2Int(x, y), false);

        // --- Edge.Between ------------------------------------------------------------------------------

        [Test]
        public void Between_GivesTheSameEdge_InEitherOrder()
        {
            Assert.AreEqual(H(-3, -2), Edge.Between(new Vector2Int(-3, -2), new Vector2Int(-2, -2)));
            Assert.AreEqual(H(-3, -2), Edge.Between(new Vector2Int(-2, -2), new Vector2Int(-3, -2)));
            Assert.AreEqual(V(-3, -2), Edge.Between(new Vector2Int(-3, -2), new Vector2Int(-3, -1)));
            Assert.AreEqual(V(-3, -2), Edge.Between(new Vector2Int(-3, -1), new Vector2Int(-3, -2)));
        }

        [Test]
        public void Between_Throws_ForNodesThatAreNotNeighbours()
        {
            Assert.Throws<System.ArgumentException>(() => Edge.Between(new Vector2Int(0, 0), new Vector2Int(1, 1)));
            Assert.Throws<System.ArgumentException>(() => Edge.Between(new Vector2Int(0, 0), new Vector2Int(0, 0)));
        }

        // --- the path ----------------------------------------------------------------------------------

        // Pressed just above the bottom side of cell (-3,-2), at `u` across it: the edge H(-3,-2), whose
        // ends are the nodes (-3,-2) and (-2,-2).
        private static Vector2 OnBottomOfCell(float u) => new Vector2(-3f + u, -1.95f);

        [Test]
        public void Begin_RemembersThePressedEdge_WithNoSegments()
        {
            var stroke = new EdgeStroke();
            stroke.Begin(Island().WorldToEdge(OnBottomOfCell(0.2f)));

            Assert.IsTrue(stroke.Active);
            Assert.AreEqual(H(-3, -2), stroke.PressedEdge);
            Assert.AreEqual(0, stroke.SegmentCount);
        }

        [Test]
        public void Extend_OntoEitherEndOfThePressedEdge_IsStillAClick()
        {
            var grid = Island();
            var stroke = Drawn(grid, OnBottomOfCell(0.5f));

            Assert.IsFalse(stroke.Extend(grid, At(-2, -2)));
            Assert.IsFalse(stroke.Extend(grid, At(-3, -2)));
            Assert.AreEqual(0, stroke.SegmentCount);
        }

        [Test]
        public void Extend_AlongThePressedEdge_StartsWithIt_EvenFromNearItsFarEnd()
        {
            // Pressed near the RIGHT end and led on to the right: the pressed edge is still the first
            // segment, not dropped for a start at its right end.
            var stroke = Drawn(Island(), OnBottomOfCell(0.8f), At(-1, -2));

            CollectionAssert.AreEqual(new[] { H(-3, -2), H(-2, -2) }, Segments(stroke));
        }

        [Test]
        public void Extend_AlongThePressedEdge_TheOtherWay_StartsWithItToo()
        {
            var stroke = Drawn(Island(), OnBottomOfCell(0.8f), At(-4, -2));

            CollectionAssert.AreEqual(new[] { H(-3, -2), H(-4, -2) }, Segments(stroke));
        }

        [Test]
        public void Extend_AcrossThePressedEdge_StartsFromItsNearerEnd_AndDropsIt()
        {
            var stroke = Drawn(Island(), OnBottomOfCell(0.8f), At(-2, -1));

            CollectionAssert.AreEqual(new[] { V(-2, -2) }, Segments(stroke));
        }

        [Test]
        public void Extend_BackOverThePressedEdge_ErasesIt()
        {
            var stroke = Drawn(Island(), OnBottomOfCell(0.8f), At(-1, -2), At(-2, -2), At(-3, -2));

            Assert.AreEqual(0, stroke.SegmentCount);
            Assert.IsTrue(stroke.Active);
        }

        [Test]
        public void Extend_StepsOntoANeighbour_AndThatIsOneSegment()
        {
            var stroke = Drawn(Island(), PressBelow(-3, -2), At(-2, -2));

            CollectionAssert.AreEqual(new[] { H(-3, -2) }, Segments(stroke));
        }

        [Test]
        public void Extend_ChangesNothing_InTheDeadZoneBetweenNodes()
        {
            var grid = Island();
            var stroke = Drawn(grid, PressBelow(-3, -2));

            // Halfway to the next node, and a shaky hand around it: never close enough to capture one.
            Assert.IsFalse(stroke.Extend(grid, new Vector2(-2.5f, -2f)));
            Assert.IsFalse(stroke.Extend(grid, new Vector2(-2.45f, -1.9f)));
            Assert.IsFalse(stroke.Extend(grid, new Vector2(-2.4f, -2.1f)));
            Assert.AreEqual(0, stroke.SegmentCount);
        }

        [Test]
        public void Extend_CapturesANode_JustInsideItsReach()
        {
            var grid = Island();
            var stroke = Drawn(grid, PressBelow(-3, -2));

            Assert.IsTrue(stroke.Extend(grid, new Vector2(-2.3f, -2f)));   // 0.3 from (-2,-2), under 0.35
            Assert.AreEqual(1, stroke.SegmentCount);
        }

        [Test]
        public void Extend_FollowsTurns_IntoOneUnbrokenLine()
        {
            var stroke = Drawn(Island(), PressBelow(-3, -2), At(-2, -2), At(-1, -2), At(-1, -1), At(-1, 0));

            CollectionAssert.AreEqual(new[] { H(-3, -2), H(-2, -2), V(-1, -2), V(-1, -1) }, Segments(stroke));
        }

        [Test]
        public void Extend_BackOntoThePreviousNode_ErasesTheLastSegment()
        {
            var stroke = Drawn(Island(), PressBelow(-3, -2), At(-2, -2), At(-1, -2), At(-2, -2));

            CollectionAssert.AreEqual(new[] { H(-3, -2) }, Segments(stroke));
        }

        [Test]
        public void Extend_BackToTheStart_LeavesNoSegments_ButKeepsTheStrokeGoing()
        {
            var stroke = Drawn(Island(), PressBelow(-3, -2), At(-2, -2), At(-1, -2), At(-3, -2));

            Assert.AreEqual(0, stroke.SegmentCount);
            Assert.IsTrue(stroke.Active);
        }

        [Test]
        public void Extend_FillsAFlickAlongALine_StepByStep()
        {
            var stroke = Drawn(Island(), PressBelow(-3, -2), At(0, -2));

            CollectionAssert.AreEqual(new[] { H(-3, -2), H(-2, -2), H(-1, -2) }, Segments(stroke));
        }

        [Test]
        public void Extend_TurnsADiagonalJump_TheWayTheLineWasHeading()
        {
            // Heading east, then a diagonal flick north-east: east first, then north.
            var east = Drawn(Island(), PressBelow(-3, -2), At(-2, -2), At(-1, -1));
            CollectionAssert.AreEqual(new[] { H(-3, -2), H(-2, -2), V(-1, -2) }, Segments(east));

            // Heading north, the same flick: north first, then east.
            var north = Drawn(Island(), PressLeft(-3, -2), At(-3, -1), At(-2, 0));
            CollectionAssert.AreEqual(new[] { V(-3, -2), V(-3, -1), H(-3, 0) }, Segments(north));
        }

        [Test]
        public void Extend_AFirstDiagonalJump_LeadsWithTheLongerLeg()
        {
            var stroke = Drawn(Island(), PressBelow(-3, -2), At(-1, -1));

            CollectionAssert.AreEqual(new[] { H(-3, -2), H(-2, -2), V(-1, -2) }, Segments(stroke));
        }

        [Test]
        public void Extend_ADiagonalJumpBack_ErasesTheCornerItCameThrough()
        {
            var stroke = Drawn(Island(), PressBelow(-3, -2), At(-2, -2), At(-2, -1), At(-3, -2));

            Assert.AreEqual(0, stroke.SegmentCount);
        }

        [Test]
        public void Clear_EndsTheStroke()
        {
            var stroke = Drawn(Island(), PressBelow(-3, -2), At(-2, -2));
            stroke.Clear();

            Assert.IsFalse(stroke.Active);
            Assert.AreEqual(0, stroke.SegmentCount);
        }

        // --- the plan ----------------------------------------------------------------------------------

        [Test]
        public void Collect_MarksEveryFreeEdge_Build()
        {
            var grid = Island();
            var stroke = Drawn(grid, PressBelow(-3, -2), At(-1, -2));
            var plan = new List<StrokeSegment>();

            Assert.AreEqual(2, stroke.Collect(grid, plan));
            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual(StrokeSegmentState.Build, plan[0].State);
            Assert.AreEqual(StrokeSegmentState.Build, plan[1].State);
        }

        [Test]
        public void Collect_LeavesOut_AnEdgeThatIsAlreadyFenced()
        {
            var grid = Island();
            grid.PlaceEdge(H(-2, -2), new EdgeInstance { Edge = H(-2, -2) });
            var stroke = Drawn(grid, PressBelow(-3, -2), At(0, -2));
            var plan = new List<StrokeSegment>();

            Assert.AreEqual(2, stroke.Collect(grid, plan));
            Assert.AreEqual(H(-3, -2), plan[0].Edge);
            Assert.AreEqual(H(-1, -2), plan[1].Edge);
        }

        [Test]
        public void Collect_CountsAnEdgeCrossedTwice_Once()
        {
            var grid = Island();
            // Round a cell and on along its first side again.
            var stroke = Drawn(grid, PressBelow(-3, -2),
                               At(-2, -2), At(-2, -1), At(-3, -1), At(-3, -2), At(-2, -2));
            var plan = new List<StrokeSegment>();

            Assert.AreEqual(5, stroke.SegmentCount);
            Assert.AreEqual(4, stroke.Collect(grid, plan));
        }

        [Test]
        public void Collect_MarksAnEdgeInsideAStructure_Blocked()
        {
            var grid = Island();
            // A 2x1 structure on cells (-3,-2) and (-2,-2): the vertical edge at x = -2 runs through it.
            grid.Place(new Vector2Int(-3, -2), Footprint.Rect(2, 1), new StructureInstance { Cell = new Vector2Int(-3, -2) });
            var stroke = Drawn(grid, PressLeft(-2, -2), At(-2, -1));
            var plan = new List<StrokeSegment>();

            Assert.AreEqual(0, stroke.Collect(grid, plan));
            Assert.AreEqual(StrokeSegmentState.Blocked, plan[0].State);
        }

        [Test]
        public void CollectFences_TakesOnlyTheFencedEdges_EachOnce_InDrawingOrder()
        {
            var grid = Island();
            grid.PlaceEdge(H(-3, -2), new EdgeInstance { Edge = H(-3, -2) });
            grid.PlaceEdge(H(-1, -2), new EdgeInstance { Edge = H(-1, -2) });
            // Along a row with fences on its 1st and 3rd edges, then round the cell above the 3rd and
            // along the 3rd a second time.
            var stroke = Drawn(grid, PressBelow(-3, -2), At(0, -2), At(0, -1), At(-1, -1), At(-1, -2), At(0, -2));
            var fences = new List<Edge>();

            stroke.CollectFences(grid, fences);

            CollectionAssert.AreEqual(new[] { H(-3, -2), H(-1, -2) }, fences);
        }

        [Test]
        public void CapBudget_TurnsTheBuildSegmentsPastTheBudget_OverBudget_AndLeavesBlockedAlone()
        {
            var plan = new List<StrokeSegment>
            {
                new StrokeSegment(H(0, 0), StrokeSegmentState.Build),
                new StrokeSegment(H(1, 0), StrokeSegmentState.Blocked),
                new StrokeSegment(H(2, 0), StrokeSegmentState.Build),
                new StrokeSegment(H(3, 0), StrokeSegmentState.Build),
            };

            EdgeStroke.CapBudget(plan, 2);

            Assert.AreEqual(StrokeSegmentState.Build, plan[0].State);
            Assert.AreEqual(StrokeSegmentState.Blocked, plan[1].State);
            Assert.AreEqual(StrokeSegmentState.Build, plan[2].State);
            Assert.AreEqual(StrokeSegmentState.OverBudget, plan[3].State);
        }
    }
}
