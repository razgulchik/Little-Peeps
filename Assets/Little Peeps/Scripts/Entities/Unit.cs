using UnityEngine;

public class Unit : MonoBehaviour
{
    public UnitDef def;

    public UnitType Type => def != null ? def.unitType : default;

    // World-space radius of the unit's collider (used for spawn-clearance math).
    public float Radius => bodyCollider != null ? bodyCollider.bounds.extents.x : 0f;
    public IslandSystem Island => island;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D bodyCollider;
    private float baseSpeed;

    private IslandSystem island;   // injected on spawn; kept for future island-aware behavior

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
        if (def != null) baseSpeed = def.speed;
    }

    // Injected by SpawnSystem on spawn. Stored for future island-aware behavior.
    public void SetIsland(IslandSystem islandSystem) => island = islandSystem;

    // Launch in a direction. The unit leaves at baseSpeed * speedMultiplier and a decaying
    // braking force eases its speed back down to baseSpeed over ~boostDuration seconds
    // (see FixedUpdate). Direction is preserved through bounces.
    public void Launch(Vector2 direction, float speedMultiplier = 1f, float boostDuration = 0f)
    {
        if (def != null) baseSpeed = def.speed;

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
