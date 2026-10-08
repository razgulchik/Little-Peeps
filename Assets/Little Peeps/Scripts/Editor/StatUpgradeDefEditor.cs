using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for StatUpgradeDef: the bonus-override affordance from BonusOverrideEditor, on description —
    // the same preview and "Override with this" / "Back to auto" a Stat Perk has. The generated text is for
    // ONE level; the screen's "1/3" says how many.
    [CustomEditor(typeof(StatUpgradeDef))]
    public class StatUpgradeDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "upgrade";

        protected override string Generated(Object target) => ((StatUpgradeDef)target).GeneratedDescription;

        protected override bool HasModifiers(Object target)
        {
            var modifiers = ((StatUpgradeDef)target).modifiers;
            return modifiers != null && modifiers.Count > 0;
        }
    }
}
