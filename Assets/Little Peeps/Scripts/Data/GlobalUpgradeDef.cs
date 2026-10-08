using UnityEngine;

namespace LittlePeeps
{
    // A meta perk: bought with prestige points on the screen after a prestige, in levels, and in effect from
    // the start of every run after that. The prestige counterpart of PerkDef, with the same split: this base
    // holds what every meta perk has, and each KIND of effect is its own subclass — StatUpgradeDef for
    // everything a stat modifier can say, which is most of them. A kind that is not a number arrives as its
    // own subclass, one file: StartResourcesUpgradeDef, StartPerkPickUpgradeDef, StartAgeUpgradeDef.
    //
    // One prestige point buys one level, and a level repeats the effect once more: the asset describes a
    // single STEP, like the levels of a perk chain (PerkDef.requires).
    public abstract class GlobalUpgradeDef : ScriptableObject
    {
        [Tooltip("Stable key for saves: the profile stores levels by it. Must be unique across the catalogue " +
                 "and must not be empty — PrestigeSystem logs both mistakes at startup.")]
        public string id;

        [Tooltip("Name shown on the meta screen. Separate from id on purpose: the title is for the player and " +
                 "can be reworded freely, while id goes to disk and must never change.")]
        public string title;

        [Tooltip("Shown under the title, for ONE level. Leave EMPTY to let the upgrade describe itself from " +
                 "its data — a Stat Upgrade lists its modifiers — so a balance pass can never leave a stale " +
                 "line behind. Fill it only to word it by hand.")]
        [TextArea] public string description;

        [Tooltip("How many levels can be bought — the 3 in '1/3'. Each level costs one prestige point.")]
        [Min(1)] public int maxLevel = 3;

        // What the screen shows: the hand-written description when there is one, else what the upgrade can
        // say about itself from its data. Same rule as PerkDef.DescriptionText.
        public string DescriptionText =>
            !string.IsNullOrWhiteSpace(description) ? description : GeneratedDescription;

        // The text the data alone would give. Empty here: the base has no data to speak from.
        public virtual string GeneratedDescription => string.Empty;

        // Put this upgrade into a run that is being built, at a level from 1 to maxLevel. Called by
        // PrestigeSystem.ApplyUpgrades from RunManager.StartNewRun, before any system reads the run.
        public abstract void ApplyToRun(RunContext run, int level);
    }
}
