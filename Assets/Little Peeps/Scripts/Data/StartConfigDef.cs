using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The whole starting state of a run, bundled into one asset: island seed, shape rules and start
    // biome, the house, resources, and stat modifiers. RunManager reads it in StartNewRun so a run's start is fully
    // data-driven. Keep several assets around (default / debug / test presets) and swap the reference
    // on RunManager to change what a run begins with — no code edits, no scene surgery.
    [CreateAssetMenu(menuName = "LittlePeeps/StartConfig")]
    public class StartConfigDef : ScriptableObject
    {
        [Tooltip("Seed of the island generator. 0 = a fresh random seed every run. The seed actually " +
                 "used is logged at run start, so any island can be reproduced by copying it here.")]
        public int islandSeed = 0;

        [Tooltip("Shape rules of the generated island: start and per-age area, minimum land width, " +
                 "bay limits. Hard constraints — see IslandRules.")]
        public IslandRules islandRules = new();

        [Tooltip("Biome of the starting zone: a gentle profile (no mountains or river, guaranteed food and " +
                 "wood). Empty = bare land.")]
        public BiomeDef startBiome;

        [Tooltip("The starting house. The generator keeps a footprint for it in the start's clearing and " +
                 "places it through the normal StructureSystem path.")]
        public StructureDef house;

        [Tooltip("Resources the run begins with, one entry per type. Types not listed start at 0.")]
        public List<ResourceCost> startingResources = new();

        [Tooltip("Stat modifiers applied at run start (base+modifiers system). Real in-run sources " +
                 "(ages/perks/meta) push their own later; use this for a run's baseline or debug bonuses.")]
        public List<StatModifier> startingModifiers = new();
    }
}
