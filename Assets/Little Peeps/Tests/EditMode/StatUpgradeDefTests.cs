using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // Levels as a Stat Perk has them: one step per level on every modifier, and level N in the run is the
    // steps 1 to N — for flat and percent alike, through the one RunStats formula. The meta screen's line
    // shows what the next level brings, or the total once every level is bought. ScriptableObjects, so
    // these run in the Test Runner only; the offline harness skips them.
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

        private void Steps(StatId id, ModifierKind kind, params float[] steps)
        {
            upgrade.levels = steps.Length;
            upgrade.modifiers = new List<LevelledModifier>
            {
                new LevelledModifier { id = id, kind = kind, steps = new List<float>(steps) },
            };
        }

        [Test]
        public void Level3_IsTheFirstThreeSteps_Flat()
        {
            Steps(StatId.TapRadius, ModifierKind.Flat, 1f, 1f, 2f);
            var run = new RunContext();

            upgrade.ApplyToRun(run, 3);

            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(14f).Within(1e-4f));
        }

        // Percents stay additive: +10%, +10%, +20% are +40%, not ×1.1 × 1.1 × 1.2.
        [Test]
        public void Level3_IsTheFirstThreeSteps_Percent()
        {
            Steps(StatId.TapRadius, ModifierKind.Percent, 0.1f, 0.1f, 0.2f);
            var run = new RunContext();

            upgrade.ApplyToRun(run, 3);

            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(14f).Within(1e-4f));
        }

        [Test]
        public void ALowerLevel_LeavesTheLaterStepsOut()
        {
            Steps(StatId.TapRadius, ModifierKind.Flat, 1f, 5f);
            var run = new RunContext();

            upgrade.ApplyToRun(run, 1);

            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(11f).Within(1e-4f));
        }

        [Test]
        public void LevelZero_ChangesNothing()
        {
            Steps(StatId.TapRadius, ModifierKind.Percent, 0.5f);
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

        [Test]
        public void Description_FollowsTheDataUnlessOverridden()
        {
            Steps(StatId.TapRadius, ModifierKind.Flat, 1f);
            Assert.That(upgrade.DescriptionAt(1), Is.EqualTo(upgrade.GeneratedDescription(1)));
            Assert.That(upgrade.DescriptionAt(1), Is.Not.Empty);

            upgrade.description = "Bigger taps";
            Assert.That(upgrade.DescriptionAt(1), Is.EqualTo("Bigger taps"));
        }

        // The line under the title: what the next [+] brings — then, with nothing left to buy, the total.
        [Test]
        public void ScreenText_ShowsTheNextLevel_ThenTheTotalAtTheTop()
        {
            Steps(StatId.ProductionGlobal, ModifierKind.Percent, 0.25f, 0.5f);

            Assert.That(upgrade.ScreenText(0), Is.EqualTo("+25% ALL PRODUCTION"));
            Assert.That(upgrade.ScreenText(1), Is.EqualTo("+50% ALL PRODUCTION [TOTAL +75%]"));
            Assert.That(upgrade.ScreenText(2), Is.EqualTo("+75% ALL PRODUCTION"));
        }

        [Test]
        public void ScreenText_IsTheHandWrittenDescription_OnEveryLevel()
        {
            Steps(StatId.ProductionGlobal, ModifierKind.Percent, 0.25f, 0.5f);
            upgrade.description = "Busy hands";

            Assert.That(upgrade.ScreenText(0), Is.EqualTo("Busy hands"));
            Assert.That(upgrade.ScreenText(2), Is.EqualTo("Busy hands"));
        }

        [Test]
        public void MaxLevel_IsNeverBelowOne()
        {
            upgrade.levels = 0;

            Assert.That(upgrade.MaxLevel, Is.EqualTo(1));
        }
    }
}
