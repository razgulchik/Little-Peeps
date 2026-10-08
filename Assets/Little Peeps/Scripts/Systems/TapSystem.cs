using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Handles game-object taps: boosts units in an AoE radius around the cursor. A ring (TapRadiusVisual)
    // follows the cursor every frame so the player sees the boost area; it only shows during normal
    // gameplay (timeScale != 0 — build mode/pause freeze time and hide it). The pier is not clickable any
    // more: prestige starts from PrestigeButton, a UI button hanging over it. A click on UI never gets here
    // (InputHandler keeps it out of OnWorldClick), so a button pressed over the island boosts no one.
    //
    // Every live tap answers, through the SoundSystem. A tap that boosts anyone is answered by the boosted
    // units themselves (UnitDef.tapSound): the nearest first, then each next one rippleStep later — a ripple
    // spreading from the finger — and only voicesPerTap of them, so a crowd stays a little chord instead of
    // a bang. A tap that reaches nobody plays the soft missSound, so the player hears it landed even on
    // empty ground. The tap's sounds are not bounces: they claim no unit's hit.
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
        [Tooltip("Empty = taps are silent.")]
        [SerializeField] private SoundSystem soundSystem;

        [Tooltip("A tap that reaches no unit. Empty = such a tap is silent.")]
        [SerializeField] private SoundDef missSound;

        [Tooltip("How many of the boosted units answer a tap: the nearest ones. The rest are boosted in silence.")]
        [SerializeField, Min(1)] private int voicesPerTap = 3;

        [Tooltip("Seconds between one answering unit's voice and the next: the ripple. 0 = all at once.")]
        [SerializeField, Min(0f)] private float rippleStep = 0.05f;

        private struct Tapped
        {
            public Unit unit;
            public float distance;   // from the tap point — the ripple's order
        }

        private RunContext runContext;
        private readonly List<Tapped> tapped = new();   // reused per tap: the units it boosted, each once

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

            // Units: AoE boost — every unit whose collider overlaps the tap radius gets boosted, once
            // however many of its colliders overlap. The collider lives on a child ("Physics"), so resolve
            // the Unit via GetComponentInParent.
            var (speedMult, radius, duration, refresh) = GetBoostParams();
            var hits = Physics2D.OverlapCircleAll(worldPos, radius);
            tapped.Clear();
            foreach (var hit in hits)
            {
                var unit = hit.GetComponentInParent<Unit>();
                if (unit == null || WasTapped(unit)) continue;
                unit.Boost(speedMult, duration, refresh);
                tapped.Add(new Tapped { unit = unit, distance = Vector2.Distance(worldPos, unit.transform.position) });
            }

            PlayTapSounds();
        }

        private bool WasTapped(Unit unit)
        {
            for (int i = 0; i < tapped.Count; i++)
                if (tapped[i].unit == unit) return true;
            return false;
        }

        // The tap's answer (see the class comment): the boosted units' voices nearest first, one
        // rippleStep apart, or the miss sound when nobody was reached.
        private void PlayTapSounds()
        {
            if (soundSystem == null) return;
            if (tapped.Count == 0)
            {
                soundSystem.Play(missSound);
                return;
            }

            tapped.Sort(static (a, b) => a.distance.CompareTo(b.distance));
            int voices = Mathf.Min(voicesPerTap, tapped.Count);
            for (int i = 0; i < voices; i++)
            {
                var def = tapped[i].unit.def;
                if (def != null) soundSystem.Play(def.tapSound, i * rippleStep);
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
