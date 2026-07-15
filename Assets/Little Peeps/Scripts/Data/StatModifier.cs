using System;

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
    public float flat;
    public float percent;
}
