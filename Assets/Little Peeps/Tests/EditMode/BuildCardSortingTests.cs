using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps.Tests
{
    public class BuildCardSortingTests
    {
        private GameObject canvas;
        private RectMask2D viewport;
        private BuildCardRow row;
        private StructureDef definition;
        private BuildCardUI first, second, third;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            viewport = Child("Viewport", canvas.transform).gameObject.AddComponent<RectMask2D>();
            row = Child("Row", viewport.transform).gameObject.AddComponent<BuildCardRow>();
            ((RectTransform)row.transform).sizeDelta = new Vector2(472f, 92f);
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.spacing = 3f;
            definition = ScriptableObject.CreateInstance<StructureDef>();
            first = Card("House");
            second = Card("Mill");
            third = Card("Market");
            Rebuild();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void HoveredCardDrawsAboveLockedNeighboursWithoutMovingItsSlot()
        {
            second.SetLocked(StructureLock.Age);
            third.SetLocked(StructureLock.Age);
            Vector2[] positions = Positions();
            float width = row.preferredWidth;

            Hover(first, true);
            Rebuild();

            Assert.That(first.transform.GetSiblingIndex(), Is.EqualTo(2));
            Assert.That(Positions(), Is.EqualTo(positions));
            Assert.That(row.preferredWidth, Is.EqualTo(width));

            Hover(first, false);
            Rebuild();
            Assert.That(first.transform.GetSiblingIndex(), Is.Zero);
            Assert.That(Positions(), Is.EqualTo(positions));
        }

        [Test]
        public void HoverWinsOverOpenAndReturningCards()
        {
            first.SetSelected(true);
            Hover(second, true);
            Assert.That(second.transform.GetSiblingIndex(), Is.GreaterThan(first.transform.GetSiblingIndex()));

            // Sample an outgoing tween: Edit Mode does not advance LitMotion's player loop.
            SetField(second, "pointerInside", false);
            SetField(second, "hoverProgress", 0.5f);
            Invoke(second, "ApplyVisualState");
            Assert.That(first.transform.GetSiblingIndex(), Is.EqualTo(2));
            Assert.That(second.transform.GetSiblingIndex(), Is.GreaterThan(third.transform.GetSiblingIndex()));

            second.ResetInteractionVisuals();
            first.SetSelected(false);
            Assert.That(first.transform.GetSiblingIndex(), Is.Zero);
            Assert.That(second.transform.GetSiblingIndex(), Is.EqualTo(1));
            Assert.That(third.transform.GetSiblingIndex(), Is.EqualTo(2));
        }

        [Test]
        public void RaisedCardStillReservesItsExpandedWidthAtItsOriginalPosition()
        {
            first.SetSelected(true);
            RectTransform rect = (RectTransform)first.transform;
            rect.sizeDelta = new Vector2(138f, 70f);
            Rebuild();

            Assert.That(LeftEdge(first), Is.Zero.Within(0.001f));
            Assert.That(LeftEdge(second), Is.EqualTo(141f).Within(0.001f));
            Assert.That(LeftEdge(third), Is.EqualTo(198f).Within(0.001f));
            Assert.That(row.preferredWidth, Is.EqualTo(252f));
        }

        [Test]
        public void SwitchingTabsAndAddingCardsPreservesLayoutAndDrawOrder()
        {
            Vector2[] positions = Positions();
            Hover(first, true);
            BuildCardUI fourth = Card("New tab");
            Rebuild();
            Assert.That(first.transform.GetSiblingIndex(), Is.EqualTo(3));
            Assert.That(LeftEdge(fourth), Is.EqualTo(171f).Within(0.001f));

            foreach (BuildCardUI card in new[] { first, second, third })
            {
                card.gameObject.SetActive(false);
                Invoke(card, "OnDisable");
            }
            Rebuild();
            Assert.That(LeftEdge(fourth), Is.Zero.Within(0.001f));

            fourth.gameObject.SetActive(false);
            foreach (BuildCardUI card in new[] { first, second, third })
            {
                card.ResetInteractionVisuals();
                card.gameObject.SetActive(true);
                Invoke(card, "OnEnable");
            }
            Rebuild();
            Assert.That(Positions(), Is.EqualTo(positions));
            Assert.That(first.transform.GetSiblingIndex(), Is.Zero);
        }

        [Test]
        public void RaisedCardAndLockedOverlayStillUseTheViewportMask()
        {
            Image overlay = Child("LockedOverlay", second.transform).gameObject.AddComponent<Image>();
            second.SetLocked(StructureLock.Age);
            Hover(first, true);
            Canvas.ForceUpdateCanvases();

            Assert.That(MaskUtilities.GetRectMaskForClippable(first.GetComponent<Image>()), Is.SameAs(viewport));
            Assert.That(MaskUtilities.GetRectMaskForClippable(overlay), Is.SameAs(viewport));
            Assert.That(overlay.transform.IsChildOf(second.transform), Is.True);
            Assert.That(first.GetComponentInParent<Canvas>(), Is.SameAs(canvas.GetComponent<Canvas>()));
        }

        [Test]
        public void PalettePrefabUsesTheRowThatSeparatesLayoutFromDrawing()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Little Peeps/Prefabs/Elements/Canvas.prefab");
            BuildPaletteUI palette = prefab.GetComponentInChildren<BuildPaletteUI>(true);
            var serialized = new SerializedObject(palette);
            var container = (Transform)serialized.FindProperty("cardContainer").objectReferenceValue;
            Assert.That(container.GetComponent<BuildCardRow>(), Is.Not.Null);
        }

        private BuildCardUI Card(string name)
        {
            RectTransform rect = Child(name, row.transform);
            rect.sizeDelta = new Vector2(54f, 70f);
            rect.gameObject.AddComponent<Image>();
            BuildCardUI card = rect.gameObject.AddComponent<BuildCardUI>();
            // No motion profile: hover/selection snap, so these tests exercise draw order without time.
            card.Init(definition, null, null);
            return card;
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private void Rebuild() => LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)row.transform);
        private Vector2[] Positions() => new[] { ((RectTransform)first.transform).anchoredPosition,
            ((RectTransform)second.transform).anchoredPosition, ((RectTransform)third.transform).anchoredPosition };
        private static float LeftEdge(BuildCardUI card)
        {
            var rect = (RectTransform)card.transform;
            return rect.anchoredPosition.x - rect.rect.width * rect.pivot.x;
        }

        // Simulate the hover decision made by Update, without depending on the desktop mouse position.
        private static void Hover(BuildCardUI card, bool value)
        {
            SetField(card, "pointerInside", value);
            typeof(BuildCardUI).GetMethod("SetHovered", PrivateInstance).Invoke(card, new object[] { value });
        }

        private static void SetField(BuildCardUI card, string name, object value) =>
            typeof(BuildCardUI).GetField(name, PrivateInstance).SetValue(card, value);
        private static void Invoke(BuildCardUI card, string name) =>
            typeof(BuildCardUI).GetMethod(name, PrivateInstance).Invoke(card, null);
    }
}
