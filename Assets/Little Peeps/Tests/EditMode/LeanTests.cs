using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // LeanView's spring as pure numbers: a kick bends the top exactly one full bend at its farthest (KickFor), the
    // spring swings past rest when underdamped and settles back (Step), and the bend reads as the right pose —
    // a top dragged right turns clockwise, up stretches, down squats (Pose).
    public class LeanTests
    {
        private const float Dt = 0.001f;

        // The farthest the top gets after a kick from rest, and where it is after `seconds`.
        private static (float peak, float lowest, float end) Swing(float frequency, float damping, float seconds)
        {
            var bend = Vector2.zero;
            var swing = new Vector2(LeanView.KickFor(frequency, damping), 0f);
            float peak = 0f, lowest = 0f;
            for (float t = 0f; t < seconds; t += Dt)
            {
                LeanView.Step(ref bend, ref swing, 2f * Mathf.PI * frequency, damping, Dt);
                peak = Mathf.Max(peak, bend.x);
                lowest = Mathf.Min(lowest, bend.x);
            }
            return (peak, lowest, bend.x);
        }

        [TestCase(3f, 0.3f)]
        [TestCase(2f, 0.6f)]
        [TestCase(5f, 1f)]
        public void AKick_BendsOneFullBendAtItsFarthest(float frequency, float damping)
        {
            Assert.AreEqual(1f, Swing(frequency, damping, 2f).peak, 0.01f);
        }

        // Solved exactly per step, so frames of any length land on the same curve.
        [Test]
        public void TheSwing_IsTheSameAtAnyFrameRate()
        {
            Vector2 fine = Vector2.zero, fineSwing = new(LeanView.KickFor(3f, 0.3f), 0f);
            Vector2 coarse = fine, coarseSwing = fineSwing;
            float omega = 2f * Mathf.PI * 3f;

            for (int i = 0; i < 100; i++) LeanView.Step(ref fine, ref fineSwing, omega, 0.3f, 0.0025f);   // 400 fps
            for (int i = 0; i < 5; i++) LeanView.Step(ref coarse, ref coarseSwing, omega, 0.3f, 0.05f);   // 20 fps

            Assert.AreEqual(fine.x, coarse.x, 1e-4f);
        }

        [Test]
        public void Underdamped_ItSwingsPastRest_AndSettles()
        {
            var (_, lowest, end) = Swing(3f, 0.3f, 4f);

            Assert.That(lowest, Is.LessThan(-0.1f), "swings back past straight");
            Assert.AreEqual(0f, end, 0.01f, "and comes to rest");
        }

        [Test]
        public void Pose_ATopDraggedRight_TurnsClockwise()
        {
            var (tilt, height) = LeanView.Pose(new Vector2(1f, 0f), 8f, 0.06f, false);

            Assert.AreEqual(-8f, tilt, 1e-5f);
            Assert.AreEqual(1f, height, 1e-5f);
        }

        [Test]
        public void Pose_UpTheScreenStretches_DownSquats()
        {
            Assert.AreEqual(1.06f, LeanView.Pose(new Vector2(0f, 1f), 8f, 0.06f, false).height, 1e-5f);
            Assert.AreEqual(0.94f, LeanView.Pose(new Vector2(0f, -1f), 8f, 0.06f, false).height, 1e-5f);
        }

        // Past the middle = moving away from it, whichever side the unit came from; standing still, never.
        [TestCase(-0.5f, 0f, 3f, 0f, false)]
        [TestCase(0.1f, 0f, 3f, 0f, true)]
        [TestCase(0.1f, 0f, -3f, 0f, false)]
        [TestCase(0.3f, -0.4f, 0f, -3f, true)]
        [TestCase(0.3f, 0.2f, 0f, 0f, false)]
        public void IsPast_MeansMovingAwayFromTheMiddle(float x, float y, float vx, float vy, bool past)
        {
            Assert.AreEqual(past, LeanView.IsPast(new Vector2(x, y), new Vector2(vx, vy), Vector2.zero));
        }

        [Test]
        public void Pose_SquatOnly_SquatsBothWays()
        {
            Assert.AreEqual(0.94f, LeanView.Pose(new Vector2(0f, 1f), 8f, 0.06f, true).height, 1e-5f);
            Assert.AreEqual(0.94f, LeanView.Pose(new Vector2(0f, -1f), 8f, 0.06f, true).height, 1e-5f);
        }
    }
}
