using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for StartAgeUpgradeDef: the description-override affordance from BonusOverrideEditor, plus
    // the one authoring gap this perk can carry — no biome, so the skipped ages grow no land.
    [CustomEditor(typeof(StartAgeUpgradeDef))]
    public class StartAgeUpgradeDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "upgrade";

        protected override string Generated(Object target) =>
            ((StartAgeUpgradeDef)target).GeneratedDescription;

        protected override bool HasModifiers(Object target) => true;

        protected override string Note(Object target) =>
            ((StartAgeUpgradeDef)target).zoneBiome == null
                ? "No Zone Biome: the skipped ages give their bonus and perk pick, but the island does not grow."
                : null;
    }
}
