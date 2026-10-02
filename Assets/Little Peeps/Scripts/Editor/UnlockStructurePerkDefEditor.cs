using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for UnlockStructurePerkDef: the same description-override affordance StatPerkDef has
    // (BonusOverrideEditor), generated from the building it opens, plus the one authoring mistake this
    // perk can carry — pointing at a building that was never locked, so picking it changes nothing.
    [CustomEditor(typeof(UnlockStructurePerkDef))]
    public class UnlockStructurePerkDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "perk";

        protected override string Generated(Object target) =>
            ((UnlockStructurePerkDef)target).GeneratedDescription;

        protected override bool HasModifiers(Object target) =>
            ((UnlockStructurePerkDef)target).structure != null;

        protected override string Note(Object target)
        {
            var structure = ((UnlockStructurePerkDef)target).structure;
            return structure != null && !structure.lockedUntilPerk
                ? $"'{structure.name}' is not marked Locked Until Perk: it is open from the start and " +
                  "this perk does nothing. Tick the box on the StructureDef."
                : null;
        }
    }
}
