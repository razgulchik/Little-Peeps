using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // Starting resources from a meta perk: a level repeats the step, the amounts are ADDED to what the start
    // config seeded, and none of it counts as harvest. ScriptableObjects, so these run in the Test Runner
    // only; the offline harness skips them.
    public class StartResourcesUpgradeDefTests
    {
        private StartResourcesUpgradeDef upgrade;

        [SetUp]
        public void SetUp()
        {
            upgrade = ScriptableObject.CreateInstance<StartResourcesUpgradeDef>();
            upgrade.id = "test_start_resources";
            upgrade.resources = new List<ResourceCost>
            {
                new ResourceCost { resourceType = ResourceType.Food, amount = 20f },
                new ResourceCost { resourceType = ResourceType.Wood, amount = 20f },
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (upgrade != null) Object.DestroyImmediate(upgrade);
        }

        [Test]
        public void Level1_GivesTheStep()
        {
            var run = new RunContext();

            upgrade.ApplyToRun(run, 1);

            Assert.That(run.resources[ResourceType.Food], Is.EqualTo(20f));
            Assert.That(run.resources[ResourceType.Wood], Is.EqualTo(20f));
        }

        [Test]
        public void Level3_IsThreeSteps()
        {
            var run = new RunContext();

            upgrade.ApplyToRun(run, 3);

            Assert.That(run.resources[ResourceType.Food], Is.EqualTo(60f));
        }

        // On top of the start config, never instead of it: RunManager seeds the config before meta perks.
        [Test]
        public void AddsToWhatTheConfigSeeded()
        {
            var run = new RunContext();
            run.resources[ResourceType.Food] = 5f;

            upgrade.ApplyToRun(run, 1);

            Assert.That(run.resources[ResourceType.Food], Is.EqualTo(25f));
        }

        // Harvest is what the prestige payout reads; a start bonus landing there would pay for itself.
        [Test]
        public void IsNotHarvest()
        {
            var run = new RunContext();

            upgrade.ApplyToRun(run, 2);

            Assert.That(run.harvested, Is.Empty);
        }

        [Test]
        public void LevelZero_ChangesNothing()
        {
            var run = new RunContext();

            upgrade.ApplyToRun(run, 0);

            Assert.That(run.resources, Is.Empty);
        }

        [Test]
        public void Description_OneLinePerResource()
        {
            Assert.That(upgrade.GeneratedDescription, Is.EqualTo("+20 FOOD\n+20 WOOD"));
        }

        [Test]
        public void Description_SkipsZeroAmounts()
        {
            upgrade.resources[1].amount = 0f;

            Assert.That(upgrade.GeneratedDescription, Is.EqualTo("+20 FOOD"));
        }
    }
}
