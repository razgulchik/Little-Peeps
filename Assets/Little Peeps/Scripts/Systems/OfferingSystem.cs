using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The offering cycle: a clock per cycle and the quota the gods want by its end. Pays an offering out of
    // the stock and moves on to the next cycle — and nothing else. It does NOT decide when either happens:
    // GameplayContainerState advances the clock (only in normal play, so every screen that pauses the game
    // pauses the cycle by construction) and opens the end-of-cycle window (OfferingEndState) when the time
    // is up — the only place an offering is paid.
    //
    // The cycle's state lives on RunContext, so this system holds nothing a new run has to reset.
    public class OfferingSystem : MonoBehaviour
    {
        [Tooltip("The cycles: length, quotas, special ones. Empty = the offering cycle is switched off.")]
        [SerializeField] private OfferingScheduleDef schedule;
        [SerializeField] private ResourceSystem resourceSystem;

        private RunContext run;

        // The current cycle's quota, built once per cycle rather than on every read — the HUD asks every
        // frame. Tagged with the run and cycle it was built for, so it can never be served to another.
        private List<ResourceCost> quota = new();
        private RunContext quotaRun;
        private int quotaCycle;

        public void Initialize(RunContext context) => run = context;

        // Re-bind on every run after the first, AgeSystem's reason: a prestige must not leave the clock
        // running on the finished run's cycle.
        private void OnEnable()  => EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
        private void OnDisable() => EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);

        private void OnRunStarted(RunStartedEvent e) => run = e.Run;

        private void Start()
        {
            if (schedule == null || schedule.IsEmpty)
                Debug.LogWarning($"[OfferingSystem] '{name}' has no Offering Schedule (or an empty one) — the " +
                                 "offering cycle is off: no quotas, no perks from offerings, no wrath.", this);
            if (resourceSystem == null)
                Debug.LogError($"[OfferingSystem] '{name}' has no ResourceSystem — no offering can be paid.", this);
        }

        // Whether there is a cycle to run at all: a schedule with something in it, and a run.
        public bool IsActive => schedule != null && !schedule.IsEmpty && run != null;

        public int Cycle => run != null ? run.offeringCycle : 0;

        // The current cycle's length — its own number of segments, so it changes from cycle to cycle.
        public float Duration => schedule != null && run != null ? schedule.SecondsOf(run.offeringCycle) : 0f;
        public float Elapsed => run != null ? run.offeringElapsed : 0f;
        public float TimeLeft => run != null ? Mathf.Max(0f, Duration - run.offeringElapsed) : 0f;

        // The time strip's unit: one segment in seconds, the same for every cycle. Every cycle is a whole
        // number of segments, so every cycle starts and ends on a tick of the strip.
        public float SegmentSeconds => schedule != null ? schedule.segmentSeconds : 0f;

        // How long a cycle lasts, in segments — the strip reads ahead to place the offerings after this one.
        public int SegmentsOf(int cycle) => schedule != null ? schedule.SegmentsOf(cycle) : 0;

        // Segments the run has gone through before a cycle starts: the strip's year count at that cycle's
        // start, so its years run on from the run's start, across cycles.
        public int StartSegmentOf(int cycle) => schedule != null ? schedule.StartSegmentOf(cycle) : 0;
        public bool IsOver => IsActive && run.offeringElapsed >= Duration;
        public bool IsSpecial => IsActive && schedule.IsSpecial(run.offeringCycle);

        // The perk pool this cycle's offering pays out of. Read it BEFORE NextCycle: after that it is the
        // next cycle's.
        public PerkGrade RewardGrade => IsSpecial ? PerkGrade.Special : PerkGrade.Common;

        // What the gods want this cycle. Read-only: it is the system's cached list.
        public IReadOnlyList<ResourceCost> Quota
        {
            get
            {
                if (!IsActive) return System.Array.Empty<ResourceCost>();
                if (quotaRun != run || quotaCycle != run.offeringCycle)
                {
                    quota = schedule.QuotaOf(run.offeringCycle);
                    quotaRun = run;
                    quotaCycle = run.offeringCycle;
                }
                return quota;
            }
        }

        // The quota can be paid right now: all of it is in the stock.
        public bool CanOffer
        {
            get
            {
                if (!IsActive || resourceSystem == null) return false;

                var q = Quota;
                for (int i = 0; i < q.Count; i++)
                    if (resourceSystem.GetResource(q[i].resourceType) < q[i].amount) return false;
                return true;
            }
        }

        // Run the clock. Stops at the end of the cycle rather than past it: what happens then is the
        // caller's to decide, and the clock must still read "time's up" when it does.
        public void Advance(float deltaTime)
        {
            if (!IsActive || deltaTime <= 0f) return;
            run.offeringElapsed = Mathf.Min(run.offeringElapsed + deltaTime, Duration);
        }

        // Pay the whole quota out of the stock. False — and nothing spent — when it cannot be paid in full.
        public bool TryOffer()
        {
            if (!CanOffer) return false;

            var q = Quota;
            for (int i = 0; i < q.Count; i++)
                resourceSystem.AddResource(q[i].resourceType, -q[i].amount);

            Debug.Log($"[Offering] Cycle {run.offeringCycle} offered{(IsSpecial ? " (special)" : "")}.");
            return true;
        }

        // The next cycle starts: a fresh clock.
        public void NextCycle()
        {
            if (run == null) return;

            run.offeringCycle++;
            run.offeringElapsed = 0f;
        }
    }
}
