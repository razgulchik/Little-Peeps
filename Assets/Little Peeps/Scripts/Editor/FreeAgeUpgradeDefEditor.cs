using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for FreeAgeUpgradeDef: the description-override affordance from BonusOverrideEditor.
    [CustomEditor(typeof(FreeAgeUpgradeDef))]
    public class FreeAgeUpgradeDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "upgrade";

        protected override string Generated(Object target) =>
            ((FreeAgeUpgradeDef)target).GeneratedDescription();

        protected override bool HasModifiers(Object target) => true;
    }
}
