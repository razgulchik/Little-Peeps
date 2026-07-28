using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // A perk whose whole effect is a list of stat modifiers — the common case, and the one that needs
    // no code at all: authoring the asset IS writing the perk. Same data shape AgeDef uses, so anything
    // an age can grant a perk can grant too.
    //
    // Perks that genuinely need behaviour (not a number) arrive as their OWN PerkDef subclass, one file
    // each. That is why PerkDef stays abstract and why PerkSystem never casts to this type: a code perk
    // must not have to pretend to be a bag of modifiers.
    [CreateAssetMenu(menuName = "LittlePeeps/Perks/Stat Perk")]
    public class StatPerkDef : PerkDef
    {
        [Tooltip("Bonuses this perk grants for the rest of the run. Leave a modifier's Source Scope " +
                 "empty to affect every source, or drag one in to target just that one.")]
        public List<StatModifier> modifiers;

        public override void ApplyPerk(RunContext context)
        {
            // Null-safe on both sides: RunStats.Add ignores a null list, so an empty perk asset is inert
            // rather than an exception mid-transition.
            if (context != null) context.stats.Add(modifiers);
        }
    }
}
