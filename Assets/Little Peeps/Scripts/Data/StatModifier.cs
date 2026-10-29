using System;
using UnityEngine;

namespace LittlePeeps
{
    // One authored contribution to a stat, declared as DATA on a source (AgeDef / PerkDef / a debug
    // list / later meta upgrades). Sources just describe modifiers; RunStats aggregates them and owns
    // the single stacking formula. A scope field is ignored when the stat's StatMeta.ScopeOf mask does
    // not include that dimension — which is also why this struct is drawn by StatModifierDrawer and
    // not by the default inspector: the drawer reads that same mask and shows only the fields that
    // survive it, so nothing on screen is a value the runtime will throw away.
    //
    //   Flat    — value is added to the base value.
    //   Percent — value is added into the additive percent bucket (0.10 = +10%).
    //
    // One kind per modifier: a bonus that needs both is two modifiers. No authored modifier ever set both
    // numbers, and a choice reads in the inspector as the single bonus it is.
    [Serializable]
    public struct StatModifier
    {
        public StatId id;
        public UnitType unitScope;

        // "Any profession" — the unit axis' counterpart of an empty sourceScope below. A flag beside the
        // enum rather than a value inside it: UnitType has no free zero for a wildcard (Unassigned is 0
        // and is a real profession — it harvests trees and alpacas), and an "Any" entry in the enum would
        // also turn up everywhere else a profession is picked, workerYields included, where it means
        // nothing. When set, unitScope is ignored. On a yield it reaches only the professions the source
        // actually pays: ResourceSource checks the worker against workerYields before the formula runs.
        public bool anyProfession;

        public ResourceType resourceScope;

        // Which SOURCE this applies to — drag in Tree / Wheat / Alpaka / Market. Left EMPTY it means
        // "any source", which is what makes a village-wide bonus like "+50% Food for Farmers" one
        // modifier instead of one per food source. Empty is therefore the useful default, not a
        // mistake: it is also what every AgeDef modifier authored before this axis existed relies on.
        [Tooltip("Which source this applies to (Tree, Wheat, Alpaka, ...). Leave empty for ANY source.")]
        public ResourceSourceDef sourceScope;

        // Which BUILDING this applies to (House, Stable, ...), on a stat that belongs to a kind of
        // structure (its build limit). Empty = any structure, the same rule as sourceScope.
        [Tooltip("Which building this applies to (House, Stable, ...). Leave empty for ANY building.")]
        public StructureDef structureScope;

        public ModifierKind kind;
        public float value;

        // The value as each bucket of the formula sees it: the other bucket gets nothing.
        public float Flat => kind == ModifierKind.Flat ? value : 0f;
        public float Percent => kind == ModifierKind.Percent ? value : 0f;
    }

    // Which bucket of the formula a modifier's value goes into. Assets store the number, so reordering
    // these flips every authored modifier.
    public enum ModifierKind
    {
        Flat,
        Percent,
    }
}
