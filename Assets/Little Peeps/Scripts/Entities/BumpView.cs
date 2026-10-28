using LitMotion;
using UnityEngine;

namespace LittlePeeps
{
    // The pinball bumper feel of a structure: a unit that bounces off it makes it squash a touch, and a wave of
    // thin arcs runs out of the face it hit (BumpVfxSystem draws the wave from BumpedEvent; this only says where
    // and of what kind). Opt-in per prefab — put it on whatever should show its bumps; a resource node keeps the
    // squash and switches the waves off.
    //
    // It shows what the hit DID, so it listens to the report of its CollisionTarget (the nearest one on it or
    // above it), never to the physics: only the target knows whether the hit worked (HitReport). By verdict:
    //   Useful  — the squash as authored, and the useful wave;
    //   Blocked — a weaker squash, and the blocked wave (shorter, another colour: something is in the way);
    //   Useless — a weaker squash still, and whatever wave BumpVfxSystem keeps for it (none by default) — the
    //             arcs carry the meaning, the squash is only the physics.
    // A hit that used the target up (Final) shows nothing here: the source's own harvest effect plays for it,
    // and a squash still running is cut so that effect starts from rest. A unit crossing a trigger shows
    // nothing either — there is no face to bounce off.
    public class BumpView : MonoBehaviour
    {
        [Tooltip("What squashes, children included — the structure's Visual. It is scaled from its own origin, so " +
                 "that must sit at the structure's base. Not the root: the colliders would scale too. Indicators " +
                 "that must keep still (the forge's heat bar) live outside Visual, on the root. Empty = no " +
                 "squash, the wave only.")]
        [SerializeField] private Transform squashTarget;

        [Tooltip("Height of the squash target over a Useful hit's squash, from the hit (0) to rest (1). Below 1 = " +
                 "squashed, above = stretched. Keep the last key at 1, or the structure stays deformed.")]
        [SerializeField] private AnimationCurve squashHeight = new(
            new Keyframe(0f, 0.94f), new Keyframe(0.35f, 1.03f), new Keyframe(1f, 1f));

        [Tooltip("How the width answers the height. 1 = keeps the area (squashed lower = that much wider), " +
                 "0 = the width stays.")]
        [SerializeField, Range(0f, 1f)] private float widthFollows = 1f;

        [Tooltip("Seconds the squash takes.")]
        [SerializeField, Min(0.01f)] private float squashDuration = 0.2f;

        [Tooltip("Run a wave of arcs out of the face that was hit. Off = the squash alone: on a resource node " +
                 "(a tree, a rock) the shake says enough, and a forest full of arcs is only noise.")]
        [SerializeField] private bool waves = true;

        [Header("By verdict")]
        [Tooltip("How much of the squash a Blocked hit shows — a tired unit, a full house, an overheated forge, " +
                 "the market from outside. 1 = the full curve, 0 = none.")]
        [SerializeField, Range(0f, 1f)] private float blockedSquash = 0.5f;

        [Tooltip("How much of the squash a Useless hit shows — a house wall to a working unit, a tree to a " +
                 "farmer. 1 = the full curve, 0 = none.")]
        [SerializeField, Range(0f, 1f)] private float uselessSquash = 0.35f;

        [Tooltip("Seconds after a shown bump during which the next ones are skipped — neither squash nor wave — " +
                 "so a crowd of units can't keep the structure shaking non-stop. A STRONGER verdict still shows: " +
                 "a hit that pays is never lost to a wall bump a moment before it.")]
        [SerializeField, Min(0f)] private float minInterval = 0.15f;

        private CollisionTarget target;
        private Vector3 restScale;
        private float lastBump = float.NegativeInfinity;
        private HitVerdict lastVerdict;
        private float amplitude = 1f;   // of the squash running now
        private MotionHandle squashMotion;

        private void Awake()
        {
            if (squashTarget != null) restScale = squashTarget.localScale;

            target = GetComponentInParent<CollisionTarget>();
            if (target == null)
                Debug.LogError($"BumpView on '{name}' has no CollisionTarget on it or above it — there are no " +
                               "hits for it to show.", this);
        }

        private void OnEnable()
        {
            if (target != null) target.HitResolved += OnHit;
        }

        // A structure sold mid-squash must not leave a motion writing to it, and one switched back on must stand
        // at rest.
        private void OnDisable()
        {
            if (target != null) target.HitResolved -= OnHit;
            StopSquash();
        }

        private void OnDestroy() => Cancel(ref squashMotion);

        private void OnHit(HitReport report)
        {
            if (report.Final)
            {
                StopSquash();
                return;
            }
            if (report.Contact == HitContact.Pass) return;
            if (!ShouldShow(report.Verdict, lastVerdict, Time.time - lastBump, minInterval)) return;

            lastBump = Time.time;
            lastVerdict = report.Verdict;

            // An arrow has no face to run a wave out of, and neither has a bounce the physics gave no contact for.
            if (waves && report.Side != Vector2Int.zero)
                EventBus<BumpedEvent>.Publish(new BumpedEvent
                {
                    Position = report.Point,
                    Side = report.Side,
                    Verdict = report.Verdict,
                });

            Squash(AmplitudeOf(report.Verdict));
        }

        // Whether a bump shows, given the last one that did: always once the interval is over; inside it only a
        // stronger verdict — a crowd can't keep the structure shaking, and a hit that pays is never lost to a
        // wall bump a moment before it.
        public static bool ShouldShow(HitVerdict verdict, HitVerdict last, float sinceLast, float minInterval)
            => sinceLast >= minInterval || verdict > last;

        // The curve's height with only `amplitude` of its departure from rest: 1 = as authored, 0 = rest.
        public static float Height(float curveHeight, float amplitude) => 1f + (curveHeight - 1f) * amplitude;

        private float AmplitudeOf(HitVerdict verdict) => verdict switch
        {
            HitVerdict.Useful => 1f,
            HitVerdict.Blocked => blockedSquash,
            _ => uselessSquash,
        };

        // From the start of the curve every time: a hit landing mid-squash is a new hit, and the curve starts at
        // the impact.
        private void Squash(float amount)
        {
            StopSquash();
            if (squashTarget == null || amount <= 0f) return;

            amplitude = amount;
            squashMotion = LMotion.Create(0f, 1f, squashDuration).Bind(ApplySquash);
        }

        private void ApplySquash(float t)
        {
            if (squashTarget == null) return;
            float height = Mathf.Max(0.01f, Height(squashHeight.Evaluate(t), amplitude));
            float width = Mathf.Pow(height, -widthFollows);
            squashTarget.localScale = new Vector3(restScale.x * width, restScale.y * height, restScale.z);
        }

        // Cut the squash short, the target back at rest.
        private void StopSquash()
        {
            Cancel(ref squashMotion);
            if (squashTarget != null) squashTarget.localScale = restScale;
        }

        private static void Cancel(ref MotionHandle handle)
        {
            if (handle.IsActive()) handle.Cancel();
        }
    }
}
