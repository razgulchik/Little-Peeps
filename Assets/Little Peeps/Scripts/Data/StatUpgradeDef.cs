using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // A meta perk whose whole effect is a list of stat modifiers — the common case, and the one that needs no
    // code: authoring the asset IS writing the upgrade. Built like a Stat Perk: one step per level on every
    // modifier (LevelledModifier), the step lists following Levels in OnValidate — so anything a perk can
    // grant in levels, a meta perk can grant from the start of a run.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Stat Upgrade")]
    public class StatUpgradeDef : GlobalUpgradeDef
    {
        [Tooltip("Bonuses this upgrade grants for the whole run, one step per level — what THAT level adds, " +
                 "not the total so far. Leave a modifier's Source Scope empty to affect every source.")]
        public List<LevelledModifier> modifiers;

        // Every modifier, one line each — the same words a perk card uses.
        public override string GeneratedDescription(int level) => StatModifierText.Describe(modifiers, level);

        public override string GeneratedTotal(int level) => StatModifierText.DescribeTotal(modifiers, level);

        private void OnValidate() => LevelledModifier.FitSteps(modifiers, MaxLevel);

        // Every step up to `level`, in one Add through the same RunStats a perk uses: flat and percent both
        // sum there, so the total of the steps IS the bonus.
        public override void ApplyToRun(RunContext run, int level)
        {
            if (run == null || modifiers == null || level <= 0) return;

            var total = new List<StatModifier>(modifiers.Count);
            for (int i = 0; i < modifiers.Count; i++) total.Add(modifiers[i].With(modifiers[i].TotalAt(level)));
            run.stats.Add(total);
        }
    }
}
