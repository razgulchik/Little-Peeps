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
    //
    // Levels live inside the one asset: taking the perk again takes its next level, and every modifier
    // carries one step per level (LevelledModifier). The levels' count is authored here; the step lists
    // follow it in OnValidate. A Stat Upgrade (meta) is built the same way, with the same field names.
    [CreateAssetMenu(menuName = "LittlePeeps/Perks/Stat Perk")]
    public class StatPerkDef : PerkDef
    {
        [Tooltip("How many times the perk can be taken in one run. Each modifier then has one step per " +
                 "level — what THAT level adds, not the total so far.")]
        [Min(1)] public int levels = 1;

        [Tooltip("Bonuses this perk grants for the rest of the run, one step per level. Leave a modifier's " +
                 "Source Scope empty to affect every source, or drag one in to target just that one.")]
        public List<LevelledModifier> modifiers;

        public override int MaxLevel => Mathf.Max(1, levels);

        // Every modifier, one line each. Unlike the age card (82px, "one age, one bonus") a perk's
        // description has room, so nothing a perk does stays unsaid unless an author overrides.
        public override string GeneratedDescription(int level) => StatModifierText.Describe(modifiers, level);

        private void OnValidate() => LevelledModifier.FitSteps(modifiers, MaxLevel);

        // The resource comes from the scopes alone, so any value will do for the lookup.
        protected override void AddResources(List<ResourceType> into)
        {
            if (modifiers == null) return;

            for (int i = 0; i < modifiers.Count; i++)
                if (TryGetResource(modifiers[i].With(0f), out ResourceType resource)) into.Add(resource);
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

        // The level's steps, as one list so RunStats raises Changed once. An empty perk asset is inert
        // rather than an exception mid-transition.
        public override void ApplyPerk(RunContext context, int level)
        {
            if (context == null || modifiers == null) return;

            var step = new List<StatModifier>(modifiers.Count);
            for (int i = 0; i < modifiers.Count; i++) step.Add(modifiers[i].With(modifiers[i].StepAt(level)));
            context.stats.Add(step);
        }
    }
}
