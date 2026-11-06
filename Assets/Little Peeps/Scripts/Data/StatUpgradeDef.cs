using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // A meta perk whose whole effect is a list of stat modifiers — the common case, and the one that needs no
    // code: authoring the asset IS writing the upgrade. Anything a modifier can say, a meta perk can grant
    // from the start of a run.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Stat Upgrade")]
    public class StatUpgradeDef : GlobalUpgradeDef
    {
        [Tooltip("Bonuses this upgrade grants for the whole run. Leave a modifier's Source Scope empty to " +
                 "affect every source.")]
        public List<StatModifier> modifiers;

        // Every modifier, one line each — the same words a perk card uses.
        public override string GeneratedDescription() => StatModifierText.Describe(modifiers);

        public override void ApplyToRun(RunContext run)
        {
            if (run == null || modifiers == null) return;

            run.stats.Add(modifiers);
        }
    }
}
