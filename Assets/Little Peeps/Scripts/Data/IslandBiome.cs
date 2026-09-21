using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // One kind of natural object a biome scatters over a zone. The recipe for a zone is built from these:
    // every rule that fires (chance) contributes minCount..maxCount guaranteed copies, then the rest of
    // the zone's object budget is filled by drawing among the rules by weight. That one shape covers
    // every profile the prototype hard-coded per biome (alternating berries and trees, "dens with a
    // 40% chance, one or two berries, the rest trees", "stones only") and the start's guaranteed food and
    // wood — as data.
    [Serializable]
    public class IslandObjectRule
    {
        [Tooltip("What gets placed. A 1×1 structure: tree, berry bush, stone, den...")]
        public StructureDef def;

        [Tooltip("Probability this rule fires at all for a zone. A fired rule places minCount..maxCount " +
                 "copies whatever the weights say; 0 means the rule only ever takes part in the fill.")]
        [Range(0f, 1f)] public float chance = 1f;

        [Tooltip("Guaranteed copies when the rule fires (the zone is regenerated if fewer end up placed).")]
        [Min(0)] public int minCount = 0;

        [Tooltip("Most copies the rule itself asks for when it fires; the fill may add more.")]
        [Min(0)] public int maxCount = 0;

        [Tooltip("Share of the remaining object budget this kind takes. 0 = never used for the fill.")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("Place copies next to earlier copies of the same kind (a wood) rather than spread out.")]
        public bool clustered;
    }

    // What a biome puts on its land, as budgets in percent of the zone's cell count. Caps, not quotas:
    // a feature that finds no room is left out and its allowance stays open land. Plain data, so the
    // generator and its tests can use it without a ScriptableObject; BiomeDef wraps it for the editor.
    //
    // Ported from the prototype's Profile / PROFILES table (island_resources.py); the numbers per biome
    // are the designer's to tune on the BiomeDef assets.
    [Serializable]
    public class IslandBiome
    {
        [Tooltip("Names the biome in logs and seeds its content stream — keep it stable once set.")]
        public string id = "biome";

        [Tooltip("Terrain every cell of a zone in this biome gets; drives placement rules (allowedTerrain) " +
                 "and which tile set draws the ground.")]
        public TerrainType terrain = TerrainType.Grass;

        [Tooltip("At least this share of the zone stays free of natural features (the start's house " +
                 "footprint counts as open). Must leave room for the three caps below.")]
        [Range(0, 100)] public int openPercent = 50;

        [Tooltip("Cap on mountain cells. Needs at least 4 cells' worth to place anything.")]
        [Range(0, 100)] public int mountainPercent = 0;

        [Tooltip("Cap on river cells. A river is at least 4 cells long, so a small cap means no river.")]
        [Range(0, 100)] public int riverPercent = 0;

        [Tooltip("Cap on natural objects (trees, berries, stones, dens).")]
        [Range(0, 100)] public int objectPercent = 20;

        [Tooltip("Probability a zone in this biome gets a river at all, given the cap allows one.")]
        [Range(0f, 1f)] public float riverChance = 0f;

        [Tooltip("1×1 structure placed on every mountain cell. Required when mountainPercent > 0.")]
        public StructureDef mountain;

        [Tooltip("1×1 structure placed on every river cell. Required when riverPercent > 0.")]
        public StructureDef river;

        public List<IslandObjectRule> objects = new();

        // Whole-cell budgets for a zone of `area` cells, rounded down.
        public void Budgets(int area, out int mountains, out int river, out int objects)
        {
            mountains = area * mountainPercent / 100;
            river = area * riverPercent / 100;
            objects = area * objectPercent / 100;
        }

        public void Validate()
        {
            if (openPercent + mountainPercent + riverPercent + objectPercent > 100)
                throw new ArgumentException($"IslandBiome '{id}': openPercent plus the three caps exceed 100%.");
            foreach (var rule in objects)
            {
                if (rule == null) throw new ArgumentException($"IslandBiome '{id}': empty object rule.");
                if (rule.maxCount < rule.minCount)
                    throw new ArgumentException($"IslandBiome '{id}': an object rule has maxCount below minCount.");
            }
        }
    }
}
