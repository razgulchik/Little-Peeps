using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // These checks load the shipped assets, rather than a second set of tuning numbers. They protect
    // against economic dead ends; production speed and player decisions still need a playtest.
    public class BalanceAssetTests
    {
        private const string Root = "Assets/Little Peeps/ScriptableObjects/";

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Root + path + ".asset");
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        [Test]
        public void StartingStock_CanBootstrapFoodAndWood_WithoutHarvestingFoodFirst()
        {
            var start = Load<StartConfigDef>("StartConfig/Start Config");
            var stock = start.startingResources.ToDictionary(c => c.resourceType, c => c.amount);
            foreach (string path in new[]
            {
                "StructureDef/Buildings/Wheat", "StructureDef/Racks/Rack_Farmer",
                "StructureDef/Racks/Rack_Lumberjack",
            })
            {
                var building = Load<StructureDef>(path);
                Assert.That(building.requiredAge, Is.LessThanOrEqualTo(RunContext.FirstAge));
                Assert.That(building.lockedUntilPerk, Is.False);
                for (int i = 0; i < building.cost.Count; i++)
                {
                    var type = building.cost[i].resourceType;
                    Assert.That(type, Is.Not.EqualTo(ResourceType.Food), path);
                    float remaining = stock.TryGetValue(type, out var amount) ? amount : 0;
                    stock[type] = remaining - building.CostAt(i, 0);
                    Assert.That(stock[type], Is.GreaterThanOrEqualTo(0), path);
                }
            }
        }

        [TestCase("Meadow")]
        [TestCase("Forest")]
        [TestCase("Desert")]
        public void EveryExpansionChoice_CanSupplyStone_AcrossSeedsAndRepeatedGrowth(string biomeName)
        {
            var start = Load<StartConfigDef>("StartConfig/Start Config");
            var biome = Load<BiomeDef>("Biomes/" + biomeName);
            var rock = Load<StructureDef>("StructureDef/Rock");

            for (int seed = 1; seed <= 20; seed++)
            {
                var generator = new IslandGenerator(start.islandRules, seed);
                var first = generator.GenerateStart();
                var firstContent = generator.Populate(first, start.startBiome.profile,
                    start.house.Footprint, start.pier.Footprint);
                Assert.That(firstContent, Is.Not.Null, $"start, seed {seed}");
                generator.Commit(first, firstContent);

                for (int age = 2; age <= 5; age++)
                {
                    IslandSection grown = null;
                    foreach (var candidate in generator.Propose(3))
                    {
                        var content = generator.Populate(candidate, biome.profile);
                        if (content == null) continue;
                        grown = generator.Commit(candidate, content);
                        break;
                    }
                    string at = $"{biomeName}, seed {seed}, age {age}";
                    Assert.That(grown, Is.Not.Null, at);
                    Assert.That(grown.Content.objects.Values.Count(r => r.def == rock),
                        Is.GreaterThanOrEqualTo(biomeName == "Desert" ? 3 : 1), at);
                    Assert.DoesNotThrow(() => IslandContent.ValidateAll(generator.Sections), at);
                }
            }
        }

        [Test]
        public void Offerings_KeepDemandOnIntroducedResources_ThroughTheEndlessTail()
        {
            var schedule = Load<OfferingScheduleDef>("OfferingDef/Offering Schedule Def");
            var previousRates = new Dictionary<ResourceType, float>();
            for (int cycle = 1; cycle <= schedule.cycles.Count + 2; cycle++)
            {
                var quota = schedule.QuotaOf(cycle);
                var rates = quota.ToDictionary(c => c.resourceType, c => c.amount / schedule.SecondsOf(cycle));
                foreach (var previous in previousRates)
                {
                    Assert.That(rates.ContainsKey(previous.Key), Is.True, $"cycle {cycle}: {previous.Key} disappeared");
                    Assert.That(rates[previous.Key], Is.GreaterThanOrEqualTo(previous.Value),
                        $"cycle {cycle}: pressure on {previous.Key} fell");
                }
                previousRates = rates;
            }
            Assert.That(previousRates.Keys, Is.EquivalentTo(System.Enum.GetValues(typeof(ResourceType))));
        }

        [Test]
        public void DuplicateOfferingRows_RequireFullPayment_AndRefusalSpendsNothing()
        {
            var go = new GameObject("Offering payment test");
            var schedule = ScriptableObject.CreateInstance<OfferingScheduleDef>();
            try
            {
                schedule.cycles.Add(new OfferingQuota
                {
                    resources = new List<ResourceCost>
                    {
                        new() { resourceType = ResourceType.Food, amount = 15 },
                        new() { resourceType = ResourceType.Wood, amount = 5 },
                        new() { resourceType = ResourceType.Food, amount = 15 },
                    },
                });
                var run = new RunContext();
                run.resources[ResourceType.Food] = 15;
                run.resources[ResourceType.Wood] = 5;
                var resources = go.AddComponent<ResourceSystem>();
                resources.Initialize(run);
                var offerings = go.AddComponent<OfferingSystem>();
                var serialized = new SerializedObject(offerings);
                serialized.FindProperty("schedule").objectReferenceValue = schedule;
                serialized.FindProperty("resourceSystem").objectReferenceValue = resources;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                offerings.Initialize(run);

                Assert.That(offerings.CanOffer, Is.False);
                Assert.That(offerings.TryOffer(), Is.False);
                Assert.That(resources.GetResource(ResourceType.Food), Is.EqualTo(15));
                Assert.That(resources.GetResource(ResourceType.Wood), Is.EqualTo(5));

                resources.AddResource(ResourceType.Food, 15);
                Assert.That(offerings.TryOffer(), Is.True);
                Assert.That(resources.GetResource(ResourceType.Food), Is.Zero);
                Assert.That(resources.GetResource(ResourceType.Wood), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(schedule);
                EventBus<ResourceChangedEvent>.Clear();
            }
        }
    }
}
