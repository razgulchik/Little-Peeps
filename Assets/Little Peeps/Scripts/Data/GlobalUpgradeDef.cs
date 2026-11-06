using UnityEngine;

namespace LittlePeeps
{
    // A meta perk: one icon on the Ancestral Wisdom screen after a prestige, taken whole for its price in
    // prestige points and in effect from the start of every run after that. No levels: a bigger step of the
    // same bonus is another upgrade, with its own icon and price.
    //
    // The prestige counterpart of PerkDef, with the same split: this base holds what every meta perk has, and
    // each KIND of effect is its own subclass — StatUpgradeDef for everything a stat modifier can say, which
    // is most of them. A kind that is not a number arrives as its own subclass, one file:
    // StartResourcesUpgradeDef, StartPerkPickUpgradeDef, StartAgeUpgradeDef.
    public abstract class GlobalUpgradeDef : ScriptableObject
    {
        [Tooltip("Stable key for saves: the profile stores what is taken by it. Must be unique across the " +
                 "catalogue and must not be empty — PrestigeSystem logs both mistakes at startup.")]
        public string id;

        [Tooltip("Name shown in the tip on the Ancestral Wisdom screen. Separate from id on purpose: the title " +
                 "is for the player and can be reworded freely, while id goes to disk and must never change.")]
        public string title;

        [Tooltip("The picture in this upgrade's cell. Empty = the cell shows the bare frame.")]
        public Sprite icon;

        [Tooltip("Shown in the tip under the title. Leave EMPTY to let the upgrade describe itself from its " +
                 "data, so a balance pass can never leave a stale line behind. Fill it only to word it by hand.")]
        [TextArea] public string description;

        [Tooltip("Price in prestige points — the number in the corner of the cell.")]
        [Min(1)] public int cost = 1;

        // Never below one: a zero typed past [Min] would be an upgrade that costs nothing.
        public int Cost => Mathf.Max(1, cost);

        // The tip's text: the hand-written description when there is one, else what the data says.
        public string ScreenText =>
            !string.IsNullOrWhiteSpace(description) ? description : GeneratedDescription();

        // What the data alone says this upgrade gives. Empty here: the base has no data to speak from.
        public virtual string GeneratedDescription() => string.Empty;

        // Put this upgrade into a run that is being built. Called by PrestigeSystem.ApplyUpgrades from
        // RunManager.StartNewRun, before any system reads the run.
        public abstract void ApplyToRun(RunContext run);
    }
}
