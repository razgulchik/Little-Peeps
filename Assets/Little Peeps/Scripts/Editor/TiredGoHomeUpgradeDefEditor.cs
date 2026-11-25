using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for TiredGoHomeUpgradeDef: the description-override affordance from BonusOverrideEditor.
    [CustomEditor(typeof(TiredGoHomeUpgradeDef))]
    public class TiredGoHomeUpgradeDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "upgrade";

        protected override string Generated(Object target) =>
            ((TiredGoHomeUpgradeDef)target).GeneratedDescription();

        protected override bool HasModifiers(Object target) => true;
    }
}
