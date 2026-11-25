using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for StartStructureUpgradeDef: the description-override affordance from BonusOverrideEditor,
    // generated from the structure it puts up.
    [CustomEditor(typeof(StartStructureUpgradeDef))]
    public class StartStructureUpgradeDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "upgrade";

        protected override string Generated(Object target) =>
            ((StartStructureUpgradeDef)target).GeneratedDescription();

        protected override bool HasModifiers(Object target) =>
            ((StartStructureUpgradeDef)target).structure != null;
    }
}
