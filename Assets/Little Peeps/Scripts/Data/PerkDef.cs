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

        [Tooltip("Shown on the selection card. Leave EMPTY to let the perk describe itself from its " +
                 "data — a Stat Perk lists its modifiers, one per line — so a balance pass can never " +
                 "leave a stale line behind. Fill it only to word this card by hand. A code perk has " +
                 "no data to speak from: for it this field is the only text.")]
        [TextArea] public string description;

        // What the card shows: the hand-written description when there is one, else whatever the perk
        // can say about itself from its data. Same rule as AgeDef.BonusText, for the same reason —
        // only a card someone worded on purpose can go stale.
        public string DescriptionText =>
            !string.IsNullOrWhiteSpace(description) ? description : GeneratedDescription;

        // The text the perk's data alone would put on the card. Empty here: the base perk has no data
        // to speak from, which makes the field above its only voice. StatPerkDef overrides.
        public virtual string GeneratedDescription => string.Empty;

        [Tooltip("Shown on the selection card. Optional — a card with no icon just leaves the slot empty.")]
        public Sprite icon;

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
