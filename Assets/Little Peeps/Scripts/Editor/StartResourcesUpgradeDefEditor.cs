using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for StartResourcesUpgradeDef: the description-override affordance from BonusOverrideEditor,
    // generated from the resource list. The text is for ONE level, like a Stat Upgrade's.
    [CustomEditor(typeof(StartResourcesUpgradeDef))]
    public class StartResourcesUpgradeDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "upgrade";

        protected override string Generated(Object target) =>
            ((StartResourcesUpgradeDef)target).GeneratedDescription;

        protected override bool HasModifiers(Object target)
        {
            var resources = ((StartResourcesUpgradeDef)target).resources;
            return resources != null && resources.Count > 0;
        }
    }
}
