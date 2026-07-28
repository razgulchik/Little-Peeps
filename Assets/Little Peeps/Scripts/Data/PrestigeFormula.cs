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

    // The prestige payout as tuning data rather than as code. Two gross terms —
    //
    //     age term     = pointsPerAge * currentAge
    //     harvest term = floor(coefficient * pow(weighted harvest, exponent))
    //
    // — of which a run is paid only the part that BEATS the profile's record for that term (see
    // MetaContext). Both terms therefore answer "how far did this run get", not "how long did it last",
    // and a run that beats nothing is worth nothing.
    //
    // The age term measures progress up the ladder; the harvest term measures how much the village
    // actually produced along the way, which is time spent weighted by how well the village was built.
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
        [Tooltip("Points per age transition. RunContext.currentAge counts transitions, starting at 0.")]
        public int pointsPerAge = 1;

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

        // What this run is worth to this profile: each term, less what that term has already paid out.
        // Pure — no scene, no side effects, no mutation of either argument — so the confirmation screen
        // (B2) can call it every frame to show a projection. A null run is worth 0.
        //
        // The profile is a required argument rather than an optional one on purpose: a caller that
        // forgot it would silently pay both records out a second time, which is the exact bug the
        // records exist to prevent. A null profile is treated as a fresh one — being wrong in the
        // player's favour beats throwing on a save that failed to load.
        //
        // Each term is clamped on its own, not after summing: beating your harvest record must pay even
        // when the run stopped short of your best age, and vice versa.
        public int Points(RunContext run, MetaContext profile)
        {
            if (run == null) return 0;

            int agePaid     = profile != null ? Mathf.Max(0, profile.agePointsAwarded)     : 0;
            int harvestPaid = profile != null ? Mathf.Max(0, profile.harvestPointsAwarded) : 0;

            return Mathf.Max(0, AgePoints(run) - agePaid)
                 + Mathf.Max(0, HarvestPoints(run) - harvestPaid);
        }

        // The GROSS age term — what the run's age is worth before the profile's record is subtracted.
        // Public because ExecutePrestige needs it to raise that record, and the B2 screen needs it to
        // show the payout as "earned, minus already paid" rather than as one unexplained number.
        public int AgePoints(RunContext run)
        {
            return run != null ? Mathf.Max(0, pointsPerAge * run.currentAge) : 0;
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
