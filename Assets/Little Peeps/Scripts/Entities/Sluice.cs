using UnityEngine;

namespace LittlePeeps
{
    // The iron-sand sluice: black sand settles in its tray by itself, a stage at a time, and a WORKING
    // Miner who hits it washes out the Metal of the stage the tray has reached — all of it at once. Each
    // stage pays more than the one before (1, 3, 6), so the tray rewards waiting: a Miner passing every
    // ten seconds takes 1 three times in half a minute, one well-spaced visit takes 6. A full tray
    // settles nothing more and waits for pickup.
    //
    // The mill's way, with a table instead of a count. The resource is the ResourceSource next to this:
    // its def (Never — the tray runs empty, never out) says who may wash it out and what ONE unit of the
    // table is worth to them, and it credits the payout and fires the pickup effect. This component is
    // its add-on:
    //   IHitGate    — a hit on an empty tray is a plain bounce;
    //   IYieldScale — a paying hit pays the def's amount × the table's entry for the stage reached;
    //   Paid        — and only then does settling start over, from nothing: the part-settled stage is
    //                 washed out with the rest. A worker the def doesn't list bounces off without paying,
    //                 so the sand stays for a Miner. A tired worker never gets here: CollisionTarget stops
    //                 it before the gates.
    //
    // Whole stages, not a running amount, like the mill's sacks: a Miner bouncing off twice in a row
    // never collects "+0.03", and the sand you see in the tray is the stage that pays, one to one
    // (SluiceView). While the tray is full the clock stands — nothing settles in advance.
    //
    // The clock and the table live here, on the component, not in a def or the stats — one kind of
    // sluice; a perk that wants one of them turns it into a stat then (the ForgeHeat way). Game time,
    // like every production clock: a paused game settles nothing.
    [RequireComponent(typeof(ResourceSource))]
    public class Sluice : MonoBehaviour, IHitGate, IYieldScale, IRunState
    {
        [Header("Settling")]
        [Tooltip("Seconds for the sand to settle one stage. The clock only runs while the tray isn't full.")]
        [SerializeField, Min(0.01f)] private float secondsPerStage = 10f;

        [Tooltip("What washing out pays at each stage, from the first, in units of the def's amount. " +
                 "The last stage is a full tray.")]
        [SerializeField] private int[] payoutByStage = { 1, 3, 6 };

        private ResourceSource source;
        private int stage;                        // whole stages settled since the last washing
        private float progress;                   // seconds settled toward the next stage

        // For presentation (the sand in the tray).
        public int Stage => stage;
        public int Stages => payoutByStage.Length;
        public bool IsFull => stage >= Stages;

        private void Awake()
        {
            // RequireComponent only adds the source when this component is added; a prefab made before that
            // can still lack it, and then there is nobody to pay.
            source = GetComponent<ResourceSource>();
            if (source == null)
                Debug.LogError($"Sluice on '{name}' needs a ResourceSource on the same object — it pays nothing.", this);
        }

        private void OnEnable() { if (source != null) source.Paid += OnPaid; }
        private void OnDisable() { if (source != null) source.Paid -= OnPaid; }

        private void Start()
        {
            var def = source != null ? source.Def : null;
            if (def != null && def.depletion != Depletion.Never)
                Debug.LogError($"Sluice on '{name}': '{def.name}' must be Never — the tray runs empty, never out; " +
                               "used up, it would stop taking hits (and maybe lose its body) for the regrow time.", this);
            if (Stages == 0)
                Debug.LogError($"Sluice on '{name}' has no stages in Payout By Stage — it never pays.", this);
        }

        // IHitGate — a plain look, nothing spent: settling restarts on Paid, once the hit has really paid.
        public bool TryConsume(Unit unit, in HitReport hit) => stage > 0;

        // IYieldScale — the def's amount is per unit of the table.
        public float Factor(Unit unit) => Payout(payoutByStage, stage);

        // What washing out pays at `stage`, in units of the def's amount: nothing before the first stage.
        public static float Payout(int[] table, int stage)
            => table != null && stage > 0 && stage <= table.Length ? table[stage - 1] : 0f;

        // The clock as numbers: `seconds` more settling from (stage, progress). A long step may settle more
        // than one stage, never past full; reaching full drops the part-settled stage, so a full tray banks
        // nothing for later.
        public static void Settle(ref int stage, ref float progress, float seconds, float secondsPerStage, int stages)
        {
            if (stage >= stages) return;

            progress += seconds;
            while (progress >= secondsPerStage && stage < stages)
            {
                progress -= secondsPerStage;
                stage++;
            }
            if (stage >= stages) progress = 0f;
        }

        private void OnPaid()
        {
            stage = 0;
            progress = 0f;
        }

        // IRunState — a saved run: the stage reached and the time toward the next one.
        public string StateKey => "sluice";

        public void SaveState(System.Collections.Generic.List<float> into)
        {
            into.Add(stage);
            into.Add(progress);
        }

        public void LoadState(System.Collections.Generic.IReadOnlyList<float> from)
        {
            if (from == null || from.Count < 2) return;

            stage = Mathf.Clamp(Mathf.RoundToInt(from[0]), 0, Stages);
            progress = stage >= Stages ? 0f : Mathf.Clamp(from[1], 0f, secondsPerStage);
        }

        private void Update() => Settle(ref stage, ref progress, Time.deltaTime, secondsPerStage, Stages);
    }
}
