using UnityEngine;

namespace LittlePeeps
{
    public class Unit : MonoBehaviour
    {
        public UnitDef def;

        [Tooltip("Container child holding every SpriteRenderer the unit is drawn from. Resting toggles " +
                 "this whole object, so hiding stays correct however many parts the art is built from.")]
        [SerializeField] private GameObject visualRoot;

        // What the unit DOES right now — the key every profession read goes through: which sources pay
        // it (ResourceSourceDef.TryGetYield), the forge gate, the yield and speed modifiers. It is
        // `Profession`, never `def.unitType`: the def says what the unit was born as, and population
        // accounting (SpawnSystem) keys on that; this one changes when a rack hands the unit a tool.
        public UnitType Type => Profession;

        // The profession the unit is working as this outing. Starts as the def's value every time the
        // unit comes out of the pool (OnEnable — the pool assigns `def` before activating), a rack
        // changes it through Equip, and Unequip puts it back when the outing ends. A villager def is
        // born Unassigned and so harvests nothing until it has crossed a rack; the old Farmer def is
        // born a farmer and — never being Unassigned — never takes a tool.
        public UnitType Profession { get; private set; }
        private UnitType BornProfession => def != null ? def.unitType : default;

        // World-space radius of the unit's collider (used for spawn-clearance math).
        public float Radius => bodyCollider != null ? bodyCollider.bounds.extents.x : 0f;
        public IslandSystem Island => island;

        // Stamina: seconds of field work left. It ticks down in FixedUpdate the whole time the unit is
        // out of a house, boosted or not — a boost makes the outing more productive, never longer. A
        // unit with stamina left is WORKING: it harvests and refuses every house. At zero it is TIRED:
        // it harvests nothing, moves at def.tiredSpeedMultiplier of its speed, and the next house of
        // its type takes it in. CollisionTarget.HandleHit routes every hit by this one flag. Only a
        // house refills stamina (Launch); a tap does so only if TapSystem says it does. Tired by
        // default (stamina == 0) so a never-launched unit isn't stuck.
        public bool IsTired => stamina <= 0f;
        private float stamina;

        private Rigidbody2D rb;
        private Collider2D bodyCollider;
        private float baseSpeed;

        private IslandSystem island;   // injected on spawn; kept for future island-aware behavior
        private RunStats stats;        // injected on spawn; applies the UnitSpeed modifier to def.speed

        // Decaying launch boost, ticked in FixedUpdate (physics-based acceleration).
        private float launchBoostTimer;
        private float launchTau;

        // The speed physics keeps the unit at once no boost is decaying: its base speed while working,
        // a fraction of it while tired. Every boost settles back to THIS, and OnBecameTired drops to
        // it — so the tired slowdown is one number, read wherever a speed is set.
        private float TargetSpeed => IsTired ? baseSpeed * TiredMultiplier : baseSpeed;
        private float TiredMultiplier => def != null ? def.tiredSpeedMultiplier : 1f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponentInChildren<Collider2D>(true);
        }

        private void OnEnable()
        {
            // Fresh out of the pool (or first activation): whatever the previous outing equipped is
            // gone with it — Despawn already returned the tool — so the unit starts as it was born.
            Profession = BornProfession;
            baseSpeed = ResolveBaseSpeed();
        }

        private void Start()
        {
            if (visualRoot == null)
                Debug.LogError($"Unit on '{name}' has no visualRoot assigned.", this);
        }

        // Injected by SpawnSystem on spawn. Stored for future island-aware behavior.
        public void SetIsland(IslandSystem islandSystem) => island = islandSystem;

        // Injected by SpawnSystem on spawn. baseSpeed is re-resolved on each launch (which runs after
        // injection), so a speed bonus gained mid-run applies from the unit's next launch onward.
        public void SetStats(RunStats runStats) => stats = runStats;

        // Base movement speed with the UnitSpeed modifier applied — keyed on the PROFESSION, so a
        // "lumberjacks walk faster" perk reaches a villager the moment it picks up an axe (Equip
        // re-resolves). Falls back to the raw def value when stats aren't injected yet (e.g. a
        // scene-placed unit, or before the first spawn injection).
        private float ResolveBaseSpeed()
        {
            if (def == null) return 0f;
            return stats != null ? stats.Apply(def.speed, StatId.UnitSpeed, Profession) : def.speed;
        }

        // Full stamina for this unit: seconds of field work per outing, with the run modifier applied.
        // Same fallback rule as ResolveBaseSpeed. A perk that keeps units out longer is a POSITIVE
        // percent here — these are seconds, not a rate. Unscoped on purpose: this is resolved at
        // launch, before the unit has crossed any rack, so there is no profession to key on yet.
        private float ResolveMaxStamina()
        {
            if (def == null) return 0f;
            return stats != null
                ? stats.Apply(def.stamina, StatId.UnitStamina)
                : def.stamina;
        }

        // A rack hands the unit a tool: from here on it works as `profession`. Whether the unit may take
        // one (it must be Unassigned) is the rack's call — this only records the result. The base speed
        // is cached at launch, so it is re-resolved here for the new profession and the running velocity
        // settled to it — the same one-off rescale a tired transition does, for the same reason.
        public void Equip(UnitType profession)
        {
            Profession = profession;
            baseSpeed = ResolveBaseSpeed();
            SettleSpeed();
        }

        // The outing is over — the unit is back to what it was born as. Called on every way an outing
        // ends (house rest, despawn) so a profession can never survive into the next launch; a unit
        // that never equipped anything is unaffected. The tool itself goes back to the rack from here
        // once racks exist. No speed work: the callers stop the unit anyway, and the next launch
        // re-resolves.
        public void Unequip()
        {
            Profession = BornProfession;
        }

        // Launch from a house in a direction, with full stamina — the house is what refills it. The unit
        // leaves at its speed * speedMultiplier and a decaying braking force eases it back down over
        // ~boostDuration seconds (see FixedUpdate). Direction is preserved through bounces.
        public void Launch(Vector2 direction, float speedMultiplier = 1f, float boostDuration = 0f)
        {
            stamina = ResolveMaxStamina();
            ApplyLaunch(direction, speedMultiplier, boostDuration);
        }

        // Tap boost: accelerate along the current heading (random if nearly stopped), then let the
        // decaying brake in FixedUpdate ease the speed back to TargetSpeed over ~duration seconds.
        // Re-tapping mid-boost just re-launches, resetting the decay timer. Whether the tap also refills
        // stamina is the tap's business (TapSystem): off, the boost is speed only — a tired unit stays
        // tired, keeps its bars and just gets home faster; on, it is a full restart like a house launch.
        // AoE/radius is owned by TapSystem; this only takes the per-unit boost amount and how long it
        // lingers.
        public void Boost(float speedMultiplier, float duration, bool refreshStamina)
        {
            if (refreshStamina) stamina = ResolveMaxStamina();

            Vector2 dir = rb.linearVelocity.sqrMagnitude > 0.0001f
                ? rb.linearVelocity.normalized
                : Random.insideUnitCircle.normalized;
            ApplyLaunch(dir, speedMultiplier, duration);
        }

        // Shared body of Launch and Boost. Reads TargetSpeed AFTER the caller has settled stamina, so a
        // tired unit that is boosted without a refill leaves at its tired speed times the multiplier
        // and settles back to the tired speed — weaker all the way through, as it should be.
        private void ApplyLaunch(Vector2 direction, float speedMultiplier, float boostDuration)
        {
            baseSpeed = ResolveBaseSpeed();

            // Coming back out of rest: re-enable physics and visuals. Toggling the whole visual root
            // rather than one renderer's `enabled` also stops and restarts the Animator that lives on
            // it, so a running clip can never draw a resting unit back onto the screen. Stamina is
            // already settled by the caller, so TiredView.OnEnable reads the right state.
            rb.simulated = true;
            if (visualRoot != null) visualRoot.SetActive(true);

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Random.insideUnitCircle.normalized;
            rb.linearVelocity = dir * TargetSpeed * Mathf.Max(1f, speedMultiplier);

            if (boostDuration > 0f && speedMultiplier > 1f)
            {
                launchBoostTimer = boostDuration;
                launchTau = boostDuration / 3f; // ~95% settled to TargetSpeed by boostDuration
            }
            else
            {
                launchBoostTimer = 0f;
            }
        }

        // Pull the unit inside a building: stop and hide it while it rests. Resting is not working, so
        // the stamina is dropped too — Launch refills it on the way out anyway; this only keeps IsTired
        // truthful while the unit is inside. Not through OnBecameTired: there is no velocity to touch.
        // Going home ends the outing, so the profession (and its tool) is given up here as well.
        public void EnterRest()
        {
            launchBoostTimer = 0f;
            stamina = 0f;
            Unequip();

            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
            if (visualRoot != null) visualRoot.SetActive(false);
        }

        private void FixedUpdate()
        {
            // The stamina clock runs whenever the unit is on the field, boosted or not. A resting unit
            // sits at zero (EnterRest), so the guard skips it for free.
            if (stamina > 0f)
            {
                stamina -= Time.fixedDeltaTime;
                if (stamina <= 0f) OnBecameTired();
            }

            // No active launch/tap boost → physics owns the velocity.
            if (launchBoostTimer <= 0f) return;

            launchBoostTimer -= Time.fixedDeltaTime;

            float speed = rb.linearVelocity.magnitude;
            if (speed < 0.0001f) { launchBoostTimer = 0f; return; }

            // TargetSpeed is re-read every step, so a unit that tires mid-boost simply eases down to
            // its tired speed instead of its working one.
            float target = TargetSpeed;
            if (launchBoostTimer <= 0f)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * target;
                return;
            }

            float factor = 1f - Mathf.Exp(-Time.fixedDeltaTime / launchTau);
            float newSpeed = Mathf.Lerp(speed, target, factor);
            rb.linearVelocity = rb.linearVelocity.normalized * newSpeed;
        }

        // The one working → tired transition — from the clock today, and from any future stamina spend
        // (a building charging per paying hit) tomorrow. Direction is kept — the unit sags, it doesn't
        // turn.
        private void OnBecameTired()
        {
            stamina = 0f;
            SettleSpeed();
        }

        // TargetSpeed just changed under a moving unit (it tired, or it took a tool and its base speed
        // moved): make the velocity follow. Mid-boost there is nothing to do — the decay is already
        // heading for TargetSpeed and re-reads it every step. Otherwise physics owns the velocity and
        // would keep the old speed forever (bounciness 1, no drag), so the magnitude is rescaled here
        // once. A stopped unit (resting) has nothing to rescale.
        private void SettleSpeed()
        {
            if (launchBoostTimer > 0f) return;

            float speed = rb.linearVelocity.magnitude;
            if (speed < 0.0001f) return;
            rb.linearVelocity = rb.linearVelocity.normalized * TargetSpeed;
        }
    }
}
