using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // One bonus of something bought in levels — a perk (StatPerkDef) or a meta upgrade (StatUpgradeDef): a
    // StatModifier's stat and scopes, with a STEP per level instead of one value. Peer Pressure at three
    // levels might read +10%, +10%, +20% — taking level 3 adds the third step, and the run then holds +40%
    // in all.
    //
    // A separate struct rather than a list inside StatModifier: an age has exactly one value, and a list of
    // one there would be a level nobody can pick. The scope fields carry the SAME names as StatModifier's,
    // which is what lets StatModifierDrawer draw both.
    [Serializable]
    public struct LevelledModifier
    {
        public StatId id;
        public UnitType unitScope;
        public bool anyProfession;
        public ResourceType resourceScope;

        [Tooltip("Which source this applies to (Tree, Wheat, Alpaka, ...). Leave empty for ANY source.")]
        public ResourceSourceDef sourceScope;

        [Tooltip("Which building this applies to (House, Stable, ...). Leave empty for ANY building.")]
        public StructureDef structureScope;

        public ModifierKind kind;

        // What each level ADDS, level 1 first. As long as the owner's level count — FitSteps keeps them in
        // step. 0 = this bonus does not grow on that level, so a bonus can also join at a later one.
        public List<float> steps;

        // What taking `level` adds. A level past the list adds nothing, like a 0 step.
        public float StepAt(int level) =>
            steps != null && level >= 1 && level <= steps.Count ? steps[level - 1] : 0f;

        // What the run holds from this bonus once `level` is taken: every step up to it. Flat and percent
        // both sum in RunStats, so the total of the steps IS the bonus.
        public float TotalAt(int level)
        {
            float total = 0f;
            for (int i = 1; i <= level; i++) total += StepAt(i);
            return total;
        }

        // This bonus with one value, as RunStats and StatModifierText take it.
        public StatModifier With(float value) => new()
        {
            id = id,
            unitScope = unitScope,
            anyProfession = anyProfession,
            resourceScope = resourceScope,
            sourceScope = sourceScope,
            structureScope = structureScope,
            kind = kind,
            value = value,
        };

        // Every modifier's steps made as long as the owner's level count, from its OnValidate: a new level
        // repeats the last step — the usual case is "the same again" — and a removed level drops its steps.
        public static void FitSteps(List<LevelledModifier> modifiers, int levels)
        {
            if (modifiers == null) return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i];
                m.steps ??= new List<float>();
                if (m.steps.Count > levels) m.steps.RemoveRange(levels, m.steps.Count - levels);
                while (m.steps.Count < levels) m.steps.Add(m.steps.Count > 0 ? m.steps[m.steps.Count - 1] : 0f);
                modifiers[i] = m;
            }
        }
    }
}
