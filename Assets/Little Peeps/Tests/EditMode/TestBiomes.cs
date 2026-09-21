namespace LittlePeeps.Tests
{
    // The prototype's PROFILES table (Prototypes/IslandShapeLab/island_resources.py) as biome profiles
    // built in code, shared by the content and zone-offer tests. Each biome's object kinds are rules
    // without a StructureDef — nothing here needs a ScriptableObject, so every test on top of it runs
    // under the offline harness. Kinds are told apart by rule identity.
    internal static class TestBiomes
    {
        public static IslandObjectRule Rule(float chance = 0f, int min = 0, int max = 0, float weight = 1f, bool clustered = false) =>
            new() { chance = chance, minCount = min, maxCount = max, weight = weight, clustered = clustered };

        // The starting profile: gentle, food and wood guaranteed, no terrain features.
        public static IslandBiome StartingGrasslands() => new()
        {
            id = "start", terrain = TerrainType.Grass,
            openPercent = 80, mountainPercent = 0, riverPercent = 0, objectPercent = 20, riverChance = 0f,
            objects = { Rule(chance: 1f, min: 2, max: 3, clustered: true), Rule(chance: 1f, min: 2, max: 3) },  // berry, tree
        };

        public static IslandBiome Grasslands() => new()
        {
            id = "grass", terrain = TerrainType.Grass,
            openPercent = 50, mountainPercent = 0, riverPercent = 35, objectPercent = 15, riverChance = 0.8f,
            objects = { Rule(clustered: true), Rule() },   // berry, tree
        };

        public static IslandBiome StoneDesert() => new()
        {
            id = "desert", terrain = TerrainType.Desert,
            openPercent = 45, mountainPercent = 35, riverPercent = 5, objectPercent = 15, riverChance = 0.3f,
            objects = { Rule(clustered: true) },   // stone
        };

        public static IslandBiome Forest() => new()
        {
            id = "forest", terrain = TerrainType.Forest,
            openPercent = 45, mountainPercent = 0, riverPercent = 20, objectPercent = 35, riverChance = 0.65f,
            objects =
            {
                Rule(chance: 0.4f, min: 1, max: 1, weight: 0f),                    // boar den
                Rule(chance: 0.4f, min: 1, max: 1, weight: 0f),                    // fox den
                Rule(chance: 1f, min: 1, max: 2, weight: 0f, clustered: true),     // berry
                Rule(chance: 0.45f, min: 1, max: 1, weight: 0f, clustered: true),  // stone
                Rule(clustered: true),                                             // tree, fills the rest
            },
        };

        public static IslandBiome[] Cycle() => new[] { Grasslands(), StoneDesert(), Forest() };
    }
}
