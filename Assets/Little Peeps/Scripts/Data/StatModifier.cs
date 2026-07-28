using System;
using UnityEngine;

namespace LittlePeeps
{
    // One authored contribution to a stat, declared as DATA on a source (AgeDef / PerkDef / a debug
    // list / later meta upgrades). Sources just describe modifiers; RunStats aggregates them and owns
    // the single stacking formula. A scope field is ignored when the stat's StatMeta.ScopeOf mask does
    // not include that dimension.
    //
    //   flat    — added to the base value.
    //   percent — added into the additive percent bucket (0.10 = +10%).
    [Serializable]
    public struct StatModifier
    {
        public StatId id;
        public UnitType unitScope;
        public ResourceType resourceScope;

        // Which SOURCE this applies to — drag in Tree / Wheat / Alpaka / Market. Left EMPTY it means
        // "any source", which is what makes a village-wide bonus like "+50% Food for Farmers" one
        // modifier instead of one per food source. Empty is therefore the useful default, not a
        // mistake: it is also what every AgeDef modifier authored before this axis existed relies on.
        [Tooltip("Which source this applies to (Tree, Wheat, Alpaka, ...). Leave empty for ANY source.")]
        public ResourceSourceDef sourceScope;

        public float flat;
        public float percent;
    }
}
