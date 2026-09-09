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
            var so = new SerializedObject(card);
            so.FindProperty("holdDuration").floatValue = holdDuration;
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
