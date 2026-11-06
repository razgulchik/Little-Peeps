using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LittlePeeps
{
    // A meta perk that puts resources in the stockpile at the start of every run — the first meta perk whose
    // effect is not a stat modifier, so it is its own subclass (see GlobalUpgradeDef).
    //
    // ADDED on top of the start config's amounts, never instead of them: RunManager seeds the config first and
    // applies meta perks after. Written straight into RunContext.resources, before ResourceSystem reads them —
    // so it is NOT harvest (RunContext.harvested), and a prestige payout can never be bought with itself.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Start Resources")]
    public class StartResourcesUpgradeDef : GlobalUpgradeDef
    {
        [Tooltip("What this adds to the run's starting resources, on top of the start config's amounts.")]
        public List<ResourceCost> resources = new();

        // "+20 FOOD", one line per resource — the grammar of a stat line on a perk card.
        public override string GeneratedDescription()
        {
            if (resources == null) return string.Empty;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < resources.Count; i++)
            {
                var r = resources[i];
                if (r == null || Mathf.Approximately(r.amount, 0f)) continue;

                if (sb.Length > 0) sb.Append('\n');
                sb.Append(r.amount > 0f ? "+" : "")
                  .Append(r.amount.ToString("0.##", CultureInfo.InvariantCulture))
                  .Append(' ')
                  .Append(r.resourceType.ToString().ToUpperInvariant());
            }
            return sb.ToString();
        }

        // A type the config left out is absent from the dictionary and starts from 0, which is what
        // ResourceSystem would have defaulted it to.
        public override void ApplyToRun(RunContext run)
        {
            if (run == null || resources == null) return;

            for (int i = 0; i < resources.Count; i++)
            {
                var r = resources[i];
                if (r == null) continue;

                run.resources.TryGetValue(r.resourceType, out float current);
                run.resources[r.resourceType] = current + r.amount;
            }
        }
    }
}
