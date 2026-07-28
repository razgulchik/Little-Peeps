using UnityEngine;

namespace LittlePeeps
{
    // Abstract base — create concrete subclasses in code with [CreateAssetMenu] to define perks.
    // StatPerkDef covers everything expressible as stat modifiers, which is most of them; a perk that
    // needs real behaviour becomes its own subclass rather than being bent into modifier shape.
    public abstract class PerkDef : ScriptableObject
    {
        [Tooltip("Stable key for saves. Must be unique across the catalogue and must not be empty — " +
                 "PerkSystem logs both mistakes at startup.")]
        public string id;

        [Tooltip("Name shown on the selection card. Separate from id on purpose: the title is for the " +
                 "player and can be reworded freely, while id goes to disk and must never change.")]
        public string title;

        [TextArea] public string description;

        [Tooltip("Relative roll weight against the other eligible perks. 0 takes it out of the pool " +
                 "without removing it from the catalogue.")]
        public float weight = 1f;

        [Tooltip("Earliest age this perk can be offered in, so the strong ones stay out of the first " +
                 "rolls. 0 = available from the very first transition.")]
        [Min(0)] public int minAge;

        // Apply this perk's permanent effect to the current run state
        public abstract void ApplyPerk(RunContext context);
    }
}
