using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The prestige economy, both halves: what a run is worth and banking it (Calculate / CashIn), and what
    // the banked points buy — the meta perks, and putting the taken ones into every new run
    // (ApplyUpgrades, called by RunManager.StartNewRun). It does not start runs itself: after a cash-in the
    // meta screen comes first, and its Play button starts the next run, so that what was just bought is in
    // place when that run is built.
    public class PrestigeSystem : MonoBehaviour
    {
        [SerializeField] private SaveSystem saveSystem;

        [Tooltip("The payout: points per age, plus a curve on everything the run harvested.")]
        [SerializeField] private PrestigeFormula formula = new();

        [Tooltip("The meta perks prestige points buy, in the order the meta screen lists them.")]
        [SerializeField] private GlobalUpgradeCatalogueDef upgrades;

        private MetaContext metaContext;

        public IReadOnlyList<GlobalUpgradeDef> Upgrades =>
            upgrades != null && upgrades.upgrades != null
                ? upgrades.upgrades
                : Array.Empty<GlobalUpgradeDef>();

        public void Initialize(MetaContext meta)
        {
            metaContext = meta;
        }

        // The catalogue holds what a save will key by, so its mistakes are checked once, at startup — the
        // same checks PerkSystem runs on the perk catalogue.
        private void Start()
        {
            ValidateCatalogue();
        }

        // What this run is worth in prestige points. Pure and side-effect free — CashIn banks exactly this, and
        // anything that wants to show the projection calls it too. The formula itself lives in
        // PrestigeFormula, so the arithmetic is inspector-tunable and testable without a scene.
        //
        // The records are read here rather than passed in: what has already been paid out belongs to the
        // profile, not to the run, so no caller has to know about them to get an honest number.
        public int Calculate(RunContext context)
        {
            return formula != null ? formula.Points(context, metaContext) : 0;
        }

        // Cash the run in: credit what it beat, raise the records it beat, persist. This is the moment a run
        // ends — from here on it is paid for and only waits, frozen under the meta screen, to be replaced.
        // It is NOT replaced here: the meta screen's Play button does that, after the spending. Returns
        // false when refused (no profile to bank into).
        //
        // No age gate: the gods' wrath ends a run in any age (OfferingEndState), and a run too young to beat
        // a record simply pays nothing.
        public bool CashIn(RunContext context)
        {
            if (context == null || metaContext == null) return false;

            // Read EVERYTHING the finished run is worth before anything moves: Calculate subtracts the
            // records as they stand now, so raising them first would pay the run nothing.
            int payout          = Calculate(context);
            int agePoints       = formula != null ? formula.AgePoints(context)     : 0;
            int harvestPoints   = formula != null ? formula.HarvestPoints(context) : 0;

            metaContext.BankPayout(payout, agePoints, harvestPoints);

            Debug.Log($"[Prestige] +{payout} (ages {agePoints}, harvest {harvestPoints}) → " +
                      $"{metaContext.prestigePoints} total, {metaContext.FreePoints} free; records now " +
                      $"{metaContext.agePointsAwarded} / {metaContext.harvestPointsAwarded}");

            SaveProfile();
            return true;
        }

        // A no-op until track C implements saving, but these are its call sites: after a cash-in, and before
        // the next run is built from what was spent.
        public void SaveProfile()
        {
            if (saveSystem != null && metaContext != null) saveSystem.Save(metaContext);
        }

        // --- Spending. The rules live in MetaContext; these pass the def's id and price. ---

        public int FreePoints => metaContext != null ? metaContext.FreePoints : 0;

        public bool IsTaken(GlobalUpgradeDef def) =>
            def != null && metaContext != null && metaContext.IsTaken(def.id);

        public bool TryTake(GlobalUpgradeDef def) =>
            def != null && metaContext != null && metaContext.TryTake(def.id, def.Cost);

        public bool TryReturn(GlobalUpgradeDef def) =>
            def != null && metaContext != null && metaContext.TryReturn(def.id);

        // Put every taken upgrade into a run that is being built. Called by RunManager.StartNewRun next to the
        // start config's modifiers — a meta perk is a run-start bonus, in the sheet before any system reads it.
        public void ApplyUpgrades(RunContext run)
        {
            if (run == null || metaContext == null) return;

            var list = Upgrades;
            for (int i = 0; i < list.Count; i++)
            {
                var def = list[i];
                if (def != null && metaContext.IsTaken(def.id)) def.ApplyToRun(run);
            }
        }

        private void ValidateCatalogue()
        {
            if (upgrades == null)
            {
                Debug.LogWarning($"PrestigeSystem on '{name}' has no upgrade catalogue — the meta screen will " +
                                 "list nothing to spend prestige points on.", this);
                return;
            }

            var list = Upgrades;
            var seen = new HashSet<string>();
            for (int i = 0; i < list.Count; i++)
            {
                var def = list[i];
                if (def == null)
                {
                    Debug.LogError($"Upgrade catalogue '{upgrades.name}' has an empty slot at index {i}.", upgrades);
                    continue;
                }

                if (string.IsNullOrEmpty(def.id))
                    Debug.LogError($"Upgrade '{def.name}' has no id — the profile keys what is taken by id, " +
                                   "so it can never be bought.", def);
                else if (!seen.Add(def.id))
                    Debug.LogError($"Upgrade '{def.name}' repeats the id '{def.id}'; ids must be unique, or " +
                                   "taking one takes both.", def);

                // Warnings, not errors: these cost a blank tip or a bare cell, not a corrupt save.
                if (string.IsNullOrEmpty(def.title))
                    Debug.LogWarning($"Upgrade '{def.name}' has no title — its tip will have no name.", def);
                if (def.icon == null)
                    Debug.LogWarning($"Upgrade '{def.name}' has no icon — its cell shows the bare frame.", def);
            }
        }
    }
}
