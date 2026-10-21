using UnityEngine;
using UnityEngine.Serialization;

namespace LittlePeeps
{
    // Shared, designer-authored motion and geometry for every build card. Keeping the complete hover
    // treatment in one asset makes the "flight" easy to tune in the Inspector without touching code or
    // editing every card instance separately. Three poses: resting, hovered (the flight) and open (the
    // card in hand, wider, with its name, description and terrains); the card blends between them.
    [CreateAssetMenu(menuName = "LittlePeeps/UI/Build Card Motion Profile")]
    public class BuildCardMotionProfile : ScriptableObject
    {
        [Header("Card geometry (reference pixels)")]
        [Tooltip("Also the card's slot in the palette row while it is closed.")]
        public Vector2 restingCardSize = new(54f, 70f);
        public Vector2 hoveredCardSize = new(66f, 82f);
        public float hoverLift = 12f;

        [Header("Item container (building icon remains fixed-size)")]
        public Vector2 restingItemContainerSize = new(48f, 48f);
        public Vector2 hoveredItemContainerSize = new(60f, 60f);
        public Vector2 restingItemContainerPosition = new(0f, 8f);
        public Vector2 hoveredItemContainerPosition = new(0f, 8f);

        // The price is a ResourceUnit — its own look is the prefab's; only where it stands moves.
        [Header("Cost")]
        [Tooltip("Where the price's centre stands: X from the card's centre, Y up from the card's bottom edge.")]
        public Vector2 costBottomOffset = new(0f, 10.5f);

        [Header("Thickness")]
        public float restingThickness = 1f;
        public float hoveredThickness = 3f;

        [Header("Shadow")]
        public Vector2 restingShadowSize = new(54f, 6f);
        public Vector2 hoveredShadowSize = new(64f, 10f);
        public Vector2 restingShadowOffset = new(0f, -34f);
        public Vector2 hoveredShadowOffset = new(0f, -32f);
        [Range(0f, 1f)] public float restingShadowAlpha = 0.75f;
        [Range(0f, 1f)] public float hoveredShadowAlpha = 0.48f;

        // The open card is the one in hand. Its slot in the row widens with it, so the neighbours move
        // apart; it grows upward from its bottom edge and does not fly or tilt.
        [Header("Open card (the one in hand)")]
        [Tooltip("Even sizes keep the card's edges on whole pixels.")]
        public Vector2 expandedCardSize = new(138f, 90f);
        [Tooltip("How far the open card's bottom edge stands above a resting card's.")]
        public float expandedLift = 2f;
        public Vector2 expandedItemContainerSize = new(64f, 64f);
        [Tooltip("From the open card's centre.")]
        public Vector2 expandedItemContainerPosition = new(-34f, 10f);
        [Tooltip("Where the price's centre stands: X from the card's centre, Y up from the card's bottom edge.")]
        public Vector2 expandedCostBottomOffset = new(-34f, 13.5f);
        public Vector2 expandedShadowSize = new(138f, 6f);
        public Vector2 expandedShadowOffset = new(0f, -32f);
        [Range(0f, 1f)] public float expandedShadowAlpha = 0.75f;
        [Min(0.01f)] public float expandDuration = 0.18f;
        [Min(0.01f)] public float collapseDuration = 0.14f;
        public AnimationCurve expandEase = new(
            new Keyframe(0f, 0f, 0f, 2.2f),
            new Keyframe(1f, 1f, 0f, 0f));
        public AnimationCurve collapseEase = new(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 2.2f, 0f));
        [Tooltip("The name, description and terrains fade in over the end of the opening: 0.8 = from 80% " +
                 "of the way on. Closing fades them out first, over the same stretch.")]
        [Range(0f, 0.99f)] public float expandedTextFrom = 0.8f;

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
        [Tooltip("Draw priority within the build row while hovered. The open card uses one less.")]
        public int hoverSortingOrder = 100;

        private void OnValidate()
        {
            restingCardSize = Max(restingCardSize, Vector2.one);
            hoveredCardSize = Max(hoveredCardSize, restingCardSize);
            expandedCardSize = Max(expandedCardSize, restingCardSize);
            restingItemContainerSize = Max(restingItemContainerSize, Vector2.one);
            hoveredItemContainerSize = Max(hoveredItemContainerSize, restingItemContainerSize);
            expandedItemContainerSize = Max(expandedItemContainerSize, Vector2.one);
            expandedShadowSize = Max(expandedShadowSize, Vector2.one);
            if (expandEase == null) expandEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            if (collapseEase == null) collapseEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);
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
