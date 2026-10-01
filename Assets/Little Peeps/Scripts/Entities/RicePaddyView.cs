using UnityEngine;

namespace LittlePeeps
{
    // The rice paddy's look. Presentation only: reads RicePaddy every frame and never writes to it, so
    // removing this component leaves the field working. Sits on the paddy's root, next to RicePaddy, like
    // every view; the renderer it drives is wired in.
    //
    // One renderer, one sprite at a time: the growing sprites split the growth time between them equally
    // (dry, then the water coming in, then the sprouts), then ripe, then harvested until the ground
    // regrows and it starts over at the first growing sprite. Author the renderer with that first one —
    // the build-mode ghost is this same prefab with every behaviour disabled, so it shows what is authored.
    //
    // Every change of look plays a stretch and squash on the renderer's transform, Y only: the field
    // shoots up, drops into a squash, the new sprite takes over at swapAt, and it settles back. The sprite
    // pivot sits at the bottom, so the field grows out of the ground rather than around its middle. The
    // shape is an authored curve on its own clock, like the reap squash on wheat (HarvestFade) — no tween.
    // A look that changes again before the swap simply lands at the swap; one that changes after it is
    // shown at once, with no second bounce. Game time: a paused game holds the squash where it is.
    // Nothing plays when the field first appears, and a move changes no look, so it plays nothing either.
    public class RicePaddyView : MonoBehaviour
    {
        [Tooltip("The field's renderer. Author it with the first growing sprite (dry).")]
        [SerializeField] private SpriteRenderer field;

        [Tooltip("The growing look, in order from bare ground: dry, watering 1, watering 2, full water, sprouts. " +
                 "The growth time is split between them equally.")]
        [SerializeField] private Sprite[] growing;

        [Tooltip("Ripe: waiting for a farmer.")]
        [SerializeField] private Sprite ripe;

        [Tooltip("After the harvest, until the ground regrows.")]
        [SerializeField] private Sprite harvested;

        [Header("Change of look")]
        [Tooltip("Seconds the stretch and squash takes. 0 = the sprite just swaps.")]
        [SerializeField, Min(0f)] private float changeDuration = 0.4f;

        [Tooltip("Vertical scale over the change, as a multiple of the authored one: x = 0..1 of the duration. " +
                 "Start and end it at 1, or the field jumps.")]
        [SerializeField] private AnimationCurve changeScaleY = new(
            new Keyframe(0f, 1f), new Keyframe(0.3f, 1.5f), new Keyframe(0.6f, 0.6f), new Keyframe(1f, 1f));

        [Tooltip("Where on the curve (0..1) the new sprite replaces the old one — usually the deepest squash.")]
        [SerializeField, Range(0f, 1f)] private float swapAt = 0.6f;

        private RicePaddy paddy;
        private float authoredScaleY;
        private Sprite pending;       // the look to show — what the field is, or is about to be after the swap
        private bool changing;
        private bool swapped;
        private float clock;          // 0..1 along the change

        private void Awake()
        {
            paddy = GetComponentInParent<RicePaddy>();
            if (paddy == null)
                Debug.LogError($"RicePaddyView on '{name}' has no RicePaddy above it in the hierarchy.", this);
            if (field == null)
                Debug.LogError($"RicePaddyView on '{name}' has no field renderer assigned.", this);
            else
                authoredScaleY = field.transform.localScale.y;
        }

        // The look the field starts with, shown without a bounce.
        private void Start()
        {
            if (paddy == null || field == null) return;

            var look = LookNow();
            if (look != null) field.sprite = look;
            pending = field.sprite;
        }

        private void Update()
        {
            if (paddy == null || field == null) return;

            var look = LookNow();
            if (look != null && look != pending)
            {
                pending = look;
                if (changeDuration <= 0f || (changing && swapped)) field.sprite = pending;
                else if (!changing) { changing = true; swapped = false; clock = 0f; }
            }

            if (changing) Step(Time.deltaTime);
        }

        private void Step(float dt)
        {
            clock += dt / changeDuration;
            if (!swapped && clock >= swapAt)
            {
                field.sprite = pending;
                swapped = true;
            }

            if (clock >= 1f)
            {
                if (!swapped) field.sprite = pending;
                SetScaleY(authoredScaleY);
                changing = false;
            }
            else
            {
                SetScaleY(authoredScaleY * changeScaleY.Evaluate(clock));
            }
        }

        // The sprite for the paddy's state right now; null where the slot is left empty (the look stays).
        private Sprite LookNow()
        {
            switch (paddy.Current)
            {
                case RicePaddy.Phase.Ripe:      return ripe;
                case RicePaddy.Phase.Harvested: return harvested;
                default:
                    if (growing == null || growing.Length == 0) return null;
                    int i = Mathf.Min((int)(paddy.GrowthProgress * growing.Length), growing.Length - 1);
                    return growing[i];
            }
        }

        private void SetScaleY(float y)
        {
            var t = field.transform;
            var s = t.localScale;
            s.y = y;
            t.localScale = s;
        }
    }
}
