using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for StatPerkDef: the bonus-override affordance from BonusOverrideEditor, on
    // description. A perk's card has room, so the generated text lists EVERY modifier — no note about
    // unspoken ones is needed here.
    //
    // Only the stat perk gets this inspector: a code perk has no modifiers to generate from, so for
    // it the default inspector and a hand-written description are the whole story.
    [CustomEditor(typeof(StatPerkDef))]
    public class StatPerkDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "description";
        protected override string Noun => "perk";

        protected override string Generated(Object target) => ((StatPerkDef)target).GeneratedDescription;

        protected override bool HasModifiers(Object target)
        {
            var modifiers = ((StatPerkDef)target).modifiers;
            return modifiers != null && modifiers.Count > 0;
        }
    }
}
