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

        // Every modifier, one line each. Unlike the age card (82px, "one age, one bonus") a perk's
        // description has room, so nothing a perk does stays unsaid unless an author overrides.
        public override string GeneratedDescription => StatModifierText.Describe(modifiers);

        protected override void AddResources(List<ResourceType> into)
        {
            if (modifiers == null) return;

            for (int i = 0; i < modifiers.Count; i++)
                if (TryGetResource(modifiers[i], out ResourceType resource)) into.Add(resource);
        }

        // The resource one modifier acts on: its resourceScope where the stat reads one (yield), else the
        // resource of the source it is scoped to (wheat regrowing → Food). The same StatMeta mask the
        // runtime asks, so a scope RunStats ignores names nothing here either. False for a modifier with
        // neither — stamina, speed, an unscoped source — and for a forge or market stat, which has no
        // scope to read the resource from.
        public static bool TryGetResource(StatModifier modifier, out ResourceType resource)
        {
            StatScope scope = StatMeta.ScopeOf(modifier.id);

            if ((scope & StatScope.Resource) != 0)
            {
                resource = modifier.resourceScope;
                return true;
            }

            if ((scope & StatScope.Source) != 0 && modifier.sourceScope != null)
            {
                resource = modifier.sourceScope.resource;
                return true;
            }

            resource = default;
            return false;
        }

        public override void ApplyPerk(RunContext context)
        {
            // Null-safe on both sides: RunStats.Add ignores a null list, so an empty perk asset is inert
            // rather than an exception mid-transition.
            if (context != null) context.stats.Add(modifiers);
        }
    }
}
