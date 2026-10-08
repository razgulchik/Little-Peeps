using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // A meta perk whose whole effect is a list of stat modifiers — the common case, and the one that needs no
    // code: authoring the asset IS writing the upgrade. Same data shape as StatPerkDef and AgeDef, so anything
    // an age or a perk can grant, a meta perk can grant from the start of a run.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Stat Upgrade")]
    public class StatUpgradeDef : GlobalUpgradeDef
    {
        [Tooltip("What ONE level grants, for the whole run. Levels repeat it: +5% here is +15% at 3/3. Leave " +
                 "a modifier's Source Scope empty to affect every source.")]
        public List<StatModifier> modifiers;

        // Every modifier, one line each — the same words a perk card uses.
        public override string GeneratedDescription => StatModifierText.Describe(modifiers);

        // The step added `level` times, through the same RunStats.Add a perk uses. Flat and percent both sum
        // in RunStats, so adding the step N times IS the step × N — no scaled copies, no second formula.
        public override void ApplyToRun(RunContext run, int level)
        {
            if (run == null || modifiers == null) return;

            for (int i = 0; i < level; i++)
                run.stats.Add(modifiers);
        }
    }
}
