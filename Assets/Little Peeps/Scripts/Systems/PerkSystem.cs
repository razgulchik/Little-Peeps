using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Rolls and applies perks: a weighted draw without replacement from the authored catalogue.
    //
    // The cadence is one perk per age transition (AgeSequencer.WaitForPerkSelection), so this runs a
    // couple of dozen times per run at most — plain lists and Random.value are the right cost here.
    public class PerkSystem : MonoBehaviour
    {
        [SerializeField] private PerkCatalogueDef catalogue;

        [Tooltip("How many perks the player picks between. Fewer are offered when fewer are eligible.")]
        [SerializeField] private int choicesOffered = 3;

        private void Start()
        {
            if (catalogue == null)
            {
                Debug.LogError($"PerkSystem on '{name}' has no PerkCatalogueDef assigned — " +
                               "no perk will ever be offered.", this);
                return;
            }

            ValidateCatalogue();
        }

        // Perk ids are what a save will store, so a duplicate or an empty one is a bug that only shows
        // up much later and looks like a lost perk. Both are cheap to catch here, once, at startup —
        // the same trap the empty `id` on Tree.asset / Wheat.asset already set for ResourceSourceDef.
        private void ValidateCatalogue()
        {
            var seen = new HashSet<string>();

            for (int i = 0; i < catalogue.perks.Count; i++)
            {
                var perk = catalogue.perks[i];
                if (perk == null)
                {
                    Debug.LogError($"PerkCatalogueDef '{catalogue.name}' has an empty slot at index {i}.",
                                   catalogue);
                    continue;
                }

                if (string.IsNullOrEmpty(perk.id))
                    Debug.LogError($"Perk '{perk.name}' has no id — saves key perks by id.", perk);
                else if (!seen.Add(perk.id))
                    Debug.LogError($"Perk '{perk.name}' repeats the id '{perk.id}'; ids must be unique.",
                                   perk);

                // A warning, not an error: a missing title costs a blank card, not a corrupt save.
                if (string.IsNullOrEmpty(perk.title))
                    Debug.LogWarning($"Perk '{perk.name}' has no title — its card will read empty.", perk);
            }
        }

        // The perks to offer this transition: eligible, distinct, weighted by PerkDef.weight.
        //
        // Returns FEWER than choicesOffered when fewer are eligible, and an empty list when none are —
        // no duplicates and no filler. The caller is expected to skip the selection step entirely on an
        // empty list rather than show an empty screen.
        //
        // A fresh list each call, deliberately: this is a transient result the UI reads and turns into
        // cards, not a long-lived slot handed to subscribers. Reusing it would mutate the offer under
        // whoever is still holding it.
        public List<PerkDef> RollPerks(int currentAge, RunContext context)
        {
            var offer = new List<PerkDef>();
            if (catalogue == null) return offer;

            var candidates = new List<PerkDef>();
            var weights = new List<float>();

            for (int i = 0; i < catalogue.perks.Count; i++)
            {
                var perk = catalogue.perks[i];
                if (!IsEligible(perk, currentAge, context)) continue;
                candidates.Add(perk);
                weights.Add(perk.weight);
            }

            while (offer.Count < choicesOffered && candidates.Count > 0)
            {
                int i = PickIndex(weights, Random.value);
                if (i < 0) break;   // everything left has non-positive weight

                offer.Add(candidates[i]);

                // Drawn without replacement: the same perk must not fill two of the three cards.
                candidates.RemoveAt(i);
                weights.RemoveAt(i);
            }

            return offer;
        }

        private static bool IsEligible(PerkDef perk, int currentAge, RunContext context)
        {
            if (perk == null) return false;
            if (perk.weight <= 0f) return false;          // deliberate off-switch, not a roll of zero
            if (currentAge < perk.minAge) return false;
            if (context != null && context.perksChosen.Contains(perk)) return false;
            return true;
        }

        // Apply the perk effect and record it so it can't be rolled again this run.
        public void ApplyPerk(PerkDef perk, RunContext context)
        {
            if (perk == null || context == null) return;

            perk.ApplyPerk(context);
            context.perksChosen.Add(perk);
        }

        // Index of the weighted pick for a roll in [0, 1], or -1 when nothing is rollable.
        //
        // Pure and static so the part with the off-by-one risk can be tested against exact rolls,
        // offline, without a catalogue or a single ScriptableObject. Non-positive weights are skipped
        // rather than clamped, so they can never be selected even by a roll that lands on their slot.
        public static int PickIndex(IReadOnlyList<float> weights, float roll01)
        {
            if (weights == null || weights.Count == 0) return -1;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0f) total += weights[i];

            if (total <= 0f) return -1;

            float target = Mathf.Clamp01(roll01) * total;
            float running = 0f;
            int lastRollable = -1;

            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;

                lastRollable = i;
                running += weights[i];
                if (target < running) return i;
            }

            // roll01 == 1 lands exactly on the total, and float drift can too; both belong to the last
            // rollable entry rather than to nobody.
            return lastRollable;
        }
    }
}
