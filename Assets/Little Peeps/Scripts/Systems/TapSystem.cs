using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // What a tap does: every unit it is given is boosted — or, if it is out of stamina, sent straight home instead
    // (Unit.TryGoHome), unless no house has room for it. WHOM a tap reaches and WHEN a press is one is the bare hand's
    // business (BareHand): it owns the button and the ring, so nothing here asks whose the click is.
    //
    // Every tap answers, through the SoundSystem. A tap that reaches anyone is answered by the reached
    // units themselves (UnitDef.tapSound): the nearest first, then each next one rippleStep later — a ripple
    // spreading from the finger — and only voicesPerTap of them, so a crowd stays a little chord instead of
    // a bang. A tap that reaches nobody plays the soft missSound, so the player hears it landed even on
    // empty ground. A unit sent home adds its puff and the house's door to its voice. The
    // tap's own sounds are not bounces: they claim no unit's hit.
    public class TapSystem : MonoBehaviour
    {
        // Radius and multiplier are BASES: perks scale them through RunStats (StatId.TapRadius,
        // StatId.TapBoostMultiplier). Duration and refreshStamina are still read as they are.
        [Header("Boost")]
        [Tooltip("The ring round the cursor, world units: how near a unit's body has to come. For now a hold picks " +
                 "up by the same ring.")]
        [SerializeField] private float tapRadius = 0.5f;
        [SerializeField] private float boostSpeedMultiplier = 2f;
        [SerializeField] private float boostDuration = 5f;

        [Tooltip("Off: a tap refills nothing; only a house refills stamina. On: a tap also refills stamina - " +
                 "a full restart, like a house launch. A future perk flips this.")]
        [SerializeField] private bool refreshStamina = false;

        [Header("Sound")]
        [Tooltip("Empty = taps are silent.")]
        [SerializeField] private SoundSystem soundSystem;

        [Tooltip("A tap that reaches no unit. Empty = such a tap is silent.")]
        [SerializeField] private SoundDef missSound;

        [Tooltip("How many of the reached units answer a tap: the nearest ones. The rest stay silent.")]
        [SerializeField, Min(1)] private int voicesPerTap = 3;

        [Tooltip("Seconds between one answering unit's voice and the next: the ripple. 0 = all at once.")]
        [SerializeField, Min(0f)] private float rippleStep = 0.05f;

        private RunContext runContext;

        public void Initialize(RunContext context) => runContext = context;

        // Re-bind after a prestige, so the radius and the multiplier read this run's perks rather than
        // those of a run that ended several prestiges ago.
        private void OnEnable() => EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
        private void OnDisable() => EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
        private void OnRunStarted(RunStartedEvent e) => runContext = e.Run;

        // The ring's radius as it is now: resolved on every read, so a perk bought mid-run grows it at once.
        public float Radius => Resolve(tapRadius, StatId.TapRadius);

        // The boost a tap gives, as it is now: the speed multiplier a unit leaves at and the seconds it eases back
        // over. The hand gives it to the units it lets go of as well (BareHand).
        public float BoostMultiplier => Resolve(boostSpeedMultiplier, StatId.TapBoostMultiplier);
        public float BoostDuration => boostDuration;

        // The tap: `units`, nearest first, each once. None is a tap too — a miss.
        public void Push(IReadOnlyList<Unit> units)
        {
            float speedMult = BoostMultiplier;

            for (int i = 0; i < units.Count; i++)
            {
                // A refilling tap leaves nobody tired, so it has nobody to send home.
                var unit = units[i];
                bool sentHome = !refreshStamina && unit.IsTired && unit.TryGoHome();
                if (!sentHome) unit.Boost(speedMult, boostDuration, refreshStamina);
            }

            PlayTapSounds(units);
        }

        // The tap's answer (see the class comment): the reached units' voices nearest first, one
        // rippleStep apart, or the miss sound when nobody was reached.
        private void PlayTapSounds(IReadOnlyList<Unit> units)
        {
            if (soundSystem == null) return;
            if (units.Count == 0)
            {
                soundSystem.Play(missSound);
                return;
            }

            int voices = Mathf.Min(voicesPerTap, units.Count);
            for (int i = 0; i < voices; i++)
            {
                var def = units[i].def;
                if (def != null) soundSystem.Play(def.tapSound, i * rippleStep);
            }
        }

        // The base as it is until Initialize has handed over a run.
        private float Resolve(float baseValue, StatId id) =>
            runContext != null ? runContext.stats.Apply(baseValue, id) : baseValue;
    }
}
