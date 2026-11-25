using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // Telling a tap from a hold. What is pinned here are the ways it could go wrong in the player's hand: a click
    // made on the move, a slow click, a drag set off at once, a hitch in the middle of a click.
    public class PressGestureTests
    {
        private const float Frame = 1f / 60f;

        private PressGesture press;
        private PressTiming timing;

        [SetUp]
        public void SetUp()
        {
            press = new PressGesture();
            timing = new PressTiming { tapUnder = 0.15f, holdFrom = 0.3f, dragPixels = 24f };
        }

        [Test]
        public void Pressed_IsUndecided()
        {
            press.Begin(Vector2.zero);

            Assert.That(press.IsDown, Is.True);
            Assert.That(press.IsUndecided, Is.True);
            Assert.That(press.IsHold, Is.False);
        }

        [Test]
        public void AClick_IsNeverAHold()
        {
            press.Begin(Vector2.zero);
            Hold(0.1f, Vector2.zero);

            Assert.That(press.IsUndecided, Is.True, "let go now, it is a tap");
        }

        // Clicking at a running unit: the cursor crosses half the screen meanwhile and it is a click all the same.
        [Test]
        public void AClickOnTheMove_IsNeverAHold()
        {
            press.Begin(Vector2.zero);
            Hold(0.13f, new Vector2(500f, 0f));

            Assert.That(press.IsUndecided, Is.True);
        }

        [Test]
        public void ASlowClick_StandingStill_IsNotAHoldYet()
        {
            press.Begin(Vector2.zero);
            Hold(0.25f, new Vector2(5f, 3f));   // a hand is never perfectly still

            Assert.That(press.IsUndecided, Is.True);
        }

        [Test]
        public void HeldLongEnough_IsAHold_WithoutMoving()
        {
            press.Begin(Vector2.zero);
            Hold(0.35f, Vector2.zero);

            Assert.That(press.IsHold, Is.True);
        }

        [Test]
        public void Dragged_IsAHold_AsSoonAsItIsNoClick()
        {
            press.Begin(Vector2.zero);
            Hold(0.18f, new Vector2(40f, 0f));

            Assert.That(press.IsHold, Is.True, "no need to wait for the standing-still time");
        }

        // Out and back counts as a drag: the cursor took something somewhere and brought it back.
        [Test]
        public void TheFarthestTravelCounts_NotWhereTheCursorIsNow()
        {
            press.Begin(Vector2.zero);
            Hold(0.1f, new Vector2(40f, 0f));
            Hold(0.1f, Vector2.zero);

            Assert.That(press.IsHold, Is.True);
        }

        [Test]
        public void Advance_IsTrueOnce_OnTheFrameItTurnsIntoAHold()
        {
            press.Begin(Vector2.zero);

            int turned = 0;
            for (int i = 0; i < 60; i++)
                if (press.Advance(timing, Frame, Vector2.zero)) turned++;

            Assert.That(turned, Is.EqualTo(1));
            Assert.That(press.IsHold, Is.True, "for good, until the button is up");
        }

        // The game stuttered during a click: the button may be up by now, and that is told only after this frame.
        [Test]
        public void AHitch_DoesNotTurnAClickIntoAHold()
        {
            press.Begin(Vector2.zero);
            press.Advance(timing, 1f, Vector2.zero);

            Assert.That(press.IsUndecided, Is.True);
        }

        [Test]
        public void Ended_AsksNothingMore()
        {
            press.Begin(Vector2.zero);
            press.End();

            Assert.That(press.IsDown, Is.False);
            Assert.That(press.Advance(timing, 1f, new Vector2(500f, 0f)), Is.False);
            Assert.That(press.IsHold, Is.False);
        }

        // Nothing of the press before: neither its time nor how far the cursor went.
        [Test]
        public void ANewPress_StartsOver()
        {
            press.Begin(Vector2.zero);
            Hold(0.4f, new Vector2(300f, 0f));
            press.End();

            press.Begin(new Vector2(300f, 0f));
            Assert.That(press.IsUndecided, Is.True);

            Hold(0.2f, new Vector2(305f, 0f));
            Assert.That(press.IsUndecided, Is.True);
        }

        // Frame by frame, the cursor already at `screen`.
        private void Hold(float seconds, Vector2 screen)
        {
            for (float t = 0f; t < seconds; t += Frame) press.Advance(timing, Frame, screen);
        }
    }
}
