using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for StatPerkDef: the bonus-override affordance from BonusOverrideEditor, on
    // description. A perk's card has room, so the generated text lists EVERY modifier — no note about
    // unspoken ones is needed here.
    //
    // The preview is the level I card, the one "Override with this" copies; a perk with more levels gets
    // the other levels' cards in the note (LevelsNote). StatUpgradeDefEditor does the same for meta.
    //
    // A code perk gets this inspector only when it has data to generate from (UnlockStructurePerkDef
    // has its own); for the rest the default inspector and a hand-written description are the whole story.
    [CustomEditor(typeof(StatPerkDef))]
    public class StatPerkDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "perk";

        protected override string Generated(Object target) => ((StatPerkDef)target).GeneratedDescription(1);

        protected override bool HasModifiers(Object target)
        {
            var modifiers = ((StatPerkDef)target).modifiers;
            return modifiers != null && modifiers.Count > 0;
        }

        protected override string Note(Object target)
        {
            var perk = (StatPerkDef)target;
            return LevelsNote(perk.description, perk.MaxLevel, perk.GeneratedDescription);
        }
    }
}
