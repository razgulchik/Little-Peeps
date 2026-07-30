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
    // This class owns the PLUMBING — the pool, the cap, the culling, the event. The FEEL lives in
    // HarvestNumberMotionDef, an asset shared with the Edit Mode authoring tool, so that tuning the
    // motion never means touching the scene and the tool cannot drift away from what the game does.
    //
    // Place on the same GameObject as HarvestVfxSystem.
    public class HarvestNumbers : MonoBehaviour
    {
        [Tooltip("World-space TextMeshPro prefab. Set its Sorting Layer to Overlay so numbers read " +
                 "over the village; font, size, outline and colour are all the prefab's business.")]
        [SerializeField] private TextMeshPro numberPrefab;

        [Tooltip("How the number travels, swells and fades. An asset, so the same motion drives the " +
                 "Edit Mode preview under Window > Little Peeps > Harvest Feedback.")]
        [SerializeField] private HarvestNumberMotionDef motion;

        [Tooltip("Used only to skip numbers nobody can see. Empty = Camera.main is taken on Awake.")]
        [SerializeField] private Camera viewCamera;

        [SerializeField, Range(0f, 0.5f)] private float offscreenMargin = 0.1f;

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

        // The prefab's own scale, which the motion's scale curve multiplies rather than replaces.
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
            if (motion == null)
                Debug.LogError($"HarvestNumbers on '{name}' has no motion asset assigned.", this);
        }

        private void OnHarvested(HarvestedEvent e)
        {
            if (numberPrefab == null || motion == null) return;
            if (!IsOnScreen(e.Position)) return;

            if (count == active.Length)
            {
                int oldest = Oldest();
                Release(active[oldest].view);
                active[oldest] = active[--count];
            }

            var view = Get();
            if (view == null) return;

            Vector3 origin = motion.ResolveOrigin(
                e.Position, new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));

            HarvestNumberFormat.Write(view, e.Amount);
            motion.Apply(view, origin, 0f, baseScale);

            active[count++] = new Entry { view = view, origin = origin, t = 0f };
        }

        // One flat loop for every live number: no coroutine per popup (an allocation each) and no
        // MonoBehaviour of its own (a managed call each). Finished entries are swap-removed, so the
        // loop never walks holes.
        private void Update()
        {
            if (motion == null) return;

            float dt = Time.deltaTime;
            float lifetime = Mathf.Max(0.0001f, motion.lifetime);

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

                motion.Apply(e.view, e.origin, k, baseScale);
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
