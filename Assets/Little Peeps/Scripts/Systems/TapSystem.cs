using UnityEngine;

// Handles game-object taps: boosts units in AoE radius, triggers prestige on Pier click.
public class TapSystem : MonoBehaviour
{
    [SerializeField] private InputHandler inputHandler;

    private RunContext runContext;

    public void Initialize(RunContext context)
    {
        runContext = context;
    }

    private void OnEnable()  => inputHandler.OnWorldClick += OnWorldClick;
    private void OnDisable() => inputHandler.OnWorldClick -= OnWorldClick;

    private void OnWorldClick(Vector2 worldPos)
    {
        // Pier: exact point check — click anywhere on its collider triggers prestige
        var exactHit = Physics2D.OverlapPoint(worldPos);
        if (exactHit != null && exactHit.TryGetComponent<Pier>(out _))
        {
            EventBus<PrestigeTriggeredEvent>.Publish(new PrestigeTriggeredEvent());
            return;
        }

        // Units: AoE boost — all units within tap radius get boosted
        var (speedMult, radius, duration) = GetBoostParams();
        var hits = Physics2D.OverlapCircleAll(worldPos, radius);
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<Unit>(out var unit)) continue;
            unit.Boost(speedMult, radius, duration);
            EventBus<UnitBoostedEvent>.Publish(new UnitBoostedEvent
            {
                Unit           = unit,
                SpeedMultiplier = speedMult,
                Radius          = radius,
                Duration        = duration,
            });
        }
    }

    private (float speedMult, float radius, float duration) GetBoostParams()
    {
        // TODO: read from runContext once perks/upgrades are implemented
        return (2f, 0.5f, 5f);
    }
}
