using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for StartPerkPickUpgradeDef: the description-override affordance from BonusOverrideEditor.
    // The perk carries no data, so the generated line is fixed and there is always something to say.
    [CustomEditor(typeof(StartPerkPickUpgradeDef))]
    public class StartPerkPickUpgradeDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "upgrade";

        protected override string Generated(Object target) =>
            ((StartPerkPickUpgradeDef)target).GeneratedDescription;

        protected override bool HasModifiers(Object target) => true;

        protected override string Note(Object target) =>
            "Bought once: Max Level is held at 1.";
    }
}
