using UnityEngine;
using UnityEngine.Serialization;

namespace LittlePeeps
{
    // A meta perk: bought with prestige points on the screen after a prestige, in levels, and in effect from
    // the start of every run after that. The prestige counterpart of PerkDef, with the same split: this base
    // holds what every meta perk has, and each KIND of effect is its own subclass — StatUpgradeDef for
    // everything a stat modifier can say, which is most of them. A kind that is not a number arrives as its
    // own subclass, one file: StartResourcesUpgradeDef, StartPerkPickUpgradeDef, StartAgeUpgradeDef.
    //
    // One prestige point buys one level. Levels are named and counted as a perk's are (StatPerkDef): `levels`,
    // MaxLevel, the text of a level — so an author meets the same words on both.
    public abstract class GlobalUpgradeDef : ScriptableObject
    {
        [Tooltip("Stable key for saves: the profile stores levels by it. Must be unique across the catalogue " +
                 "and must not be empty — PrestigeSystem logs both mistakes at startup.")]
        public string id;

        [Tooltip("Name shown on the meta screen. Separate from id on purpose: the title is for the player and " +
                 "can be reworded freely, while id goes to disk and must never change.")]
        public string title;

        [Tooltip("Shown under the title. Leave EMPTY to let the upgrade describe itself from its data — what " +
                 "the next level adds and what it comes to — so a balance pass can never leave a stale line " +
                 "behind. Fill it only to word it by hand; it is then the same on every level.")]
        [TextArea] public string description;

        [Tooltip("How many levels can be bought — the 3 in '1/3'. Each level costs one prestige point. On a " +
                 "Stat Upgrade each modifier then has one step per level, as on a Stat Perk.")]
        [FormerlySerializedAs("maxLevel")]
        [Min(1)] public int levels = 3;

        // Never below one: a zero typed past [Min] would be an upgrade nobody can buy.
        public int MaxLevel => Mathf.Max(1, levels);

        // The text of `level`: the hand-written description when there is one, else what the upgrade can say
        // about that level from its data. Same rule as PerkDef.DescriptionAt.
        public string DescriptionAt(int level) =>
            !string.IsNullOrWhiteSpace(description) ? description : GeneratedDescription(level);

        // The line under the title with `bought` levels in: what the next [+] brings — or, with every level
        // bought, what they all give together.
        public string ScreenText(int bought)
        {
            if (bought < MaxLevel) return DescriptionAt(bought + 1);
            return !string.IsNullOrWhiteSpace(description) ? description : GeneratedTotal(MaxLevel);
        }

        // What the data alone says about `level`: what it adds. Empty here: the base has no data to speak from.
        public virtual string GeneratedDescription(int level) => string.Empty;

        // What the data alone says levels 1 to `level` give together. A kind with no total of its own
        // repeats the level's text.
        public virtual string GeneratedTotal(int level) => GeneratedDescription(level);

        // Put this upgrade into a run that is being built, at a level from 1 to MaxLevel. Called by
        // PrestigeSystem.ApplyUpgrades from RunManager.StartNewRun, before any system reads the run.
        public abstract void ApplyToRun(RunContext run, int level);
    }
}
