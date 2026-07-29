using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LittlePeeps
{
    // The "+1.2k" that floats off a harvested source. Universal: one prefab and one set of curves for
    // every resource in the game, unlike the pickup particles which are authored per source.
    //
    // Driven by the same HarvestedEvent as HarvestVfxSystem, subscribed independently rather than
    // folded into it. The two have nothing in common at runtime: particles are handed to Unity and
    // forgotten, while these are ours to pool and move every frame. Keeping them apart is also what
    // makes the renderer swappable — when TMP runs out of headroom the replacement (one mesh built
    // from a glyph atlas, one draw call for any count) touches this file and nothing else.
    //
    // Deliberately a world-space TextMeshPro, never TextMeshProUGUI — the field's type enforces it.
    // UGUI text lives on a Canvas, and a Canvas rebuilds its whole batch whenever anything on it
    // moves; a hundred drifting numbers would rebuild it a hundred times a frame.
    //
    // The tuning lives HERE rather than on the prefab, on purpose: the feel then survives a change of
    // renderer, which is the whole point of treating the TMP version as a first pass.
    //
    // Place on the same GameObject as HarvestVfxSystem.
    public class HarvestNumbers : MonoBehaviour
    {
        [Tooltip("World-space TextMeshPro prefab. Set its Sorting Layer to Overlay so numbers read " +
                 "over the village; font, size, outline and colour are all the prefab's business.")]
        [SerializeField] private TextMeshPro numberPrefab;

        [Tooltip("Used only to skip numbers nobody can see. Empty = Camera.main is taken on Awake.")]
        [SerializeField] private Camera viewCamera;

        [SerializeField, Range(0f, 0.5f)] private float offscreenMargin = 0.1f;

        [Header("Motion")]
        [Tooltip("Seconds from spawn to gone.")]
        [Min(0.05f)] [SerializeField] private float lifetime = 0.9f;

        [Tooltip("Total travel in world units, reached at the end of the curve below. Straight up by " +
                 "default: the number reads as a readout, and letting it drift sideways only makes it " +
                 "compete with the pickup, which is the thing that IS supposed to fly off diagonally. " +
                 "X is kept tunable in case a lean is wanted later.")]
        [SerializeField] private Vector2 travel = new Vector2(0f, 0.8f);

        [Tooltip("How far along `travel` the number is, across its life. A curve that flattens out " +
                 "reads as the number shooting out and settling.")]
        [SerializeField] private AnimationCurve travelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Scale across the life. A short spike above 1 at the start gives it a pop.")]
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Alpha across the life.")]
        [SerializeField] private AnimationCurve alphaCurve =
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));

        [Tooltip("Shifts EVERY number, in world units, on top of whatever the source's Fx Anchor says. " +
                 "The division of labour: the anchor is per-object and answers 'where does this thing's " +
                 "feedback come from' (a tree's top edge is not a field's); this is the global nudge, " +
                 "for lifting all numbers a little without editing prefabs one at a time.")]
        [SerializeField] private Vector2 spawnOffset = Vector2.zero;

        [Tooltip("Random spawn offset in world units, so numbers from the same spot don't stack into " +
                 "one unreadable blob. Every hit still gets its own number.")]
        [SerializeField] private Vector2 spawnJitter = new Vector2(0.15f, 0.1f);

        [Tooltip("Most numbers alive at once; past the cap the one nearest death is recycled. This is a " +
                 "readability valve as much as a budget — beyond a certain density nobody can read them.")]
        [Min(1)] [SerializeField] private int maxConcurrent = 60;

        private struct Entry
        {
            public TextMeshPro view;
            public Vector3 origin;
            public float t;
        }

        private Entry[] active;               // fixed size = maxConcurrent; no growth, no reallocation
        private int count;
        private readonly Stack<TextMeshPro> pool = new();

        // The prefab's own scale, which scaleCurve multiplies rather than replaces. A world-space TMP
        // is enormous next to a one-unit tile, so the prefab is always scaled right down — writing a
        // raw curve value into localScale would throw that away and blow every number up to tile size.
        private Vector3 baseScale = Vector3.one;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            active = new Entry[maxConcurrent];
            if (numberPrefab != null) baseScale = numberPrefab.transform.localScale;
        }

        private void OnEnable() => EventBus<HarvestedEvent>.Subscribe(OnHarvested);

        private void OnDisable()
        {
            EventBus<HarvestedEvent>.Unsubscribe(OnHarvested);

            // Nothing will tick these again, so don't leave a screenful of numbers frozen mid-flight.
            for (int i = 0; i < count; i++) Release(active[i].view);
            count = 0;
        }

        private void Start()
        {
            if (numberPrefab == null)
                Debug.LogError($"HarvestNumbers on '{name}' has no numberPrefab assigned.", this);
        }

        private void OnHarvested(HarvestedEvent e)
        {
            if (numberPrefab == null) return;
            if (!IsOnScreen(e.Position)) return;

            if (count == active.Length)
            {
                int oldest = Oldest();
                Release(active[oldest].view);
                active[oldest] = active[--count];
            }

            var view = Get();
            if (view == null) return;

            Vector3 origin = e.Position + new Vector3(
                spawnOffset.x + Random.Range(-spawnJitter.x, spawnJitter.x),
                spawnOffset.y + Random.Range(-spawnJitter.y, spawnJitter.y),
                0f);

            SetAmount(view, e.Amount);
            Place(view, origin, 0f);

            active[count++] = new Entry { view = view, origin = origin, t = 0f };
        }

        // One flat loop for every live number: no coroutine per popup (an allocation each) and no
        // MonoBehaviour of its own (a managed call each). Finished entries are swap-removed, so the
        // loop never walks holes.
        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                ref Entry e = ref active[i];
                e.t += dt;

                float k = e.t / lifetime;
                if (k >= 1f)
                {
                    Release(e.view);
                    active[i] = active[--count];
                    i--;                       // the swapped-in entry has not been ticked yet
                    continue;
                }

                Place(e.view, e.origin, k);
            }
        }

        private void Place(TextMeshPro view, Vector3 origin, float k)
        {
            var tr = view.transform;
            tr.position = origin + (Vector3)(travel * travelCurve.Evaluate(k));
            tr.localScale = baseScale * scaleCurve.Evaluate(k);

            // TMP_Text.alpha recolours the existing vertices; assigning .color or .text would instead
            // mark the mesh dirty and rebuild it, which is the one thing worth avoiding per frame.
            view.alpha = alphaCurve.Evaluate(k);
        }

        // Writes the amount straight into the label's char buffer. TMP's SetText overloads take the
        // value as an argument and format in place, so no string is built and nothing lands on the GC —
        // which matters when this runs on every single harvest in the village.
        //
        // "{0:1}" is TMP's own spec for one decimal place, not string.Format's.
        private static void SetAmount(TextMeshPro label, float amount)
        {
            if (amount < 1000f)
            {
                // A whole number reads better bare: "+1", not "+1.0". Yields stay whole until a
                // percentage modifier lands on them, so most of the game this is the branch taken.
                if (Mathf.Approximately(amount, Mathf.Round(amount))) label.SetText("+{0:0}", amount);
                else label.SetText("+{0:1}", amount);
                return;
            }

            float v = amount;
            int tier = 0;
            while (v >= 1000f && tier < 4)
            {
                v /= 1000f;
                tier++;
            }

            switch (tier)
            {
                case 1:  label.SetText("+{0:1}k", v); break;
                case 2:  label.SetText("+{0:1}M", v); break;
                case 3:  label.SetText("+{0:1}B", v); break;
                default: label.SetText("+{0:1}T", v); break;
            }
        }

        // The live number closest to dying — the least disruptive one to take when at the cap.
        private int Oldest()
        {
            int best = 0;
            float bestT = active[0].t;
            for (int i = 1; i < count; i++)
                if (active[i].t > bestT) { bestT = active[i].t; best = i; }
            return best;
        }

        private TextMeshPro Get()
        {
            if (pool.Count > 0)
            {
                var reused = pool.Pop();
                reused.gameObject.SetActive(true);
                return reused;
            }

            var created = Instantiate(numberPrefab, transform);
            return created;
        }

        private void Release(TextMeshPro view)
        {
            if (view == null) return;
            view.gameObject.SetActive(false);
            pool.Push(view);
        }

        private bool IsOnScreen(Vector3 world)
        {
            if (viewCamera == null) return true;   // nothing to test against: never swallow the number

            Vector3 v = viewCamera.WorldToViewportPoint(world);
            float m = offscreenMargin;
            return v.z >= 0f && v.x >= -m && v.x <= 1f + m && v.y >= -m && v.y <= 1f + m;
        }
    }
}
