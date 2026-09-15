using UnityEngine;

namespace LittlePeeps
{
    // The "tired" marker over a unit's head: bars that appear above the unit, drop onto it, fade out
    // and appear above again, for as long as the unit is tired. Presentation only: reads Unit.IsTired
    // every frame and never writes to it, so removing this component leaves the mechanic untouched.
    // Sits on its own object under the unit's visual root — so it hides with the unit inside a house
    // for free — as a SIBLING of the model, not under it, so the walk Animator, a flip and the
    // SpriteShadow never touch it.
    //
    // Authored in the LANDED state: this object's local position is where the bars come to rest (the
    // art's bottom-centre pivot puts their bottom edge there — set it just over the head), and
    // dropPixels says how far above that they appear. One cycle is `period` seconds; two curves run
    // over it with x = 0..1 of the cycle: `drop` is the position (0 = top, 1 = landed) and `alpha` the
    // opacity. The defaults fall fast and land soft, hold, then fade out over the last part; the next
    // cycle starts back at the top, fully opaque. Every new bout of fatigue starts from the top too.
    //
    // The drop is snapped to whole sprite pixels: the scene's Pixel Perfect Camera does no snapping of
    // its own, and a point-filtered sprite sliding through sub-pixel positions shimmers.
    //
    // Polled, not event-driven, on purpose: the Update is needed anyway to animate while tired, the
    // read is one float compare, and a unit resting inside a house is under a disabled visual root
    // and costs nothing. If unit counts ever make a per-component Update matter, the move is to one
    // system walking every unit, not to events — ForgeHeatView reads its model the same way.
    [RequireComponent(typeof(SpriteRenderer))]
    public class TiredView : MonoBehaviour
    {
        [Tooltip("How far above the landed position (this object's authored local position) the bars " +
                 "appear at the start of each cycle, in sprite pixels.")]
        [SerializeField, Min(0f)] private float dropPixels = 8f;

        [Tooltip("Seconds for one appear → drop → fade cycle.")]
        [SerializeField, Min(0.01f)] private float period = 1.2f;

        [Tooltip("Position over the cycle: x = 0..1 of the period, y = 0 at the top, 1 landed. Default " +
                 "lands by mid-cycle, fast off the top and soft at the bottom, then stays.")]
        [SerializeField] private AnimationCurve drop = new(
            new Keyframe(0f, 0f, 0f, 4f),
            new Keyframe(0.45f, 1f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Tooltip("Opacity over the cycle: x = 0..1 of the period, y = alpha. Default holds full until " +
                 "0.6 and fades to nothing by the end, so the bars vanish landed and pop back in at the top.")]
        [SerializeField] private AnimationCurve alpha = new(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(0.6f, 1f, 0f, -2.5f),
            new Keyframe(1f, 0f, -2.5f, 0f));

        private Unit unit;
        private SpriteRenderer sprite;
        private Vector3 landedLocal;   // the authored local position — where the bars come to rest
        private float pixel;           // world units per sprite pixel
        private float cycleTime;
        private bool shown;

        private void Awake()
        {
            unit = GetComponentInParent<Unit>();
            sprite = GetComponent<SpriteRenderer>();
            landedLocal = transform.localPosition;

            if (unit == null)
                Debug.LogError($"TiredView on '{name}' has no Unit above it.", this);
            if (sprite.sprite == null)
                Debug.LogError($"TiredView on '{name}' has no sprite assigned.", this);

            pixel = sprite.sprite != null ? 1f / sprite.sprite.pixelsPerUnit : 1f / 16f;
        }

        // Re-sync the moment the visual root comes back on (the unit leaving a house): Launch has
        // already reset fatigue by then, so the bars must not flash for a frame before Update runs.
        private void OnEnable()
        {
            if (unit == null) return;
            Show(unit.IsTired);
        }

        private void Update()
        {
            if (unit == null) return;

            bool tired = unit.IsTired;
            if (tired != shown) Show(tired);
            if (!tired) return;

            cycleTime = Mathf.Repeat(cycleTime + Time.deltaTime, period);
            Animate(cycleTime / period);
        }

        // Flip the marker on or off. On: the cycle starts over from the top and is drawn right away, so
        // the frame the unit tires already shows the bars at their first position, not a stale fade.
        private void Show(bool on)
        {
            shown = on;
            sprite.enabled = on;
            cycleTime = 0f;
            if (on) Animate(0f);
        }

        private void Animate(float k)
        {
            float above = dropPixels * (1f - Mathf.Clamp01(drop.Evaluate(k)));
            transform.localPosition = landedLocal + Vector3.up * (Mathf.Round(above) * pixel);

            var c = sprite.color;
            c.a = Mathf.Clamp01(alpha.Evaluate(k));
            sprite.color = c;
        }
    }
}
