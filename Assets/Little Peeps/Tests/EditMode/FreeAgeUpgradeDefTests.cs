using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The free-age meta perk only writes a count into the run; TriggerAgeCmd is where that count is used up
    // instead of the price. Both halves are pinned here, and that a free age grants everything a bought one
    // does. The screens around it (the button, the FREE label, the zone pick) are verified by playing.
    // GameObjects and ScriptableObjects, so these run in the Test Runner only; the offline harness skips them.
    public class FreeAgeUpgradeDefTests
    {
        // Non-zero on purpose: Stone is not ResourceType 0, so a price that ignored its type would still fail.
        private const ResourceType Currency = ResourceType.Stone;
        private const float Price = 10f;

        private FreeAgeUpgradeDef upgrade;
        private AgeDef age;
        private GameObject systemsGo;
        private ResourceSystem resourceSystem;
        private RunContext run;

        [SetUp]
        public void SetUp()
        {
            upgrade = ScriptableObject.CreateInstance<FreeAgeUpgradeDef>();
            upgrade.id = "test_free_age";

            age = ScriptableObject.CreateInstance<AgeDef>();
            age.resourceCost = new List<ResourceCost>
            {
                new ResourceCost { resourceType = Currency, amount = Price },
            };
            age.modifiers = new List<StatModifier>
            {
                new StatModifier { id = StatId.TapRadius, kind = ModifierKind.Flat, value = 1f },
            };

            systemsGo = new GameObject("ResourceSystem");
            resourceSystem = systemsGo.AddComponent<ResourceSystem>();

            run = new RunContext();
            resourceSystem.Initialize(run);
        }

        [TearDown]
        public void TearDown()
        {
            if (upgrade != null) Object.DestroyImmediate(upgrade);
            if (age != null) Object.DestroyImmediate(age);
            if (systemsGo != null) Object.DestroyImmediate(systemsGo);

            // Spending publishes on a static bus; don't leave this test's traffic wired to the next.
            EventBus<ResourceChangedEvent>.Clear();
        }

        private TriggerAgeCmd Buy() => new TriggerAgeCmd(resourceSystem, run, age);

        [Test]
        public void GivesTheRunItsFreeAges()
        {
            upgrade.freeAges = 2;

            upgrade.ApplyToRun(run);

            Assert.That(run.freeAgesLeft, Is.EqualTo(2));
        }

        [Test]
        public void TwoSuchUpgrades_AddUp()
        {
            upgrade.ApplyToRun(run);
            upgrade.ApplyToRun(run);

            Assert.That(run.freeAgesLeft, Is.EqualTo(2));
        }

        [Test]
        public void AZeroTypedPastMin_StillGivesOne()
        {
            upgrade.freeAges = 0;

            upgrade.ApplyToRun(run);

            Assert.That(run.freeAgesLeft, Is.EqualTo(1));
        }

        // The age moves when the player buys it, not when the run is built: the choice of zone is the point.
        [Test]
        public void Applying_DoesNotMoveTheAge()
        {
            upgrade.ApplyToRun(run);

            Assert.That(run.currentAge, Is.EqualTo(RunContext.FirstAge));
        }

        [Test]
        public void AFreeAge_CanBeEnteredWithAnEmptyWallet()
        {
            run.freeAgesLeft = 1;

            Assert.That(Buy().CanExecute(), Is.True);
        }

        [Test]
        public void AFreeAge_TakesNothingFromTheWallet()
        {
            resourceSystem.AddResource(Currency, Price);
            run.freeAgesLeft = 1;

            Buy().Execute();

            Assert.That(resourceSystem.GetResource(Currency), Is.EqualTo(Price));
            Assert.That(run.freeAgesLeft, Is.Zero);
        }

        [Test]
        public void AFreeAge_GrantsEverythingABoughtOneDoes()
        {
            run.freeAgesLeft = 1;

            Buy().Execute();

            Assert.That(run.currentAge, Is.EqualTo(RunContext.FirstAge + 1));
            Assert.That(run.stats.Apply(10f, StatId.TapRadius), Is.EqualTo(11f).Within(1e-4f));
        }

        [Test]
        public void AfterTheLastFreeAge_ThePriceIsBack()
        {
            run.freeAgesLeft = 1;
            Buy().Execute();

            Assert.That(Buy().CanExecute(), Is.False);

            resourceSystem.AddResource(Currency, Price);
            Assert.That(Buy().CanExecute(), Is.True);
        }

        [Test]
        public void WithoutFreeAges_ThePriceIsSpent()
        {
            resourceSystem.AddResource(Currency, Price);

            Buy().Execute();

            Assert.That(resourceSystem.GetResource(Currency), Is.Zero);
            Assert.That(run.currentAge, Is.EqualTo(RunContext.FirstAge + 1));
        }

        [Test]
        public void Description_FollowsTheData()
        {
            upgrade.freeAges = 1;
            Assert.That(upgrade.GeneratedDescription(), Is.EqualTo("FIRST EXPANSION FREE"));

            upgrade.freeAges = 2;
            Assert.That(upgrade.GeneratedDescription(), Is.EqualTo("FIRST 2 EXPANSIONS FREE"));
        }

        [Test]
        public void Description_FollowsTheDataUnlessOverridden()
        {
            Assert.That(upgrade.ScreenText, Is.EqualTo(upgrade.GeneratedDescription()));

            upgrade.description = "A head start";
            Assert.That(upgrade.ScreenText, Is.EqualTo("A head start"));
        }
    }
}
