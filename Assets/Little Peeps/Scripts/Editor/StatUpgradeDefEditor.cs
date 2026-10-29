using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for StatUpgradeDef: the bonus-override affordance from BonusOverrideEditor, on description —
    // the same preview and "Override with this" / "Back to auto" a Stat Perk has, and the same note with
    // the text of every level past the first (LevelsNote).
    [CustomEditor(typeof(StatUpgradeDef))]
    public class StatUpgradeDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "upgrade";

        protected override string Generated(Object target) => ((StatUpgradeDef)target).GeneratedDescription(1);

        protected override bool HasModifiers(Object target)
        {
            var modifiers = ((StatUpgradeDef)target).modifiers;
            return modifiers != null && modifiers.Count > 0;
        }

        protected override string Note(Object target)
        {
            var upgrade = (StatUpgradeDef)target;
            return LevelsNote(upgrade.description, upgrade.MaxLevel, upgrade.GeneratedDescription);
        }
    }
}
