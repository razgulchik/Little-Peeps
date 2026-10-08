using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The start-age meta perk only writes its intent into the run; RunManager.SkipStartAges carries it out
    // once the island exists, and that needs live systems — verified by playing. Pinned here: the intent,
    // and TriggerAgeCmd.EnterAge, the one definition of entering an age that a skipped age and a bought one
    // share. ScriptableObjects, so these run in the Test Runner only; the offline harness skips them.
    public class StartAgeUpgradeDefTests
    {
        private StartAgeUpgradeDef upgrade;
        private BiomeDef biome;
        private AgeDef age;

        [SetUp]
        public void SetUp()
        {
            upgrade = ScriptableObject.CreateInstance<StartAgeUpgradeDef>();
            upgrade.id = "test_start_age";
            biome = ScriptableObject.CreateInstance<BiomeDef>();
            upgrade.zoneBiome = biome;
            age = ScriptableObject.CreateInstance<AgeDef>();
            age.modifiers = new List<StatModifier>
            {
                new StatModifier { id = StatId.TapRadius, flat = 1f },
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (upgrade != null) Object.DestroyImmediate(upgrade);
            if (biome != null) Object.DestroyImmediate(biome);
            if (age != null) Object.DestroyImmediate(age);
        }

        [Test]
        public void Level2_SkipsTwoAges_InTheChosenBiome()
        {
            var run = new RunContext();

            upgrade.ApplyToRun(run, 2);

            Assert.That(run.startAgesToSkip, Is.EqualTo(2));
            Assert.That(run.skippedAgeBiome, Is.SameAs(biome));
        }

        // The intent only: the age itself moves when RunManager carries it out, after the island exists.
        [Test]
        public void Applying_DoesNotMoveTheAgeYet()
        {
            var run = new RunContext();

            upgrade.ApplyToRun(run, 2);

            Assert.That(run.currentAge, Is.EqualTo(RunContext.FirstAge));
            Assert.That(run.perkPicksOwed, Is.Zero);
        }

        [Test]
        public void LevelZero_SkipsNothing()
        {
            var run = new RunContext();

            upgrade.ApplyToRun(run, 0);

            Assert.That(run.startAgesToSkip, Is.Zero);
        }

        // What a skipped age grants is exactly what a bought one does, minus the price.
        [Test]
        public void EnterAge_RaisesTheAgeAndGrantsItsBonus()
        {
            var run = new RunContext();

            TriggerAgeCmd.EnterAge(run, age);

            Assert.That(run.currentAge, Is.EqualTo(RunContext.FirstAge + 1));
            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(11f).Within(1e-4f));
        }

        [Test]
        public void Description_FollowsTheDataUnlessOverridden()
        {
            Assert.That(upgrade.DescriptionText, Is.EqualTo(upgrade.GeneratedDescription));
            Assert.That(upgrade.DescriptionText, Is.Not.Empty);

            upgrade.description = "Late bloomer";
            Assert.That(upgrade.DescriptionText, Is.EqualTo("Late bloomer"));
        }
    }
}
