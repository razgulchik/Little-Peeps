using UnityEngine;

public class Unit : MonoBehaviour
{
    public UnitDef def;

    public UnitType Type => def != null ? def.unitType : default;

    // World-space radius of the unit's collider (used for spawn-clearance math).
    public float Radius => bodyCollider != null ? bodyCollider.bounds.extents.x : 0f;
    public IslandSystem Island => island;

    // True once the unit has been roaming long enough (def.fatigueDelay) since its last launch to be
    // willing to enter a house. Spawner.OnHit checks this so a freshly launched unit ignores houses
    // until it tires out. Defaults to true (fatigueReadyTime == 0) so a never-launched unit isn't stuck.
    public bool IsTired => Time.time >= fatigueReadyTime;
    private float fatigueReadyTime;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
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
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        bodyCollider = GetComponentInChildren<Collider2D>(true);
    }

    private void OnEnable()
    {
        baseSpeed = ResolveBaseSpeed();
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

    // Launch in a direction. The unit leaves at baseSpeed * speedMultiplier and a decaying
    // braking force eases its speed back down to baseSpeed over ~boostDuration seconds
    // (see FixedUpdate). Direction is preserved through bounces.
    public void Launch(Vector2 direction, float speedMultiplier = 1f, float boostDuration = 0f)
    {
        baseSpeed = ResolveBaseSpeed();

        // Fatigue clock restarts every launch: the unit won't enter a house until this elapses.
        fatigueReadyTime = Time.time + (def != null ? def.fatigueDelay : 0f);

        // Coming back out of rest: re-enable physics and visuals.
        rb.simulated = true;
        if (spriteRenderer != null) spriteRenderer.enabled = true;

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

    // Pull the unit inside a building: stop and hide it while it rests.
    public void EnterRest()
    {
        launchBoostTimer = 0f;

        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    private void FixedUpdate()
    {
        // No active launch/tap boost → physics owns the velocity.
        if (launchBoostTimer <= 0f) return;

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
    // Re-tapping mid-boost just re-launches, resetting the decay timer. AoE/radius is owned by
    // TapSystem; this only takes the per-unit boost amount and how long it lingers.
    public void Boost(float speedMultiplier, float duration)
    {
        Vector2 dir = rb.linearVelocity.sqrMagnitude > 0.0001f
            ? rb.linearVelocity.normalized
            : Random.insideUnitCircle.normalized;
        Launch(dir, speedMultiplier, duration);
    }
}
