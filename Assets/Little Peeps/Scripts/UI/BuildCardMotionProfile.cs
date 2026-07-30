using UnityEngine;
using UnityEngine.Serialization;

namespace LittlePeeps
{
    // Shared, designer-authored motion and geometry for every build card. Keeping the complete hover
    // treatment in one asset makes the "flight" easy to tune in the Inspector without touching code or
    // editing every card instance separately.
    [CreateAssetMenu(menuName = "LittlePeeps/UI/Build Card Motion Profile")]
    public class BuildCardMotionProfile : ScriptableObject
    {
        [Header("Card geometry (reference pixels)")]
        public Vector2 restingCardSize = new(57f, 76f);
        public Vector2 hoveredCardSize = new(69f, 88f);
        public Vector2 selectionFramePadding = new(4f, 4f);
        public float selectedLift = 4f;
        public float hoverLift = 12f;

        [Header("Item container (building icon remains fixed-size)")]
        public Vector2 restingItemContainerSize = new(47f, 47f);
        public Vector2 hoveredItemContainerSize = new(59f, 59f);
        public Vector2 restingItemContainerPosition = new(0f, 10f);
        public Vector2 hoveredItemContainerPosition = new(0f, 10f);

        [Header("Resource panel (fixed-size, bottom-center anchored)")]
        [FormerlySerializedAs("resourcePanelSize")]
        [InspectorName("Background Size")]
        public Vector2 resourcePanelBackgroundSize = new(41f, 14f);
        [Tooltip("X is measured from the card center; Y is measured upward from the card's bottom edge.")]
        [InspectorName("Background Bottom Offset")]
        public Vector2 resourcePanelBottomOffset = new(0f, 14f);
        [InspectorName("Icon Size")]
        public Vector2 resourceIconSize = new(10f, 10f);
        [Tooltip("Measured from the resource panel's center.")]
        [InspectorName("Icon Offset")]
        public Vector2 resourceIconOffset = new(-14f, 0f);
        [InspectorName("Text Rect Size")]
        public Vector2 resourceTextRectSize = new(27f, 10f);
        [Tooltip("Measured from the resource panel's center.")]
        [InspectorName("Text Offset")]
        public Vector2 resourceTextOffset = new(7f, 0f);
        [Min(1f)]
        [InspectorName("Text Font Size")]
        public float resourceTextFontSize = 8f;

        [Header("Thickness")]
        public float restingThickness = 1f;
        public float hoveredThickness = 3f;

        [Header("Shadow")]
        public Vector2 restingShadowSize = new(53f, 4f);
        public Vector2 hoveredShadowSize = new(61f, 5f);
        public Vector2 restingShadowOffset = new(0f, -42f);
        public Vector2 hoveredShadowOffset = new(0f, -49f);
        [Range(0f, 1f)] public float restingShadowAlpha = 0.75f;
        [Range(0f, 1f)] public float hoveredShadowAlpha = 0.48f;

        [Header("Flight tween")]
        [Min(0.01f)] public float hoverInDuration = 0.16f;
        [Min(0.01f)] public float hoverOutDuration = 0.18f;
        public AnimationCurve hoverInEase = new(
            new Keyframe(0f, 0f, 0f, 2.2f),
            new Keyframe(1f, 1f, 0f, 0f));
        public AnimationCurve hoverOutEase = new(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 2.2f, 0f));

        [Header("Pointer lean / pseudo-perspective")]
        [Range(0f, 15f)] public float maxTiltX = 4f;
        [Range(0f, 15f)] public float maxTiltY = 7f;
        [Range(0f, 5f)] public float maxRoll = 1.25f;
        [Tooltip("Maps normalized horizontal pointer position (-1 left, 1 right) to tilt response.")]
        public AnimationCurve tiltXResponseCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
        [Tooltip("Maps normalized vertical pointer position (-1 bottom, 1 top) to tilt response.")]
        public AnimationCurve tiltYResponseCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
        [Min(0.01f)] public float tiltResetDuration = 0.14f;
        [FormerlySerializedAs("tiltEase")]
        public AnimationCurve tiltResetEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Shine")]
        public Vector2 shineScale = Vector2.one;
        public float shineRotation = -15f;
        [Tooltip("Card-local reference pixels measured from the card center.")]
        public Vector2 shineStartPosition = new(-58f, 0f);
        [Tooltip("Card-local reference pixels measured from the card center.")]
        public Vector2 shineStopPosition = new(58f, 0f);
        [Min(0.05f)] public float shineTravelDuration = 0.72f;
        [Min(0f)] public float shineInterval = 0.18f;
        [Range(0f, 1f)] public float shineAlpha = 0.12f;

        [Header("State presentation")]
        [Range(0f, 1f)] public float unaffordableAlpha = 0.55f;
        [Range(0f, 1f)] public float lockedArtworkAlpha = 0.3f;
        public int hoverSortingOrder = 100;

        private void OnValidate()
        {
            restingCardSize = Max(restingCardSize, Vector2.one);
            hoveredCardSize = Max(hoveredCardSize, restingCardSize);
            selectionFramePadding = Max(selectionFramePadding, Vector2.zero);
            restingItemContainerSize = Max(restingItemContainerSize, Vector2.one);
            hoveredItemContainerSize = Max(hoveredItemContainerSize, restingItemContainerSize);
            resourcePanelBackgroundSize = Max(resourcePanelBackgroundSize, Vector2.one);
            resourceIconSize = Max(resourceIconSize, Vector2.one);
            resourceTextRectSize = Max(resourceTextRectSize, Vector2.one);
            resourceTextFontSize = Mathf.Max(1f, resourceTextFontSize);
            if (tiltXResponseCurve == null)
                tiltXResponseCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
            if (tiltYResponseCurve == null)
                tiltYResponseCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);
            shineScale = Max(shineScale, Vector2.zero);
            restingThickness = Mathf.Max(1f, restingThickness);
            hoveredThickness = Mathf.Max(restingThickness, hoveredThickness);
            restingShadowSize = Max(restingShadowSize, Vector2.one);
            hoveredShadowSize = Max(hoveredShadowSize, Vector2.one);
        }

        private static Vector2 Max(Vector2 a, Vector2 b) =>
            new(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
}
