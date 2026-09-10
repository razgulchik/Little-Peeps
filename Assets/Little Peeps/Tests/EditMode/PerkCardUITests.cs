using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LittlePeeps.Tests
{
    // The hold-to-confirm rules, which are the part of the card that is easy to get subtly wrong: the
    // difference between "confirms" and "confirms twice" or "never confirms" is one branch, and all
    // three look identical in a diff.
    //
    // What matters most here is that holdDuration 0 confirms on RELEASE rather than on press. That is
    // the whole reason a zero setting is safe to offer: it degrades the card into an ordinary click,
    // keeping the slide-off-to-cancel escape that the hold exists to provide, instead of turning it
    // into a hair trigger.
    //
    // NOT COVERED: the timer itself running to completion. It advances in Update, and Edit Mode never
    // invokes MonoBehaviour lifecycle callbacks — the same rule that shapes RunTeardownTests. A test for
    // it could only assert that Unity failed to call Update. It belongs in a PlayMode assembly.
    public class PerkCardUITests
    {
        private GameObject go;
        private PerkCardUI card;
        private PerkDef perk;
        private GameObject highlight;
        private int confirmCount;
        private PerkDef confirmedWith;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("PerkCard");
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

        // Sets the private [SerializeField] the way the inspector does. SerializedObject rather than
        // reflection: it goes through Unity's own serialisation, so renaming the field fails here loudly
        // instead of quietly leaving the card on its default and the tests passing on nothing.
        private void Arrange(float holdDuration)
        {
            // A real child object rather than a stand-in: the card toggles it with SetActive and the
            // tests read activeSelf straight back, so nothing about the frame is faked.
            highlight = new GameObject("HoverFrame");
            highlight.transform.SetParent(go.transform);

            var so = new SerializedObject(card);
            so.FindProperty("holdDuration").floatValue = holdDuration;
            so.FindProperty("hoverHighlight").objectReferenceValue = highlight;
            so.ApplyModifiedPropertiesWithoutUndo();

            card.Init(perk, p => { confirmCount++; confirmedWith = p; });
        }

        private static PointerEventData Pointer() => new PointerEventData(EventSystem.current);

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

        // --- hover frame ---
        //
        // The frame belongs to HOVER and to nothing else. It used to be raised in OnPointerDown, which
        // made the card look dead until pressed and made the frame read as "chosen" rather than "under
        // the cursor". These pin the split so it cannot quietly collapse back into the press.

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

        // The actual bug this stage fixes: a press with no hover before it must not conjure the frame.
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

        [Test]
        public void ConfirmingTwiceIsNotPossibleFromOnePress()
        {
            Arrange(holdDuration: 0f);

            card.OnPointerDown(Pointer());
            card.OnPointerUp(Pointer());
            card.OnPointerUp(Pointer());

            Assert.That(confirmCount, Is.EqualTo(1));
        }
    }
}
