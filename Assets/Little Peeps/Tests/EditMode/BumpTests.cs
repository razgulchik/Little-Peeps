using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The bump wave's direction: which face a bounce landed on (CollisionTarget.FaceOf) and how far the
    // top-face art turns for it (BumpVfxSystem.TurnFor). And BumpView's two rules that are pure numbers: the
    // throttle never drops a stronger verdict for a weaker one (ShouldShow), and a weaker verdict's squash is
    // the same curve with less of it (Height).
    public class BumpTests
    {
        [Test]
        public void Throttle_OnceTheIntervalIsOver_AnythingShows()
        {
            Assert.IsTrue(BumpView.ShouldShow(HitVerdict.Useless, HitVerdict.Useful, 0.15f, 0.15f));
        }

        [Test]
        public void Throttle_InsideTheInterval_AStrongerVerdictStillShows()
        {
            Assert.IsTrue(BumpView.ShouldShow(HitVerdict.Useful, HitVerdict.Useless, 0f, 0.15f));
            Assert.IsTrue(BumpView.ShouldShow(HitVerdict.Blocked, HitVerdict.Useless, 0f, 0.15f));
            Assert.IsTrue(BumpView.ShouldShow(HitVerdict.Useful, HitVerdict.Blocked, 0f, 0.15f));
        }

        [Test]
        public void Throttle_InsideTheInterval_TheSameOrAWeakerVerdictIsSkipped()
        {
            Assert.IsFalse(BumpView.ShouldShow(HitVerdict.Useful, HitVerdict.Useful, 0.1f, 0.15f));
            Assert.IsFalse(BumpView.ShouldShow(HitVerdict.Useless, HitVerdict.Blocked, 0.1f, 0.15f));
        }

        [Test]
        public void Height_KeepsOnlyTheShareOfTheDepartureFromRest()
        {
            Assert.AreEqual(0.94f, BumpView.Height(0.94f, 1f), 1e-5f, "full: as authored");
            Assert.AreEqual(0.97f, BumpView.Height(0.94f, 0.5f), 1e-5f, "half the dip");
            Assert.AreEqual(1.015f, BumpView.Height(1.03f, 0.5f), 1e-5f, "half the overshoot");
            Assert.AreEqual(1f, BumpView.Height(0.94f, 0f), 1e-5f, "none: rest");
        }

        [Test]
        public void Face_TheNormalPointingInward_IsTurnedOut()
        {
            // A hit on the right face, with the normal reported into the building.
            Assert.AreEqual(new Vector2Int(1, 0), CollisionTarget.FaceOf(Vector2.left, new Vector2(0.4f, 0.05f)));
        }

        [Test]
        public void Face_TheNormalAlreadyOut_IsKept()
        {
            Assert.AreEqual(new Vector2Int(0, -1), CollisionTarget.FaceOf(Vector2.down, new Vector2(0.02f, -0.4f)));
        }

        [Test]
        public void Face_ACornerHit_GoesToTheLargerAxis()
        {
            Assert.AreEqual(new Vector2Int(0, 1), CollisionTarget.FaceOf(new Vector2(-0.6f, 0.8f), new Vector2(-0.6f, 0.8f)));
            Assert.AreEqual(new Vector2Int(-1, 0), CollisionTarget.FaceOf(new Vector2(-0.8f, 0.6f), new Vector2(-0.8f, 0.6f)));
        }

        [Test]
        public void Turn_TheArtIsDrawnForATopFace()
        {
            Assert.AreEqual(0f, BumpVfxSystem.TurnFor(new Vector2Int(0, 1)));
            Assert.AreEqual(180f, BumpVfxSystem.TurnFor(new Vector2Int(0, -1)));
        }

        [Test]
        public void Turn_SideFaces_GoClockwiseForTheRight()
        {
            Assert.AreEqual(90f, BumpVfxSystem.TurnFor(new Vector2Int(1, 0)));
            Assert.AreEqual(-90f, BumpVfxSystem.TurnFor(new Vector2Int(-1, 0)));
        }
    }
}
