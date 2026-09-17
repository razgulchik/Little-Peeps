using UnityEngine;

namespace LittlePeeps
{
    public class Unit : MonoBehaviour
    {
        public UnitDef def;

        [Tooltip("Container child holding every SpriteRenderer the unit is drawn from. Resting toggles " +
                 "this whole object, so hiding stays correct however many parts the art is built from.")]
        [SerializeField] private GameObject visualRoot;

        [Header("Bounce")]
        [Tooltip("After a bounce the unit never leaves closer than this to the wall's perpendicular: a " +
                 "head-on trajectory is bent this far to whichever side it already leaned. 0 = off. What " +
                 "keeps a unit from shuttling forever between two parallel fences.")]
        [SerializeField, Range(0f, 45f)] private float minBounceAngle = 10f;

        [Tooltip("Random turn added to every bounce, in degrees either way, so two units on the same " +
                 "line drift apart instead of tracing one path. 0 = off.")]
        [SerializeField, Range(0f, 30f)] private float bounceJitterDegrees = 3f;

        // What the unit DOES right now — the id every profession read goes through: which sources pay
        // it (ResourceSourceDef.TryGetYield), the forge gate, the yield and speed modifiers. Derived
        // from `Profession`, never from the def: the def says what the unit was born as, and
        // population accounting (SpawnSystem) counts per def; this changes when a rack hands the unit
        // a tool. No profession at all (a def with none assigned) reads as Unassigned.
        public UnitType Type => Profession != null ? Profession.type : UnitType.Unassigned;

        // The profession the unit is working as this outing, look included (ProfessionView draws it).
        // Starts as the def's every time the unit comes out of the pool (OnEnable — the pool assigns
        // `def` before activating), a rack changes it through Equip, and Unequip puts it back when
        // the outing ends. A villager def is born Unassigned and so harvests nothing until it has
        // crossed a rack; a def born with a profession (the old Farmer) never takes a tool.
        public ProfessionDef Profession { get; private set; }
        private ProfessionDef BornProfession => def != null ? def.profession : null;

        // The rack whose tool the unit is carrying, so the outing's end can hand it back. Null while
        // working as born. Compared with Unity's null on the way out: a rack sold mid-outing is a
        // destroyed component by then, and its tool simply goes with it (a new rack comes full).
        private ToolRack rack;

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

        // The speed the unit moves at once no boost is decaying: its base speed while working, a
        // fraction of it while tired. Every boost settles back to THIS, and FixedUpdate holds the unit
        // AT it (see there) — so the tired slowdown, a profession's speed, a perk, all are one number
        // read in one place.
        private float TargetSpeed => IsTired ? baseSpeed * TiredMultiplier : baseSpeed;
        private float TiredMultiplier => def != null ? def.tiredSpeedMultiplier : 1f;

        // How far the speed may drift from TargetSpeed before FixedUpdate writes it back — wide enough
        // that float noise from the solver never causes a write, far narrower than any real hit.
        private const float SpeedTolerance = 0.01f;

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
            rack = null;
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
            return stats != null ? stats.Apply(def.speed, StatId.UnitSpeed, Type) : def.speed;
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

        // A rack hands the unit its tool: from here on it works as `profession` and owes the tool to
        // `rack`. Whether the unit may take one (it must be Unassigned) is the rack's call — this only
        // records the result. The base speed is cached at launch, so it is re-resolved here for the new
        // profession; FixedUpdate moves the unit to it on the next step.
        public void Equip(ProfessionDef profession, ToolRack rack)
        {
            this.rack = rack;
            Profession = profession;
            baseSpeed = ResolveBaseSpeed();
        }

        // The outing is over — the tool goes back on its rack and the unit is what it was born as.
        // Called on every way an outing ends (house rest, despawn) so a profession can never survive
        // into the next launch; a unit that never equipped anything is unaffected. `!= null` and not
        // `?.` on purpose: only the overloaded operator sees a destroyed rack as null. No speed work:
        // the callers stop the unit anyway, and the next launch re-resolves.
        public void Unequip()
        {
            if (rack != null) rack.ReturnTool(this);
            rack = null;
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

        // Physics owns the unit's DIRECTION; its SPEED is ours. Bounciness 1 and no drag keep the
        // speed through a hit on anything static, but animals are moving kinematic bodies and a bounce
        // off one is taken in the animal's frame — head-on the unit comes away at its speed plus twice
        // the boar's, from behind at nearly nothing. So every step the magnitude is held at TargetSpeed
        // (or eased toward it while a boost decays), and a tired transition or a new profession simply
        // shows up here on the next step, with no rescale of its own anywhere else.
        private void FixedUpdate()
        {
            // The stamina clock runs whenever the unit is on the field, boosted or not. A resting unit
            // sits at zero (EnterRest), so the guard skips it for free.
            if (stamina > 0f)
            {
                stamina -= Time.fixedDeltaTime;
                if (stamina <= 0f) OnBecameTired();
            }

            // Resting inside a house: physics is off, nothing to hold. Any other unit at a standstill is
            // WEDGED — a boar pinned it to a wall and the solver killed both components — and would sit
            // there forever, since nothing else ever writes its velocity. Kick it out in a random
            // direction; the boost is over either way.
            Vector2 velocity = rb.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < 0.0001f)
            {
                launchBoostTimer = 0f;
                if (rb.simulated && TargetSpeed > 0f)
                    rb.linearVelocity = Random.insideUnitCircle.normalized * TargetSpeed;
                return;
            }

            // TargetSpeed is re-read every step, so a unit that tires mid-boost simply eases down to
            // its tired speed instead of its working one.
            float target = TargetSpeed;

            if (launchBoostTimer > 0f)
            {
                launchBoostTimer -= Time.fixedDeltaTime;
                if (launchBoostTimer > 0f)
                {
                    float factor = 1f - Mathf.Exp(-Time.fixedDeltaTime / launchTau);
                    target = Mathf.Lerp(speed, target, factor);
                }
            }

            // Write back only when something actually moved the speed — a hit on a moving body, a
            // transition, the decay — never for solver noise; the velocity set is the costly half.
            if (Mathf.Abs(speed - target) > SpeedTolerance)
                rb.linearVelocity = velocity * (target / speed);
        }

        // A bounce just happened (physics has already reflected the velocity; this runs after the
        // step). Perfect reflection keeps a head-on trajectory head-on, so a unit that once comes off a
        // corner or a double contact travelling perpendicular to two parallel fences shuttles between
        // them forever. Two corrections to the DIRECTION only — FixedUpdate keeps the speed:
        //   - never leave closer than minBounceAngle to the surface normal: a near-perpendicular exit
        //     is bent out to that angle on the side it already leaned (a coin toss if it is dead on),
        //     which walks the unit along the corridor and out in a couple of crossings;
        //   - a small random turn on top, so units never settle into one shared groove.
        // Triggers (racks, fields, the market passage) are not bounces and never get here.
        private void OnCollisionEnter2D(Collision2D collision)
        {
            Vector2 velocity = rb.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < 0.0001f) return;   // wedged — FixedUpdate's kick handles it

            Vector2 dir = velocity / speed;

            if (minBounceAngle > 0f && collision.contactCount > 0)
            {
                // Oriented along the way OUT: whichever way Unity reports it, the normal a bounce
                // leaves along is the one the unit is now moving away from the surface with.
                Vector2 normal = collision.GetContact(0).normal;
                if (Vector2.Dot(normal, dir) < 0f) normal = -normal;

                float angle = Vector2.SignedAngle(normal, dir);
                if (Mathf.Abs(angle) < minBounceAngle)
                {
                    float side = angle != 0f ? Mathf.Sign(angle) : (Random.value < 0.5f ? -1f : 1f);
                    dir = Rotate(normal, side * minBounceAngle);
                }
            }

            if (bounceJitterDegrees > 0f)
                dir = Rotate(dir, Random.Range(-bounceJitterDegrees, bounceJitterDegrees));

            rb.linearVelocity = dir * speed;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        // The one working → tired transition — from the clock today, and from any future stamina spend
        // (a building charging per paying hit) tomorrow. Nothing to do with the velocity here: the next
        // FixedUpdate reads the new TargetSpeed and eases or snaps to it. Direction is kept — the unit
        // sags, it doesn't turn.
        private void OnBecameTired()
        {
            stamina = 0f;
        }
    }
}
