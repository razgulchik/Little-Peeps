using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LittlePeeps.Tests
{
    // The card behaves as a button, and its three visuals are independent: the frame is HOVER, the scale
    // is PRESS, the fill is HOLD. These tests pin that separation, because collapsing any two of them
    // back together is a one-line mistake that looks harmless in a diff.
    //
    // What matters most is that holdDuration 0 confirms on RELEASE rather than on press. That is the
    // whole reason a zero setting is safe to offer: it degrades the card into an ordinary click, keeping
    // the slide-off-to-cancel escape the hold exists to provide, instead of turning it into a hair
    // trigger.
    //
    // NOT COVERED: anything that has to tick. The hold timer advances in Update and the press response
    // is a LitMotion tween, and Edit Mode drives neither — the same rule that shapes RunTeardownTests.
    // The press tests get at the logic anyway by running with zero durations, where ScaleTo snaps
    // instead of tweening; what stays untested is the interpolation itself, which is the library's job.
    public class PerkCardUITests
    {
        private GameObject go;
        private PerkCardUI card;
        private PerkDef perk;
        private GameObject highlight;
        private RectTransform scaleTarget;
        private int confirmCount;
        private PerkDef confirmedWith;

        // Far from 1 so an assertion cannot pass on a value that merely drifted.
        private const float PressedScale = 0.5f;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("PerkCard", typeof(RectTransform));
            card = go.AddComponent<PerkCardUI>();

            perk = ScriptableObject.CreateInstance<StatPerkDef>();
            perk.id = "test_perk";

            confirmCount = 0;
            confirmedWith = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            if (perk != null) Object.DestroyImmediate(perk);
        }

        // Sets the private [SerializeField]s the way the inspector does. SerializedObject rather than
        // reflection: it goes through Unity's own serialisation, so renaming a field fails here loudly
        // instead of quietly leaving the card on its defaults and the tests passing on nothing.
        //
        // The durations are zero so ScaleTo snaps: Edit Mode never ticks a tween, so a real duration
        // would leave the scale at its starting value forever and the assertions would test nothing.
        // scaleTarget is assigned explicitly rather than left to the Awake fallback, for the same reason
        // Warmup is called by hand in RunTeardownTests.
        private void Arrange(float holdDuration)
        {
            // Real child objects rather than stand-ins: the card toggles one with SetActive and writes
            // localScale on the other, and the tests read both straight back.
            highlight = new GameObject("HoverFrame");
            highlight.transform.SetParent(go.transform);

            scaleTarget = new GameObject("Visual", typeof(RectTransform)).GetComponent<RectTransform>();
            scaleTarget.SetParent(go.transform);

            var so = new SerializedObject(card);
            so.FindProperty("holdDuration").floatValue = holdDuration;
            so.FindProperty("hoverHighlight").objectReferenceValue = highlight;
            so.FindProperty("scaleTarget").objectReferenceValue = scaleTarget;
            so.FindProperty("pressedScale").floatValue = PressedScale;
            so.FindProperty("pressDuration").floatValue = 0f;
            so.FindProperty("releaseDuration").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            card.Init(perk, p => { confirmCount++; confirmedWith = p; });
        }

        private static PointerEventData Pointer() => new PointerEventData(EventSystem.current);

        private float Scale => scaleTarget.localScale.x;

        [Test]
        public void WithNoHoldTime_AReleaseConfirmsTheCard()
        {
            Arrange(holdDuration: 0f);

            card.OnPointerDown(Pointer());
            Assert.That(confirmCount, Is.Zero, "Pressing alone must not confirm — that is the hair trigger.");

            card.OnPointerUp(Pointer());
            Assert.That(confirmCount, Is.EqualTo(1));
            Assert.That(confirmedWith, Is.SameAs(perk));
        }

        [Test]
        public void WithNoHoldTime_SlidingOffBeforeReleaseCancels()
        {
            Arrange(holdDuration: 0f);

            card.OnPointerDown(Pointer());
            card.OnPointerExit(Pointer());
            card.OnPointerUp(Pointer());

            Assert.That(confirmCount, Is.Zero);
        }

        // The mirror of the first test: with a real hold time, letting go early is a cancel, not a pick.
        // Confirming here would make every hold duration behave like a click.
        [Test]
        public void WithAHoldTime_ReleasingEarlyConfirmsNothing()
        {
            Arrange(holdDuration: 1f);

            card.OnPointerDown(Pointer());
            card.OnPointerUp(Pointer());

            Assert.That(confirmCount, Is.Zero);
        }

        // Locked the moment a sibling card is confirmed, while the panel is still on screen waiting for
        // the state to hide it.
        [Test]
        public void ALockedCardIgnoresThePointerEntirely()
        {
            Arrange(holdDuration: 0f);
            card.SetInteractable(false);

            card.OnPointerDown(Pointer());
            card.OnPointerUp(Pointer());

            Assert.That(confirmCount, Is.Zero);
        }

        [Test]
        public void ConfirmingTwiceIsNotPossibleFromOnePress()
        {
            Arrange(holdDuration: 0f);

            card.OnPointerDown(Pointer());
            card.OnPointerUp(Pointer());
            card.OnPointerUp(Pointer());

            Assert.That(confirmCount, Is.EqualTo(1));
        }

        // --- hover frame ---
        //
        // The frame belongs to HOVER and to nothing else. It used to be raised in OnPointerDown, which
        // made the card look dead until pressed and made the frame read as "chosen" rather than "under
        // the cursor".

        [Test]
        public void ANewCardStartsWithoutAFrame()
        {
            Arrange(holdDuration: 1f);

            Assert.That(highlight.activeSelf, Is.False);
        }

        [Test]
        public void HoveringShowsTheFrame()
        {
            Arrange(holdDuration: 1f);

            card.OnPointerEnter(Pointer());

            Assert.That(highlight.activeSelf, Is.True);
        }

        [Test]
        public void LeavingHidesTheFrame()
        {
            Arrange(holdDuration: 1f);

            card.OnPointerEnter(Pointer());
            card.OnPointerExit(Pointer());

            Assert.That(highlight.activeSelf, Is.False);
        }

        // A press with no hover before it must not conjure the frame.
        [Test]
        public void PressingAloneDoesNotRaiseTheFrame()
        {
            Arrange(holdDuration: 1f);

            card.OnPointerDown(Pointer());

            Assert.That(highlight.activeSelf, Is.False);
        }

        [Test]
        public void ALockedCardDoesNotHighlightOnHover()
        {
            Arrange(holdDuration: 1f);
            card.SetInteractable(false);

            card.OnPointerEnter(Pointer());

            Assert.That(highlight.activeSelf, Is.False);
        }

        // A sibling card was confirmed while the cursor sat on this one: it must stop looking pickable
        // immediately, not when the panel finally hides.
        [Test]
        public void LockingACardDropsTheFrameItWasShowing()
        {
            Arrange(holdDuration: 1f);
            card.OnPointerEnter(Pointer());

            card.SetInteractable(false);

            Assert.That(highlight.activeSelf, Is.False);
        }

        // --- press response ---
        //
        // The scale belongs to the PRESS. Every path back out of a press has to restore it, because a
        // card left shrunken is a card that looks permanently disabled.

        [Test]
        public void ANewCardStartsAtRestingScale()
        {
            Arrange(holdDuration: 1f);

            Assert.That(Scale, Is.EqualTo(1f));
        }

        [Test]
        public void PressingSinksTheCard()
        {
            Arrange(holdDuration: 1f);

            card.OnPointerDown(Pointer());

            Assert.That(Scale, Is.EqualTo(PressedScale));
        }

        [Test]
        public void ReleasingLetsTheCardBackUp()
        {
            Arrange(holdDuration: 1f);

            card.OnPointerDown(Pointer());
            card.OnPointerUp(Pointer());

            Assert.That(Scale, Is.EqualTo(1f));
        }

        // Without this the card stays visibly pressed forever: the pointer is gone, so no release will
        // ever arrive to put it back.
        [Test]
        public void SlidingOffWhileHeldLetsTheCardBackUp()
        {
            Arrange(holdDuration: 1f);

            card.OnPointerDown(Pointer());
            card.OnPointerExit(Pointer());

            Assert.That(Scale, Is.EqualTo(1f));
        }

        [Test]
        public void ALockedCardDoesNotSinkOnPress()
        {
            Arrange(holdDuration: 1f);
            card.SetInteractable(false);

            card.OnPointerDown(Pointer());

            Assert.That(Scale, Is.EqualTo(1f));
        }

        // Another card won the race while this one was held down.
        [Test]
        public void LockingAPressedCardLetsItBackUp()
        {
            Arrange(holdDuration: 1f);
            card.OnPointerDown(Pointer());

            card.SetInteractable(false);

            Assert.That(Scale, Is.EqualTo(1f));
        }
    }
}
