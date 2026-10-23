using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LittlePeeps.Tests
{
    // The hold is the whole point of the button: it guards an answer that cannot be taken back. So the tests
    // pin what makes it safe — a press alone never answers, sliding off cancels, an early release with a real
    // hold time cancels, a locked button ignores the pointer — and that the fill is back at nothing at rest.
    //
    // NOT COVERED: the timer itself. It advances in Update, which Edit Mode never runs — PerkCardUITests'
    // rule. Hold time 0 (release confirms) gets at the rest.
    public class HoldButtonTests
    {
        private GameObject go;
        private HoldButton button;
        private RectTransform fill;
        private int confirmed;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("WrathButton", typeof(RectTransform));
            button = go.AddComponent<HoldButton>();
            button.Confirmed += () => confirmed++;
            confirmed = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        private void Arrange(float holdDuration, bool withFill = false)
        {
            var so = new SerializedObject(button);
            so.FindProperty("holdDuration").floatValue = holdDuration;

            if (withFill)
            {
                fill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
                fill.SetParent(go.transform);
                fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0f, 1f);
                fill.sizeDelta = new Vector2(75f, 29f);
                so.FindProperty("fill").objectReferenceValue = fill;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PointerEventData Pointer() => new PointerEventData(EventSystem.current);

        [Test]
        public void WithNoHoldTime_AReleaseConfirms()
        {
            Arrange(holdDuration: 0f);

            button.OnPointerDown(Pointer());
            Assert.That(confirmed, Is.Zero, "Pressing alone must not confirm — that is the hair trigger.");

            button.OnPointerUp(Pointer());
            Assert.That(confirmed, Is.EqualTo(1));
        }

        [Test]
        public void SlidingOffBeforeReleaseCancels()
        {
            Arrange(holdDuration: 0f);

            button.OnPointerDown(Pointer());
            button.OnPointerExit(Pointer());
            button.OnPointerUp(Pointer());

            Assert.That(confirmed, Is.Zero);
        }

        [Test]
        public void WithAHoldTime_ReleasingEarlyConfirmsNothing()
        {
            Arrange(holdDuration: 1f);

            button.OnPointerDown(Pointer());
            button.OnPointerUp(Pointer());

            Assert.That(confirmed, Is.Zero);
        }

        [Test]
        public void ALockedButtonIgnoresThePointer()
        {
            Arrange(holdDuration: 0f);
            button.Interactable = false;

            button.OnPointerDown(Pointer());
            button.OnPointerUp(Pointer());

            Assert.That(confirmed, Is.Zero);
        }

        // The authored fill is the FULL fill; at rest it is drawn as nothing.
        [Test]
        public void AtRest_TheFillIsEmpty()
        {
            Arrange(holdDuration: 1f, withFill: true);

            button.OnPointerDown(Pointer());
            button.OnPointerUp(Pointer());

            Assert.That(fill.rect.width, Is.EqualTo(0f));
        }
    }
}
