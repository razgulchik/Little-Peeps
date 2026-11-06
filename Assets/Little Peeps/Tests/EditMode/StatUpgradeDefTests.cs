using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // A stat meta perk: its modifiers go into the run through the one RunStats formula, and its line in the
    // tip follows the data unless worded by hand. ScriptableObjects, so these run in the Test Runner only;
    // the offline harness skips them.
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

        private void Modifier(StatId id, ModifierKind kind, float value)
        {
            upgrade.modifiers = new List<StatModifier>
            {
                new StatModifier { id = id, kind = kind, value = value },
            };
        }

        [Test]
        public void Applying_AddsTheModifier_Flat()
        {
            Modifier(StatId.TapRadius, ModifierKind.Flat, 4f);
            var run = new RunContext();

            upgrade.ApplyToRun(run);

            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(14f).Within(1e-4f));
        }

        [Test]
        public void Applying_AddsTheModifier_Percent()
        {
            Modifier(StatId.TapRadius, ModifierKind.Percent, 0.4f);
            var run = new RunContext();

            upgrade.ApplyToRun(run);

            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(14f).Within(1e-4f));
        }

        [Test]
        public void NoModifiers_IsInert()
        {
            upgrade.modifiers = null;
            var run = new RunContext();

            Assert.DoesNotThrow(() => upgrade.ApplyToRun(run));
            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(10f));
        }

        [Test]
        public void ScreenText_FollowsTheDataUnlessOverridden()
        {
            Modifier(StatId.ProductionGlobal, ModifierKind.Percent, 0.25f);
            Assert.That(upgrade.ScreenText, Is.EqualTo("+25% ALL PRODUCTION"));

            upgrade.description = "Busy hands";
            Assert.That(upgrade.ScreenText, Is.EqualTo("Busy hands"));
        }

        [Test]
        public void Cost_IsNeverBelowOne()
        {
            upgrade.cost = 0;

            Assert.That(upgrade.Cost, Is.EqualTo(1));
        }
    }
}
