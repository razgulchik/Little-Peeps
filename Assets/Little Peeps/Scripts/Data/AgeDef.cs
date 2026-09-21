using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // One age/epoch the player can advance into. Authored as data: what it costs and which permanent
    // bonuses it grants (via the RunStats modifier system). How the island grows is not authored here —
    // every age adds one generated zone (IslandGenerator, rules on the StartConfig).
    [CreateAssetMenu(menuName = "LittlePeeps/AgeDef")]
    public class AgeDef : ScriptableObject
    {
        [Tooltip("Shown on the transition banner. Falls back to \"Age N\" when empty.")]
        public string title;

        [Tooltip("Resources spent to advance into this age.")]
        public List<ResourceCost> resourceCost;

        [Tooltip("Permanent bonuses granted on entering this age (production, yield, speed, ...). " +
                 "Pushed into RunStats — no code needed to add a new kind of bonus, just data.")]
        public List<StatModifier> modifiers;

        [Tooltip("The bonus line on the timeline card. Leave EMPTY to describe the first modifier " +
                 "automatically — an empty field keeps the card following the data, so re-balancing " +
                 "can never leave a stale label behind. Fill it only to word one card by hand.")]
        public string bonusOverride;

        // The one bonus this age advertises. Extra modifiers stay legal and all of them apply — but
        // "one age, one bonus" is the design rule, so the card speaks for the first and anything more
        // has to be worded into the override deliberately.
        public string BonusText =>
            !string.IsNullOrWhiteSpace(bonusOverride) ? bonusOverride : GeneratedBonusText;

        // The line the data alone would put on the card — the first modifier's. Public so the
        // inspector previews exactly what the card would show, instead of keeping its own copy of
        // the "first modifier" rule.
        public string GeneratedBonusText =>
            (modifiers != null && modifiers.Count > 0) ? StatModifierText.Describe(modifiers[0]) : string.Empty;
    }
}
