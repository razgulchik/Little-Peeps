using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The bend of something units walk THROUGH — a field, a bush, a tool rack — as a unit crosses it: the top is
    // dragged along the unit's path while the base stays on the ground, then springs back with a swing, like
    // stalks in the wind. Sideways that is a tilt; along the screen's height it is a stretch (the unit runs up:
    // the top goes away from us, which in this 3/4 view reads taller) or a squat (runs down: toward us). A
    // diagonal crossing does both. BumpView's squash is the same feedback for solid things; this is triggers'.
    //
    // Listens to its CollisionTarget's report like BumpView (the nearest one on it or above it): a crossing
    // (HitContact.Pass) kicks the spring in the unit's direction of travel, by verdict. Not on the way in — the
    // object would shy away from a unit that has only touched it — but once the unit is past the middle: the
    // wake of something rushing by drags the top after it. Past the middle = moving away from it (the report's
    // Point), the closest the unit's path comes; so it is the middle for a field of any size, with no timing.
    // A unit that leaves before that (it grazed a corner, turned back) kicks on its way out (PassEnded). Each
    // crossing kicks once. A crossing that used the source up (Final — the farmer reaping the wheat) bends
    // nothing, and drops the ones still on their way: the harvest effect shows it, from rest.
    // Kicks ADD UP: a crowd keeps the field moving without restarts, two units from opposite sides cancel out,
    // and the bend is capped. A spring of its own, not a tween: a tween always returns to where it started, so a
    // kick landing mid-swing would either jump or leave the object bent.
    public class LeanView : MonoBehaviour
    {
        [Tooltip("What bends, children included — the object's Visual. It turns and stretches about its own origin, " +
                 "which must sit at the base, on the ground line. Not the root: the colliders would bend too.")]
        [SerializeField] private Transform leanTarget;

        [Tooltip("Tilt in degrees of a full-strength crossing straight across the screen.")]
        [SerializeField, Range(0f, 30f)] private float maxTilt = 8f;

        [Tooltip("Stretch (a crossing straight up the screen) or squat (straight down) of a full-strength crossing, " +
                 "as a share of the height. Weaker than the tilt by default: a change of height reads stronger " +
                 "than a turn.")]
        [SerializeField, Range(0f, 0.5f)] private float maxStretch = 0.06f;

        [Tooltip("Squat both ways, never stretch — for when a unit running up makes the object look like it hops.")]
        [SerializeField] private bool squatOnly;

        [Tooltip("Swings per second.")]
        [SerializeField, Range(0.5f, 10f)] private float frequency = 3f;

        [Tooltip("How fast the swing dies down: small = it sways back and forth a few times, 1 = it bends and " +
                 "settles back without swinging past.")]
        [SerializeField, Range(0.05f, 1f)] private float damping = 0.3f;

        [Header("By verdict")]
        [Tooltip("How much of the bend a Blocked crossing shows — a tired farmer in the wheat, an Unassigned unit " +
                 "at an empty rack. 1 = full.")]
        [SerializeField, Range(0f, 1f)] private float blockedLean = 0.6f;

        [Tooltip("How much of the bend a Useless crossing shows — anyone but a farmer in the wheat, a worker who " +
                 "already has a tool at a rack. 1 = full.")]
        [SerializeField, Range(0f, 1f)] private float uselessLean = 0.6f;

        // A crowd piles the bend up to this many full-strength bends, never further.
        private const float MaxPile = 1.5f;
        // Settled: closer to rest than this, and slower than this per second, in full-strength bends.
        private const float Rest = 0.003f;

        // A unit inside that has not kicked yet: where the middle it waits to pass is, and how hard it kicks — the
        // verdict is the hit's, settled on the way in.
        private struct Crossing
        {
            public Unit Unit;
            public Vector2 Middle;
            public float Amount;
        }

        private readonly List<Crossing> crossings = new();
        private CollisionTarget target;
        private Vector3 restScale;
        private Quaternion restRotation;
        private Vector2 bend;    // where the top is, in full-strength bends: x across the screen, y up it
        private Vector2 swing;   // how fast it moves, bends per second
        private bool moving;

        private void Awake()
        {
            if (leanTarget != null)
            {
                restScale = leanTarget.localScale;
                restRotation = leanTarget.localRotation;
            }

            target = GetComponentInParent<CollisionTarget>();
            if (target == null)
                Debug.LogError($"LeanView on '{name}' has no CollisionTarget on it or above it — nothing ever " +
                               "crosses it.", this);
        }

        private void OnEnable()
        {
            if (target == null) return;
            target.HitResolved += OnHit;
            target.PassEnded += OnLeft;
        }

        // A field sold mid-swing must not stay bent, and one switched back on must stand straight.
        private void OnDisable()
        {
            if (target != null)
            {
                target.HitResolved -= OnHit;
                target.PassEnded -= OnLeft;
            }
            crossings.Clear();
            Settle();
        }

        private void OnHit(HitReport report)
        {
            if (report.Contact != HitContact.Pass || report.Unit == null) return;
            if (report.Final)
            {
                crossings.Clear();
                Settle();
                return;
            }

            float amount = AmountOf(report.Verdict);
            if (leanTarget == null || amount <= 0f || IndexOf(report.Unit) >= 0) return;

            crossings.Add(new Crossing { Unit = report.Unit, Middle = report.Point, Amount = amount });
        }

        // Out before the middle: the crossing kicks now, so no unit goes through unnoticed. One that already
        // kicked is no longer waiting, and leaves without a second one.
        private void OnLeft(Unit unit)
        {
            int i = IndexOf(unit);
            if (i < 0) return;
            Kick(crossings[i]);
            crossings.RemoveAt(i);
        }

        private int IndexOf(Unit unit)
        {
            for (int i = 0; i < crossings.Count; i++)
                if (crossings[i].Unit == unit) return i;
            return -1;
        }

        // The units past the middle kick; one gone from the field (taken in, destroyed) is dropped without.
        private void KickPassed()
        {
            for (int i = crossings.Count - 1; i >= 0; i--)
            {
                var crossing = crossings[i];
                if (crossing.Unit == null || !crossing.Unit.isActiveAndEnabled)
                    crossings.RemoveAt(i);
                else if (IsPast(crossing.Unit.transform.position, crossing.Unit.Velocity, crossing.Middle))
                {
                    Kick(crossing);
                    crossings.RemoveAt(i);
                }
            }
        }

        // Past the middle: moving away from it. A unit standing still is past nothing yet.
        public static bool IsPast(Vector2 position, Vector2 velocity, Vector2 middle)
            => Vector2.Dot(position - middle, velocity) > 0f;

        // The way the unit goes now, not the way it came in: what drags the top is where it is heading.
        private void Kick(Crossing crossing)
        {
            Vector2 velocity = crossing.Unit != null ? crossing.Unit.Velocity : Vector2.zero;
            if (velocity.sqrMagnitude < 1e-6f) return;

            swing += velocity.normalized * (crossing.Amount * KickFor(frequency, damping));
            moving = true;
        }

        private float AmountOf(HitVerdict verdict) => verdict switch
        {
            HitVerdict.Useful => 1f,
            HitVerdict.Blocked => blockedLean,
            _ => uselessLean,
        };

        // Scaled time: a paused game holds the swing where it is.
        private void Update()
        {
            if (moving || crossings.Count > 0) Tick(Time.deltaTime);
        }

        private void Tick(float dt)
        {
            KickPassed();
            if (!moving) return;

            Step(ref bend, ref swing, 2f * Mathf.PI * frequency, damping, dt);
            bend = Vector2.ClampMagnitude(bend, MaxPile);

            if (bend.sqrMagnitude < Rest * Rest && swing.sqrMagnitude < Rest * Rest * 100f)
            {
                Settle();
                return;
            }

            var (tilt, height) = Pose(bend, maxTilt, maxStretch, squatOnly);
            leanTarget.localRotation = restRotation * Quaternion.Euler(0f, 0f, tilt);
            leanTarget.localScale = new Vector3(restScale.x, restScale.y * height, restScale.z);
        }

        // Straight and still.
        private void Settle()
        {
            bend = swing = Vector2.zero;
            moving = false;
            if (leanTarget == null) return;
            leanTarget.localRotation = restRotation;
            leanTarget.localScale = restScale;
        }

        // The swing speed a kick from rest needs to bend the top exactly one full-strength bend at its farthest,
        // for a spring of this frequency (Hz) and damping ratio: speed = peak × ω / e^(−ζ·atan(√(1−ζ²)/ζ)/√(1−ζ²)),
        // which tends to peak × ω × e as the damping reaches 1 (no swing past).
        public static float KickFor(float frequency, float damping)
        {
            float omega = 2f * Mathf.PI * frequency;
            float zeta = Mathf.Clamp(damping, 0.01f, 1f);
            if (zeta > 0.999f) return omega * Mathf.Exp(1f);
            float s = Mathf.Sqrt(1f - zeta * zeta);
            return omega / Mathf.Exp(-zeta * Mathf.Atan(s / zeta) / s);
        }

        // One step of the damped spring pulling the bend back to rest, solved exactly over dt rather than stepped
        // numerically: the swing is the same at any frame rate, and a long frame can't throw it off.
        public static void Step(ref Vector2 bend, ref Vector2 swing, float omega, float damping, float dt)
        {
            float zeta = Mathf.Clamp(damping, 0.01f, 1f);
            float decay = Mathf.Exp(-zeta * omega * dt);
            Vector2 x0 = bend, v0 = swing;

            if (zeta > 0.999f)   // critically damped
            {
                Vector2 b = v0 + omega * x0;
                bend = decay * (x0 + b * dt);
                swing = decay * (v0 - omega * dt * b);
                return;
            }

            float wd = omega * Mathf.Sqrt(1f - zeta * zeta);
            float c = Mathf.Cos(wd * dt), s = Mathf.Sin(wd * dt);
            bend = decay * (x0 * c + (v0 + zeta * omega * x0) * (s / wd));
            swing = decay * (v0 * c - (zeta * omega * v0 + omega * omega * x0) * (s / wd));
        }

        // The bend as a pose: the tilt in degrees (a top dragged right turns clockwise, a negative Z) and the
        // height as a share of the rest height.
        public static (float tilt, float height) Pose(Vector2 bend, float maxTilt, float maxStretch, bool squatOnly)
        {
            float tilt = -bend.x * maxTilt;
            float height = squatOnly ? 1f - Mathf.Abs(bend.y) * maxStretch : 1f + bend.y * maxStretch;
            return (tilt, Mathf.Max(0.01f, height));
        }
    }
}
