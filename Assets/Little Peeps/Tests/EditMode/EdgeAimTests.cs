using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // EdgeAim is the fence ghost's hold on its edge. In the middle of a cell all four sides are nearly
    // equally near, and without the hold a one-pixel twitch flips the ghost between them — the jitter the
    // player complained about. Pinned here: a twitch changes nothing, a clear lean does, and the hold lets
    // go once the cursor is in another cell.
    //
    // cellSize 1. Cell (-3,-2) spans x -3..-2, y -2..-1; its centre is (-2.5,-1.5). Negative coordinates
    // throughout: the island is centred on the world origin.
    public class EdgeAimTests
    {
        private static IslandGrid Island() => TestIsland.Square(-6, 5);

        private static readonly Edge Bottom = new Edge(new Vector2Int(-3, -2), true);
        private static readonly Edge Top = new Edge(new Vector2Int(-3, -1), true);
        private static readonly Edge Left = new Edge(new Vector2Int(-3, -2), false);
        private static readonly Edge NextBottom = new Edge(new Vector2Int(-2, -2), true);   // bottom of cell (-2,-2)

        // Twitches around the centre, each within 0.05 of it on both axes.
        private static readonly Vector2[] AroundTheCentre =
        {
            new Vector2(-2.5f, -1.5f), new Vector2(-2.45f, -1.45f), new Vector2(-2.55f, -1.55f),
            new Vector2(-2.45f, -1.55f), new Vector2(-2.55f, -1.45f),
        };

        [Test]
        public void Update_TakesTheNearestEdge_AtFirst()
        {
            var aim = new EdgeAim();

            Assert.AreEqual(Bottom, aim.Update(Island(), new Vector2(-2.5f, -1.9f)));
        }

        [Test]
        public void Update_HoldsTheEdge_WhileTheCursorTwitchesAroundTheCentre()
        {
            var grid = Island();
            var aim = new EdgeAim();
            aim.Update(grid, new Vector2(-2.5f, -1.9f));

            foreach (var p in AroundTheCentre)
                Assert.AreEqual(Bottom, aim.Update(grid, p), $"at {p}");
        }

        [Test]
        public void Update_HoldsWhicheverSideItCameFrom()
        {
            var grid = Island();
            var aim = new EdgeAim();
            aim.Update(grid, new Vector2(-2.9f, -1.5f));

            foreach (var p in AroundTheCentre)
                Assert.AreEqual(Left, aim.Update(grid, p), $"at {p}");
        }

        [Test]
        public void Update_MovesOn_AtAClearLeanTowardsAnotherSide()
        {
            var grid = Island();
            var aim = new EdgeAim();
            aim.Update(grid, new Vector2(-2.5f, -1.9f));
            aim.Update(grid, new Vector2(-2.5f, -1.5f));

            Assert.AreEqual(Top, aim.Update(grid, new Vector2(-2.5f, -1.25f)));
        }

        [Test]
        public void Update_HoldsAtTheCornerItSharesWithTheNextEdge()
        {
            var grid = Island();
            var aim = new EdgeAim();
            aim.Update(grid, new Vector2(-2.5f, -1.95f));

            // Just past the corner (-2,-2), already nearer the next cell's bottom edge, but only just.
            Assert.AreEqual(Bottom, aim.Update(grid, new Vector2(-1.9f, -1.95f)));
        }

        [Test]
        public void Update_LetsGo_OnceTheCursorIsWellIntoTheNextCell()
        {
            var grid = Island();
            var aim = new EdgeAim();
            aim.Update(grid, new Vector2(-2.5f, -1.9f));

            Assert.AreEqual(NextBottom, aim.Update(grid, new Vector2(-1.5f, -1.9f)));
        }

        // --- the pull to a wall's end -----------------------------------------------------------------
        // A wall of one fence on Bottom: its ends are the corners (-3,-2) and (-2,-2). The free edges at
        // the east end (-2,-2) are NextBottom (on along the wall), EastUp and EastDown (turns).

        private static readonly Edge EastUp = new Edge(new Vector2Int(-2, -2), false);
        private static readonly Edge EastDown = new Edge(new Vector2Int(-2, -3), false);
        private static readonly Edge FarRight = new Edge(new Vector2Int(-1, -2), false);   // the right side of cell (-2,-2)

        private static void PutFence(IslandGrid grid, Edge edge) => grid.PlaceEdge(edge, new EdgeInstance { Edge = edge });

        private static IslandGrid OneFenceWall()
        {
            var grid = Island();
            PutFence(grid, Bottom);
            return grid;
        }

        [Test]
        public void Update_CarriesTheWallOn_AheadOfItsEnd()
        {
            Assert.AreEqual(NextBottom, new EdgeAim().Update(OneFenceWall(), new Vector2(-1.6f, -1.9f)));
        }

        [Test]
        public void Update_TurnsAtTheEnd_WhenTheCursorIsBesideIt()
        {
            Assert.AreEqual(EastUp, new EdgeAim().Update(OneFenceWall(), new Vector2(-2.05f, -1.6f)));
        }

        [Test]
        public void Update_KeepsTheGhostOnTheEnd_WhileTheCursorIsACellAway()
        {
            // By the far side of the next cell, 1.03 from the end: the plain nearest would be that cell's
            // right side, but the ghost stays on the wall, carrying it on.
            Assert.AreEqual(NextBottom, new EdgeAim().Update(OneFenceWall(), new Vector2(-1.1f, -1.5f)));
        }

        [Test]
        public void Update_PullsToTheEnd_AgainstAHoldElsewhere()
        {
            var grid = OneFenceWall();
            var aim = new EdgeAim();
            Assert.AreEqual(FarRight, aim.Update(grid, new Vector2(-0.6f, -1.5f)));   // out of reach

            // Into reach (1.21 from the end), still nearest the held edge: the end pulls anyway.
            Assert.AreEqual(NextBottom, aim.Update(grid, new Vector2(-0.9f, -1.5f)));
        }

        [Test]
        public void Update_StepsOffTheFence_OntoTheEndsNearestFreeEdge()
        {
            var grid = Island();
            var middle = new Edge(new Vector2Int(-4, -2), true);
            PutFence(grid, new Edge(new Vector2Int(-5, -2), true));
            PutFence(grid, middle);
            PutFence(grid, Bottom);   // a wall of three, its ends at (-5,-2) and (-2,-2)
            var aim = new EdgeAim();

            // On the middle fence, 1.5 from either end: plain nearest — the fence itself.
            Assert.AreEqual(middle, aim.Update(grid, new Vector2(-3.5f, -1.95f)));
            // Near the east end: off the fence and onto the end, hold or not.
            Assert.AreEqual(EastUp, aim.Update(grid, new Vector2(-2.15f, -1.95f)));
        }

        [Test]
        public void Update_HoldsAmongTheEndsOwnEdges()
        {
            var grid = OneFenceWall();
            var aim = new EdgeAim();
            Assert.AreEqual(NextBottom, aim.Update(grid, new Vector2(-1.7f, -1.95f)));

            // On the corner itself the turn is a hair nearer — not enough to leave the held edge.
            Assert.AreEqual(NextBottom, aim.Update(grid, new Vector2(-1.98f, -1.9f)));
        }

        [Test]
        public void Update_IsNotPulled_ByAJointInTheMiddleOfAWall()
        {
            var grid = Island();
            for (int x = -4; x <= -1; x++) PutFence(grid, new Edge(new Vector2Int(x, -2), true));
            // A wall from (-4,-2) to (0,-2): (-2,-2) is a joint, and both ends are out of reach.

            Assert.AreEqual(NextBottom, new EdgeAim().Update(grid, new Vector2(-1.9f, -1.95f)));
        }

        [Test]
        public void Update_IsNotPulled_FromFurtherThanTheReach()
        {
            // 1.49 from the end (-2,-2), beyond its 1.25 reach: the plain nearest side, the left one of
            // cell (-1,-2) — the same edge FarRight names.
            Assert.AreEqual(FarRight, new EdgeAim().Update(OneFenceWall(), new Vector2(-0.6f, -1.5f)));
        }

        [Test]
        public void Update_GoesToTheNearerOfTwoEnds()
        {
            var grid = OneFenceWall();
            PutFence(grid, new Edge(new Vector2Int(0, -2), true));   // a second wall, two cells on: its west end at (0,-2)

            // In the gap, nearer the second wall's end: the ghost reaches back from it to close the gap.
            Assert.AreEqual(new Edge(new Vector2Int(-1, -2), true), new EdgeAim().Update(grid, new Vector2(-0.6f, -1.9f)));
        }
    }
}
