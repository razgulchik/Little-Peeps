using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The start-pick meta perk only owes the run a pick; GameplayContainerState opens it from there.
    // What is pinned here is the debt itself. Opening the pick hangs off RunManager.CurrentRun, which only
    // StartNewRun can fill (see PerkSelectionStateTests for why that is not faked) — verified by playing.
    // ScriptableObjects, so these run in the Test Runner only; the offline harness skips them.
    public class StartPerkPickUpgradeDefTests
    {
        private StartPerkPickUpgradeDef upgrade;

        [SetUp]
        public void SetUp()
        {
            upgrade = ScriptableObject.CreateInstance<StartPerkPickUpgradeDef>();
            upgrade.id = "test_start_pick";
        }

        [TearDown]
        public void TearDown()
        {
            if (upgrade != null) Object.DestroyImmediate(upgrade);
        }

        [Test]
        public void ARunStartsOwingNothing()
        {
            Assert.That(new RunContext().perkPicksOwed, Is.Zero);
        }

        [Test]
        public void Bought_OwesOnePick()
        {
            var run = new RunContext();

            upgrade.ApplyToRun(run, 1);

            Assert.That(run.perkPicksOwed, Is.EqualTo(1));
        }

        [Test]
        public void LevelZero_OwesNothing()
        {
            var run = new RunContext();

            upgrade.ApplyToRun(run, 0);

            Assert.That(run.perkPicksOwed, Is.Zero);
        }

        [Test]
        public void Description_FollowsTheDataUnlessOverridden()
        {
            Assert.That(upgrade.DescriptionText, Is.EqualTo(upgrade.GeneratedDescription));
            Assert.That(upgrade.DescriptionText, Is.Not.Empty);

            upgrade.description = "A head start";
            Assert.That(upgrade.DescriptionText, Is.EqualTo("A head start"));
        }
    }
}
