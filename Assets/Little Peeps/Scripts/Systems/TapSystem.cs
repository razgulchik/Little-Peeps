using UnityEngine;

namespace LittlePeeps
{
    // Handles game-object taps: boosts units in an AoE radius around the cursor. A ring (TapRadiusVisual)
    // follows the cursor every frame so the player sees the boost area; it only shows during normal
    // gameplay (timeScale != 0 — build mode/pause freeze time and hide it). The pier is not clickable any
    // more: prestige starts from PrestigeButton, a UI button hanging over it.
    public class TapSystem : MonoBehaviour
    {
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private TapRadiusVisual radiusVisual;

        // Radius and multiplier are BASES: perks scale them through RunStats (StatId.TapRadius,
        // StatId.TapBoostMultiplier). Duration and refreshStamina are still read as they are.
        [Header("Boost")]
        [SerializeField] private float tapRadius = 0.5f;
        [SerializeField] private float boostSpeedMultiplier = 2f;
        [SerializeField] private float boostDuration = 5f;

        [Tooltip("Off: a tap is speed only. A tired unit stays tired and just gets home faster; only a " +
                 "house refills stamina. On: a tap also refills stamina - a full restart, like a house " +
                 "launch. A future perk flips this.")]
        [SerializeField] private bool refreshStamina = false;

        private RunContext runContext;

        public void Initialize(RunContext context)
        {
            runContext = context;
        }

        private void OnEnable()
        {
            inputHandler.OnWorldClick += OnWorldClick;
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
        }

        private void OnDisable()
        {
            inputHandler.OnWorldClick -= OnWorldClick;
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
        }

        // Re-bind after a prestige, so the radius and the multiplier read this run's perks rather than
        // those of a run that ended several prestiges ago.
        private void OnRunStarted(RunStartedEvent e) => runContext = e.Run;

        // Drive the cursor ring: visible & following the mouse only in live gameplay.
        private void Update()
        {
            if (radiusVisual == null) return;

            bool active = Time.timeScale != 0f && inputHandler.HasMouse;
            radiusVisual.SetVisible(active);
            if (!active) return;

            // Resolved every frame, so a perk bought mid-run grows the ring at once. SetRadius rebuilds
            // the circle only when the value actually changes.
            radiusVisual.SetRadius(Resolve(tapRadius, StatId.TapRadius));
            radiusVisual.transform.position = inputHandler.WorldMousePosition;
        }

        private void OnWorldClick(Vector2 worldPos)
        {
            // Build mode pauses the game (timeScale 0) and the PlacementController owns clicks then —
            // don't boost units while placing structures.
            if (Time.timeScale == 0f) return;

            // Units: AoE boost — every unit whose collider overlaps the tap radius gets boosted.
            // The collider lives on a child ("Physics"), so resolve the Unit via GetComponentInParent.
            var (speedMult, radius, duration, refresh) = GetBoostParams();
            var hits = Physics2D.OverlapCircleAll(worldPos, radius);
            foreach (var hit in hits)
            {
                var unit = hit.GetComponentInParent<Unit>();
                if (unit == null) continue;
                unit.Boost(speedMult, duration, refresh);
            }
        }

        private (float speedMult, float radius, float duration, bool refresh) GetBoostParams()
        {
            return (Resolve(boostSpeedMultiplier, StatId.TapBoostMultiplier),
                    Resolve(tapRadius, StatId.TapRadius),
                    boostDuration, refreshStamina);
        }

        // The base as it is until Initialize has handed over a run.
        private float Resolve(float baseValue, StatId id) =>
            runContext != null ? runContext.stats.Apply(baseValue, id) : baseValue;
    }
}
