using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // A level repeats the step: the asset authors ONE level, and level N must come out as N of them — for
    // flat and percent alike, through the one RunStats formula. ScriptableObjects, so these run in the Test
    // Runner only; the offline harness skips them.
    public class StatUpgradeDefTests
    {
        private StatUpgradeDef upgrade;

        [SetUp]
        public void SetUp()
        {
            upgrade = ScriptableObject.CreateInstance<StatUpgradeDef>();
            upgrade.id = "test_upgrade";
        }

        [TearDown]
        public void TearDown()
        {
            if (upgrade != null) Object.DestroyImmediate(upgrade);
        }

        private void Step(float flat, float percent) =>
            upgrade.modifiers = new List<StatModifier>
            {
                new StatModifier { id = StatId.TapRadius, flat = flat, percent = percent },
            };

        [Test]
        public void Level3_IsThreeSteps_Flat()
        {
            Step(flat: 1f, percent: 0f);
            var run = new RunContext();

            upgrade.ApplyToRun(run, 3);

            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(13f).Within(1e-4f));
        }

        // Percents stay additive: three +10% steps are +30%, not ×1.1³.
        [Test]
        public void Level3_IsThreeSteps_Percent()
        {
            Step(flat: 0f, percent: 0.1f);
            var run = new RunContext();

            upgrade.ApplyToRun(run, 3);

            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(13f).Within(1e-4f));
        }

        [Test]
        public void LevelZero_ChangesNothing()
        {
            Step(flat: 1f, percent: 0.5f);
            var run = new RunContext();

            upgrade.ApplyToRun(run, 0);

            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(10f));
        }

        [Test]
        public void NoModifiers_IsInert()
        {
            upgrade.modifiers = null;
            var run = new RunContext();

            Assert.DoesNotThrow(() => upgrade.ApplyToRun(run, 2));
            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(10f));
        }

        // The screen's text is for ONE level and follows the data unless someone wrote it by hand.
        [Test]
        public void Description_FollowsTheDataUnlessOverridden()
        {
            Step(flat: 1f, percent: 0f);
            Assert.That(upgrade.DescriptionText, Is.EqualTo(upgrade.GeneratedDescription));
            Assert.That(upgrade.DescriptionText, Is.Not.Empty);

            upgrade.description = "Bigger taps";
            Assert.That(upgrade.DescriptionText, Is.EqualTo("Bigger taps"));
        }
    }
}
