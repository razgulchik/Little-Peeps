using System.Collections;
using UnityEngine;

public class Unit : MonoBehaviour
{
    public UnitDef def;

    public UnitType Type => def != null ? def.unitType : default;

    // World-space radius of the unit's collider (used for spawn-clearance math).
    public float Radius => bodyCollider != null ? bodyCollider.bounds.extents.x : 0f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D bodyCollider;
    private float baseSpeed;
    private Coroutine boostCoroutine;

    private IslandSystem island;   // for the on-island containment backstop (injected on spawn)
    private Vector2 lastInside;    // last known on-island position, restored if the unit escapes

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

    // Injected by SpawnSystem on spawn so the unit can keep itself on the island (containment backstop).
    public void SetIsland(IslandSystem islandSystem) => island = islandSystem;

    // Launch in a direction. The unit leaves at baseSpeed * speedMultiplier and a decaying
    // braking force eases its speed back down to baseSpeed over ~boostDuration seconds
    // (see FixedUpdate). Direction is preserved through bounces.
    public void Launch(Vector2 direction, float speedMultiplier = 1f, float boostDuration = 0f)
    {
        if (def != null) baseSpeed = def.speed;

        // Coming back out of rest: re-enable physics and visuals.
        rb.simulated = true;
        lastInside = rb.position;   // seed the containment backstop with the (on-island) spawn point
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
        if (boostCoroutine != null) { StopCoroutine(boostCoroutine); boostCoroutine = null; }

        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    private void FixedUpdate()
    {
        KeepOnIsland();

        // Tap boost (if any) owns the velocity while it runs.
        if (launchBoostTimer <= 0f || boostCoroutine != null) return;

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

    // Containment backstop: if the unit has ended up off the island (e.g. tunneled through the
    // boundary at boost speed, or got launched toward a near edge), snap it back to its last
    // on-island position and send it back inward. The bounce wall handles normal containment; this
    // guarantees nothing escapes for good. Cheap: one grid lookup per physics step.
    private void KeepOnIsland()
    {
        if (island == null || !rb.simulated) return;
        var grid = island.Grid;
        if (grid == null) return;

        if (grid.GetCell(grid.WorldToGrid(rb.position)) != null)
            lastInside = rb.position;                 // still on the island — remember where
        else
        {
            rb.position = lastInside;                 // escaped — pull it back to the last good spot
            rb.linearVelocity = -rb.linearVelocity;   // and send it back inward
        }
    }

    // Multiply speed for duration seconds; re-calling mid-boost resets the timer.
    // radius is the AoE tap radius handled by TapSystem, not used here.
    public void Boost(float speedMultiplier, float radius, float duration)
    {
        if (boostCoroutine != null) StopCoroutine(boostCoroutine);
        boostCoroutine = StartCoroutine(BoostRoutine(speedMultiplier, duration));
    }

    private IEnumerator BoostRoutine(float speedMultiplier, float duration)
    {
        rb.linearVelocity = rb.linearVelocity.normalized * baseSpeed * speedMultiplier;
        yield return new WaitForSeconds(duration);
        rb.linearVelocity = rb.linearVelocity.normalized * baseSpeed;
        boostCoroutine = null;
    }
}
