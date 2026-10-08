using UnityEngine;

namespace LittlePeeps
{
    // Handles game-object taps: boosts units in an AoE radius around the cursor. A ring (TapRadiusVisual)
    // follows the cursor every frame so the player sees the boost area; it only shows during normal
    // gameplay (timeScale != 0 — build mode/pause freeze time and hide it). The pier is not clickable any
    // more: prestige starts from PrestigeButton, a UI button hanging over it. A click on UI never gets here
    // (InputHandler keeps it out of OnWorldClick), so a button pressed over the island boosts no one.
    //
    // Every live tap clicks (tapClip), whether it reaches anyone or not: the player hears that the tap
    // landed even on empty ground. The AudioSource is made here, so the clip is the only thing to wire.
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

        [Header("Sound")]
        [Tooltip("Played on every tap in live gameplay. Empty = taps are silent.")]
        [SerializeField] private AudioClip tapClip;
        [SerializeField, Range(0f, 1f)] private float tapVolume = 0.6f;

        [Tooltip("Random pitch spread per tap, ± this much (0.05 = ±5%), so a run of taps doesn't sound " +
                 "like one click repeated. 0 = always the same.")]
        [SerializeField, Range(0f, 0.5f)] private float tapPitchJitter = 0.05f;

        private RunContext runContext;
        private AudioSource tapVoice;

        private void Awake()
        {
            tapVoice = gameObject.AddComponent<AudioSource>();
            tapVoice.playOnAwake = false;
            tapVoice.spatialBlend = 0f;   // heard the same wherever the camera is
        }

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

            PlayTapSound();

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

        // PlayOneShot, so a quick run of taps overlaps instead of each cutting the last one off. Pitch
        // belongs to the source, so a new tap bends the tail of the one before it too — a click is too
        // short for that to be heard.
        private void PlayTapSound()
        {
            if (tapClip == null) return;
            tapVoice.pitch = 1f + Random.Range(-tapPitchJitter, tapPitchJitter);
            tapVoice.PlayOneShot(tapClip, tapVolume);
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
