using System.Collections.Generic;
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
                 "leave a stale line behind. Fill it only to word this card by hand; it is then the same " +
                 "on every level, so a perk with levels is best left to its data. A code perk with no " +
                 "data to speak from shows only this field.")]
        [TextArea] public string description;

        // What the card of `level` shows: the hand-written description when there is one, else whatever
        // the perk can say about that level from its data. Same rule as AgeDef.BonusText, for the same
        // reason — only a card someone worded on purpose can go stale.
        public string DescriptionAt(int level) =>
            !string.IsNullOrWhiteSpace(description) ? description : GeneratedDescription(level);

        // The text the perk's data alone would put on the card of `level`. Empty here: the base perk has
        // no data to speak from, which makes the field above its only voice. StatPerkDef and
        // UnlockStructurePerkDef override.
        public virtual string GeneratedDescription(int level) => string.Empty;

        // How many times the perk can be taken in one run, each time as the next level. 1 here: only a
        // perk whose data has levels (StatPerkDef) can grow.
        public virtual int MaxLevel => 1;

        [Tooltip("The large picture (icon_blessings_*, 29×29), shown on the selection card. Optional — a " +
                 "card with no icon just leaves the slot empty.")]
        public Sprite icon;

        [Tooltip("The small picture (icon_blessing_xs_*, 20×20), shown in the HUD perk panel once the perk " +
                 "is taken. A separate drawing, not the large one scaled down. Optional — a cell with no " +
                 "small icon shows the bare frame.")]
        public Sprite smallIcon;

        [Tooltip("Which pool the perk is rolled from. Common: every ordinary offering and the start-of-run " +
                 "pick. Special: only the special cycles' offerings (every N-th, set on the Offering " +
                 "Schedule). The pools never mix — a special cycle falls back to common perks only when no " +
                 "special one is left to offer.")]
        public PerkGrade grade = PerkGrade.Common;

        [Tooltip("The colour of the card's icon frame. Only a look: how often the perk is offered is its " +
                 "Weight, set by hand.")]
        public PerkRarity rarity = PerkRarity.Normal;

        [Tooltip("Relative roll weight against the other eligible perks. 0 takes it out of the pool " +
                 "without removing it from the catalogue.")]
        public float weight = 1f;

        [Tooltip("Earliest age this perk can be offered in, so the strong ones stay out of the first " +
                 "rolls. Numbered as the player sees it: 3 = offered once the run stands in the Age III. " +
                 "0 or 1 = from the very first offering.")]
        [Min(0)] public int minAge;

        [Tooltip("A perk that must already be taken this run, at any level, before this one can be " +
                 "offered — one perk opening another. Empty = no condition. Not for levels: those live " +
                 "inside one perk (Levels on a Stat Perk).")]
        public PerkDef requires;

        // The resources this perk acts on, read from its data — the card's icon row. Each once, in
        // ResourceType order, so the row does not depend on which modifier happened to name a resource
        // first. Empty for a perk that touches no resource (stamina, speed, a house slot).
        public void GetResources(List<ResourceType> into)
        {
            into.Clear();
            AddResources(into);
            into.Sort();

            for (int i = into.Count - 1; i > 0; i--)
                if (into[i] == into[i - 1]) into.RemoveAt(i);
        }

        // Add whatever the perk's data names — repeats and order do not matter, GetResources sorts them
        // out. The base perk has no data to read. StatPerkDef and UnlockStructurePerkDef override.
        protected virtual void AddResources(List<ResourceType> into) { }

        // Apply what taking `level` (1 = the first time) adds to the current run state, for the rest of
        // the run. Each level brings only its own step: the levels before it are already in the run.
        public abstract void ApplyPerk(RunContext context, int level);
    }

    // The two perk pools: what an ordinary offering earns, and what a special cycle's offering earns.
    public enum PerkGrade
    {
        Common,
        Special,
    }

    // The frame colours, lowest to highest — the usual rarity ladder (grey, white, green, blue, purple,
    // orange, red), with Pure in the blue place. Assets store the number, so reordering or inserting one
    // quietly shifts the rarity of every perk already set.
    public enum PerkRarity
    {
        Normal,
        Common,
        Uncommon,
        Pure,
        Epic,
        Legendary,
        Mythic,
    }
}
