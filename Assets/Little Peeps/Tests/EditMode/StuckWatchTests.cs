using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // StuckWatch says a unit is stuck when a whole window passes without it getting further than the
    // radius from where the window began. The cases that matter: free travel never trips it however
    // many walls the unit hits, confinement does, the verdict lands within one to two windows of the
    // unit stopping, it clears the moment the unit is out of the disc, and a placement resets it.
    public class StuckWatchTests
    {
        private const float Radius = 1f;
        private const float Window = 5f;
        private const float Dt = 0.02f;
        private const float Margin = 0.1f;   // a few steps clear of the window edge, so float accumulation never decides a case

        private StuckWatch watch;

        [SetUp]
        public void SetUp() => watch = default;

        // Feed `seconds` of steps from `at(t)`; returns the verdict after the last step.
        private bool Run(float seconds, System.Func<float, Vector2> at)
        {
            bool stuck = false;
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 1; i <= steps; i++)
                stuck = watch.Tick(at(i * Dt), Dt, Radius, Window);
            return stuck;
        }

        private static Vector2 Straight(float t) => new Vector2(2f * t, 0f);                     // 2 u/s, never turns
        private static Vector2 Pinned(float t) => new Vector2(Mathf.Sin(t * 40f) * 0.05f, 0f);   // jittering in place

        // Bouncing back and forth across a 3-unit corridor: leaves the disc on every crossing.
        private static Vector2 Corridor(float t) => new Vector2(Mathf.PingPong(2f * t, 3f), 0f);

        [Test]
        public void FreeTravel_NeverStuck()
        {
            Assert.IsFalse(Run(3f * Window, Straight));
        }

        [Test]
        public void Bouncing_NeverStuck_WhileItGetsSomewhere()
        {
            Assert.IsFalse(Run(3f * Window, Corridor));
        }

        [Test]
        public void Pinned_Stuck_AfterOneWindow()
        {
            Assert.IsTrue(Run(Window + Margin, Pinned));
        }

        [Test]
        public void NoVerdict_BeforeTheFirstWindowCompletes()
        {
            Assert.IsFalse(Run(Window - Margin, Pinned));
        }

        // Stuck from mid-window: that window still holds the free travel and passes; the next fails.
        [Test]
        public void StuckMidWindow_VerdictLandsWithinTwoWindows()
        {
            Assert.IsFalse(Run(Window / 2f, Straight));
            Vector2 stop = Straight(Window / 2f);

            Assert.IsFalse(Run(Window / 2f + Margin, t => stop), "first window still holds free travel");
            Assert.IsTrue(Run(Window, t => stop), "second window is all standstill");
        }

        [Test]
        public void Verdict_Holds_AcrossWindows_WhileStillPinned()
        {
            Assert.IsTrue(Run(Window + Margin, Pinned));
            Assert.IsTrue(Run(2f * Window, Pinned));
        }

        [Test]
        public void Verdict_Clears_TheMomentTheUnitIsOutOfTheDisc()
        {
            Assert.IsTrue(Run(Window + Margin, Pinned));

            // One step well outside the radius — a shove — is enough; no need to wait for the window.
            Assert.IsFalse(watch.Tick(new Vector2(Radius * 3f, 0f), Dt, Radius, Window));
        }

        [Test]
        public void Reset_ClearsTheVerdict_AndStartsAFreshWindow()
        {
            Assert.IsTrue(Run(Window + Margin, Pinned));

            watch.Reset(new Vector2(100f, 100f));
            Assert.IsFalse(watch.IsStuck);

            // The reset position is the new anchor: standing there is a fresh window, not a jump.
            Assert.IsFalse(Run(Window - Margin, t => new Vector2(100f, 100f)));
            Assert.IsTrue(Run(2f * Margin, t => new Vector2(100f, 100f)));
        }

        // A launch moves the unit across the map and resets the watch: the jump must not read as
        // travel, i.e. a reset followed by standing still is stuck after one window, not two.
        [Test]
        public void Reset_MakesThePlacementJump_NotCountAsTravel()
        {
            Assert.IsFalse(Run(Window / 2f, Straight));

            watch.Reset(new Vector2(-50f, 0f));
            Assert.IsTrue(Run(Window + Margin, t => new Vector2(-50f, 0f)));
        }
    }
}
