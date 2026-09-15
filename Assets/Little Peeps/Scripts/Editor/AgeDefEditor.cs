using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for AgeDef: the bonus-override affordance from BonusOverrideEditor, on bonusOverride.
    // The card speaks for the FIRST modifier only ("one age, one bonus"), so a longer list gets a
    // note rather than more lines.
    [CustomEditor(typeof(AgeDef))]
    public class AgeDefEditor : BonusOverrideEditor
    {
        protected override string OverrideField => "bonusOverride";
        protected override string Noun => "age";

        protected override string Generated(Object target) => ((AgeDef)target).GeneratedBonusText;

        protected override bool HasModifiers(Object target)
        {
            var modifiers = ((AgeDef)target).modifiers;
            return modifiers != null && modifiers.Count > 0;
        }

        protected override string Note(Object target)
        {
            int count = ((AgeDef)target).modifiers.Count;
            return count > 1
                ? $"{count} modifiers — all of them apply, but the card only speaks for the first. " +
                  "Word the rest into the override if they should be visible."
                : null;
        }
    }
}
