using UnityEngine;

namespace LittlePeeps
{
    public class Unit : MonoBehaviour
    {
        public UnitDef def;

        [Tooltip("Container child holding every SpriteRenderer the unit is drawn from. Resting toggles " +
                 "this whole object, so hiding stays correct however many parts the art is built from.")]
        [SerializeField] private GameObject visualRoot;

        public UnitType Type => def != null ? def.unitType : default;

        // World-space radius of the unit's collider (used for spawn-clearance math).
        public float Radius => bodyCollider != null ? bodyCollider.bounds.extents.x : 0f;
        public IslandSystem Island => island;

        // Fatigue. A unit that is not tired is WORKING: it harvests and refuses to enter a house. Once
        // tired the two flip — it harvests nothing, and the next house of its type takes it in.
        // CollisionTarget.HandleHit routes every hit by this one flag. The countdown is reset on every
        // Launch (house or tap) and runs only while the unit is NOT boosted — see FixedUpdate — so a
        // unit can never tire mid-boost: its work time is the boost plus def.fatigueDelay. Tired by
        // default (fatigueLeft == 0) so a never-launched unit isn't stuck.
        public bool IsTired => fatigueLeft <= 0f;
        private float fatigueLeft;

        private Rigidbody2D rb;
        private Collider2D bodyCollider;
        private float baseSpeed;

        private IslandSystem island;   // injected on spawn; kept for future island-aware behavior
        private RunStats stats;        // injected on spawn; applies the UnitSpeed modifier to def.speed

        // Decaying launch boost, ticked in FixedUpdate (physics-based acceleration).
        private float launchBoostTimer;
        private float launchTau;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponentInChildren<Collider2D>(true);
        }

        private void OnEnable()
        {
            baseSpeed = ResolveBaseSpeed();
        }

        private void Start()
        {
            if (visualRoot == null)
                Debug.LogError($"Unit on '{name}' has no visualRoot assigned.", this);
        }

        // Injected by SpawnSystem on spawn. Stored for future island-aware behavior.
        public void SetIsland(IslandSystem islandSystem) => island = islandSystem;

        // Injected by SpawnSystem on spawn. baseSpeed is re-resolved on each Launch (which runs after
        // injection), so a speed bonus gained mid-run applies from the unit's next launch onward.
        public void SetStats(RunStats runStats) => stats = runStats;

        // Base movement speed with the UnitSpeed modifier applied. Falls back to the raw def value when
        // stats aren't injected yet (e.g. a scene-placed unit, or before the first spawn injection).
        private float ResolveBaseSpeed()
        {
            if (def == null) return 0f;
            return stats != null ? stats.Apply(def.speed, StatId.UnitSpeed, def.unitType) : def.speed;
        }

        // How long this unit works after its boost settles before it tires, with the run modifier
        // applied. Same fallback rule as ResolveBaseSpeed. A perk that keeps units working longer is a
        // POSITIVE percent here — this is the delay in seconds, not a rate.
        private float ResolveFatigueDelay()
        {
            if (def == null) return 0f;
            return stats != null
                ? stats.Apply(def.fatigueDelay, StatId.UnitFatigueDelay, def.unitType)
                : def.fatigueDelay;
        }

        // Launch in a direction. The unit leaves at baseSpeed * speedMultiplier and a decaying
        // braking force eases its speed back down to baseSpeed over ~boostDuration seconds
        // (see FixedUpdate). Direction is preserved through bounces.
        public void Launch(Vector2 direction, float speedMultiplier = 1f, float boostDuration = 0f)
        {
            baseSpeed = ResolveBaseSpeed();

            // Fatigue restarts on every launch: a full fatigueDelay of work, counted from the moment the
            // boost settles (FixedUpdate). A tap on a tired unit is this same Launch — that is what puts
            // it back to work.
            fatigueLeft = ResolveFatigueDelay();

            // Coming back out of rest: re-enable physics and visuals. Toggling the whole visual root
            // rather than one renderer's `enabled` also stops and restarts the Animator that lives on
            // it, so a running clip can never draw a resting unit back onto the screen.
            rb.simulated = true;
            if (visualRoot != null) visualRoot.SetActive(true);

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Random.insideUnitCircle.normalized;
            rb.linearVelocity = dir * baseSpeed * Mathf.Max(1f, speedMultiplier);

            if (boostDuration > 0f && speedMultiplier > 1f)
            {
                launchBoostTimer = boostDuration;
                launchTau = boostDuration / 3f; // ~95% settled to baseSpeed by boostDuration
            }
            else
            {
                launchBoostTimer = 0f;
            }
        }

        // Pull the unit inside a building: stop and hide it while it rests. Resting is not working, so
        // the fatigue countdown is dropped too — Launch hands out a fresh one on the way out anyway;
        // this only keeps IsTired truthful while the unit is inside.
        public void EnterRest()
        {
            launchBoostTimer = 0f;
            fatigueLeft = 0f;

            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
            if (visualRoot != null) visualRoot.SetActive(false);
        }

        private void FixedUpdate()
        {
            // No active launch/tap boost → physics owns the velocity, and the fatigue clock runs. It
            // does NOT run while boosted: a boosted unit is at work by definition, so its countdown
            // waits until the boost has settled.
            if (launchBoostTimer <= 0f)
            {
                if (fatigueLeft > 0f) fatigueLeft -= Time.fixedDeltaTime;
                return;
            }

            launchBoostTimer -= Time.fixedDeltaTime;

            float speed = rb.linearVelocity.magnitude;
            if (speed < 0.0001f) { launchBoostTimer = 0f; return; }

            if (launchBoostTimer <= 0f)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * baseSpeed;
                return;
            }

            float factor = 1f - Mathf.Exp(-Time.fixedDeltaTime / launchTau);
            float newSpeed = Mathf.Lerp(speed, baseSpeed, factor);
            rb.linearVelocity = rb.linearVelocity.normalized * newSpeed;
        }

        // Tap boost: accelerate along the current heading (random if nearly stopped), then let the
        // decaying brake in FixedUpdate ease the speed back to baseSpeed over ~duration seconds.
        // Re-tapping mid-boost just re-launches, resetting the decay timer — and, like every Launch,
        // restarting fatigue: tapping a tired unit is how it gets back to work. AoE/radius is owned by
        // TapSystem; this only takes the per-unit boost amount and how long it lingers.
        public void Boost(float speedMultiplier, float duration)
        {
            Vector2 dir = rb.linearVelocity.sqrMagnitude > 0.0001f
                ? rb.linearVelocity.normalized
                : Random.insideUnitCircle.normalized;
            Launch(dir, speedMultiplier, duration);
        }
    }
}
