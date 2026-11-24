using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // How much one resource type counts towards the prestige payout. Mirrors ResourceCost: a
    // serializable pair, authored as a list in the inspector.
    [Serializable]
    public class ResourceWeight
    {
        public ResourceType resourceType;
        public float weight = 1f;
    }

    // The prestige payout as tuning data rather than as code. Two RECORD terms —
    //
    //     age term     = sum of agePoints over the transitions bought (currentAge - RunContext.FirstAge)
    //     harvest term = floor(coefficient * pow(weighted harvest, exponent))
    //
    // — of which a run is paid only the part that BEATS the profile's record for that term (see
    // MetaContext): both answer "how far did this run get". And one term paid in full by EVERY run,
    // with no record behind it:
    //
    //     time term    = pointsPerInterval * whole intervals of minutesPerInterval played
    //
    // The age term measures progress up the ladder; the harvest term measures how much the village
    // actually produced along the way; the time term keeps a run that beats nothing worth something.
    //
    // Only WHOLE intervals pay, and the remainder is lost with the run. That is what stops the time term
    // from being farmed: carried over, it could be gathered by idling through first cycles and taking the
    // wrath, without ever paying an offering.
    //
    // Only PRODUCTION counts: RunContext.harvested is fed by ResourceSystem.AddHarvest and nothing else,
    // so a build → sell → build cycle cannot farm points off its own refunds. See the ledger's comment
    // in RunContext.
    //
    // A plain [Serializable] class, not a handful of fields on PrestigeSystem: Points() is then reachable
    // from the EditMode tests without a GameObject, which is what lets the offline harness run them.
    [Serializable]
    public class PrestigeFormula
    {
        [Header("Ages")]
        [Tooltip("Points for entering each age, in order. Element 0 is the Age II (the Age I is where every " +
                 "run starts, it pays nothing), element 1 the Age III, and so on. Ages past the end of the " +
                 "list pay the last element, so a single 1 means one point per age. Empty = ages pay nothing.")]
        public List<int> agePoints = new() { 1 };

        [Header("Time")]
        [Tooltip("Every this many minutes of play pay Points Per Interval, in every run. Only normal play " +
                 "counts (building included): the zone, perk, offering and Ancestral Wisdom screens and the " +
                 "age transition do not. An unfinished interval is lost with the run. 0 = time pays nothing.")]
        [Min(0f)] public float minutesPerInterval = 5f;

        [Tooltip("Points per whole interval played.")]
        [Min(0)] public int pointsPerInterval = 1;

        [Header("Harvest")]
        [Tooltip("Per-type weight in the harvest sum. A type left out of this list uses defaultWeight, " +
                 "so an empty list means a raw sum in which 1 Stone equals 1 Coin.")]
        public List<ResourceWeight> weights = new();

        [Tooltip("Weight for resource types absent from the list above.")]
        public float defaultWeight = 1f;

        [Header("Curve")]
        [Tooltip("Scales the harvest term, after the exponent has been applied.")]
        public float coefficient = 1f;

        [Tooltip("Exponent on the weighted harvest total. 0.5 (square root) makes a run that harvests " +
                 "four times as much worth only twice as much, so several short runs beat one endless " +
                 "one. 1 makes the term linear, which reverses that.")]
        [Min(0f)] public float exponent = 0.5f;

        // What this run is worth to this profile: each record term, less what that term has already paid
        // out, plus the time term in full. Pure — no scene, no side effects, no mutation of either argument —
        // so the confirmation screen (B2) can call it every frame to show a projection. A null run is worth 0.
        //
        // The profile is a required argument rather than an optional one on purpose: a caller that
        // forgot it would silently pay both records out a second time, which is the exact bug the
        // records exist to prevent. A null profile is treated as a fresh one — being wrong in the
        // player's favour beats throwing on a save that failed to load.
        //
        // Each record term is clamped on its own, not after summing: beating your harvest record must pay
        // even when the run stopped short of your best age, and vice versa.
        public int Points(RunContext run, MetaContext profile)
        {
            if (run == null) return 0;

            int agePaid     = profile != null ? Mathf.Max(0, profile.agePointsAwarded)     : 0;
            int harvestPaid = profile != null ? Mathf.Max(0, profile.harvestPointsAwarded) : 0;

            return Mathf.Max(0, AgePoints(run) - agePaid)
                 + Mathf.Max(0, HarvestPoints(run) - harvestPaid)
                 + TimePoints(run);
        }

        // The GROSS age term — what the run's age is worth before the profile's record is subtracted.
        // Public because CashIn needs it to raise that record, and a payout breakdown would need it to
        // show the payout as "earned, minus already paid" rather than as one unexplained number.
        public int AgePoints(RunContext run)
        {
            if (run == null || agePoints == null || agePoints.Count == 0) return 0;

            int total = 0;
            int transitions = run.currentAge - RunContext.FirstAge;
            for (int i = 0; i < transitions; i++)
                total += Mathf.Max(0, agePoints[Mathf.Min(i, agePoints.Count - 1)]);
            return total;
        }

        // What the run's play time is worth: whole intervals only. No record is subtracted from it, so it is
        // both gross and net.
        public int TimePoints(RunContext run)
        {
            if (run == null || minutesPerInterval <= 0f) return 0;

            int intervals = (int)(run.playSeconds / (minutesPerInterval * 60.0));
            return Mathf.Max(0, pointsPerInterval * intervals);
        }

        // The GROSS harvest term. Same reasoning as AgePoints.
        public int HarvestPoints(RunContext run)
        {
            float harvest = WeightedHarvest(run);

            // Guard the base rather than trust it: a negative total is impossible today (harvest only
            // ever adds), but Mathf.Pow of a negative base is NaN, and NaN floors to int.MinValue.
            float bonus = harvest > 0f ? coefficient * Mathf.Pow(harvest, exponent) : 0f;

            return Mathf.Max(0, Mathf.FloorToInt(bonus));
        }

        // Everything the run harvested, each type scaled by its weight.
        public float WeightedHarvest(RunContext run)
        {
            if (run == null || run.harvested == null) return 0f;

            float total = 0f;
            foreach (var entry in run.harvested)
                total += entry.Value * WeightOf(entry.Key);
            return total;
        }

        // The authored weight for a type, or defaultWeight when it isn't listed. Falling back to
        // defaultWeight rather than to zero matters: otherwise authoring a weight for ONE type would
        // silently stop every other type from counting.
        public float WeightOf(ResourceType type)
        {
            if (weights != null)
                for (int i = 0; i < weights.Count; i++)
                    if (weights[i] != null && weights[i].resourceType == type)
                        return weights[i].weight;

            return defaultWeight;
        }
    }
}
