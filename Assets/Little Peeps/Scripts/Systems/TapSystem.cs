using UnityEngine;

// Handles game-object taps: boosts units in an AoE radius around the cursor, triggers prestige on Pier
// click. A ring (TapRadiusVisual) follows the cursor every frame so the player sees the boost area; it
// only shows during normal gameplay (timeScale != 0 — build mode/pause freeze time and hide it).
public class TapSystem : MonoBehaviour
{
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private TapRadiusVisual radiusVisual;

    [Header("Boost (placeholder until perks/upgrades drive these)")]
    [SerializeField] private float tapRadius = 0.5f;
    [SerializeField] private float boostSpeedMultiplier = 2f;
    [SerializeField] private float boostDuration = 5f;

    private RunContext runContext;

    public void Initialize(RunContext context)
    {
        runContext = context;
    }

    private void OnEnable()  => inputHandler.OnWorldClick += OnWorldClick;
    private void OnDisable() => inputHandler.OnWorldClick -= OnWorldClick;

    // Drive the cursor ring: visible & following the mouse only in live gameplay.
    private void Update()
    {
        if (radiusVisual == null) return;

        bool active = Time.timeScale != 0f && inputHandler.HasMouse;
        radiusVisual.SetVisible(active);
        if (!active) return;

        radiusVisual.SetRadius(tapRadius);
        radiusVisual.transform.position = inputHandler.WorldMousePosition;
    }

    private void OnWorldClick(Vector2 worldPos)
    {
        // Build mode pauses the game (timeScale 0) and the PlacementController owns clicks then —
        // don't boost units or trigger the pier while placing structures.
        if (Time.timeScale == 0f) return;

        // Pier: exact point check — click anywhere on its collider triggers prestige
        var exactHit = Physics2D.OverlapPoint(worldPos);
        if (exactHit != null && exactHit.GetComponentInParent<Pier>() != null)
        {
            EventBus<PrestigeTriggeredEvent>.Publish(new PrestigeTriggeredEvent());
            return;
        }

        // Units: AoE boost — every unit whose collider overlaps the tap radius gets boosted.
        // The collider lives on a child ("Physics"), so resolve the Unit via GetComponentInParent.
        var (speedMult, radius, duration) = GetBoostParams();
        var hits = Physics2D.OverlapCircleAll(worldPos, radius);
        foreach (var hit in hits)
        {
            var unit = hit.GetComponentInParent<Unit>();
            if (unit == null) continue;
            unit.Boost(speedMult, duration);
            EventBus<UnitBoostedEvent>.Publish(new UnitBoostedEvent
            {
                Unit            = unit,
                SpeedMultiplier = speedMult,
                Radius          = radius,
                Duration        = duration,
            });
        }
    }

    private (float speedMult, float radius, float duration) GetBoostParams()
    {
        // TODO: fold in runContext perks/upgrades once they're implemented.
        return (boostSpeedMultiplier, tapRadius, boostDuration);
    }
}
