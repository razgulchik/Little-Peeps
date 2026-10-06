using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The build limit (StructureDef.maxCount + StructureLimit modifiers) and the growing price read off
    // a def. What is pinned: 0 means no limit and no modifier changes that, a modifier reaches its own
    // building or every limited one, the limit is the LAST lock reason, and the count it compares against
    // is the run's (RunContext.CountOf).
    //
    // Needs real StructureDef instances, so these run in the Editor's Test Runner and are skipped by the
    // offline reflection harness.
    public class StructureLimitTests
    {
        private StructureDef house;
        private StructureDef stable;

        [SetUp]
        public void SetUp()
        {
            house = ScriptableObject.CreateInstance<StructureDef>();
            house.name = "House";
            stable = ScriptableObject.CreateInstance<StructureDef>();
            stable.name = "Stable";
        }

        [TearDown]
        public void TearDown()
        {
            if (house != null) Object.DestroyImmediate(house);
            if (stable != null) Object.DestroyImmediate(stable);
        }

        private static RunContext RunWith(StructureDef def, int standing)
        {
            var run = new RunContext();
            run.standing[def] = standing;
            return run;
        }

        private static StatModifier Limit(StructureDef def, float flat = 0f, float percent = 0f) =>
            new StatModifier { id = StatId.StructureLimit, structureScope = def, flat = flat, percent = percent };

        // --- the limit -------------------------------------------------------------------------------

        [Test]
        public void MaxCountZero_IsNoLimit_HoweverManyStand()
        {
            var run = RunWith(house, 100);

            Assert.IsFalse(house.HasLimit);
            Assert.IsFalse(house.AtLimit(run));
            Assert.That(house.LockState(0, run), Is.EqualTo(StructureLock.None));
        }

        [Test]
        public void TheLimit_LocksOnceThatManyStand()
        {
            house.maxCount = 2;

            Assert.That(house.LockState(0, RunWith(house, 1)), Is.EqualTo(StructureLock.None));
            Assert.That(house.LockState(0, RunWith(house, 2)), Is.EqualTo(StructureLock.Limit));
            Assert.That(house.LockState(0, RunWith(house, 3)), Is.EqualTo(StructureLock.Limit), "over it too");
        }

        [Test]
        public void NoRun_IsNeverAtTheLimit_AndReadsTheBareMaxCount()
        {
            house.maxCount = 2;

            Assert.IsFalse(house.AtLimit(null));
            Assert.That(house.LimitIn(null), Is.EqualTo(2));
        }

        [Test]
        public void AModifier_RaisesItsOwnBuilding_Only()
        {
            house.maxCount = 2;
            stable.maxCount = 2;
            var run = new RunContext();
            run.stats.Add(Limit(house, flat: 1f));

            Assert.That(house.LimitIn(run), Is.EqualTo(3));
            Assert.That(stable.LimitIn(run), Is.EqualTo(2));
        }

        [Test]
        public void AModifierWithNoBuilding_RaisesEveryLimitedOne()
        {
            house.maxCount = 2;
            stable.maxCount = 1;
            var run = new RunContext();
            run.stats.Add(Limit(null, flat: 1f));

            Assert.That(house.LimitIn(run), Is.EqualTo(3));
            Assert.That(stable.LimitIn(run), Is.EqualTo(2));
        }

        [Test]
        public void TheOwnAndTheAnyBuckets_Stack()
        {
            house.maxCount = 2;
            var run = new RunContext();
            run.stats.Add(Limit(house, flat: 1f));
            run.stats.Add(Limit(null, flat: 1f));

            Assert.That(house.LimitIn(run), Is.EqualTo(4));
        }

        [Test]
        public void AModifier_NeverLimitsAnUnlimitedBuilding()
        {
            var run = RunWith(house, 5);
            run.stats.Add(Limit(house, flat: 1f));
            run.stats.Add(Limit(null, flat: 1f));

            Assert.IsFalse(house.HasLimit);
            Assert.IsFalse(house.AtLimit(run));
        }

        [Test]
        public void TheLimit_RoundsDown_AndNeverGoesBelowZero()
        {
            house.maxCount = 3;
            var run = new RunContext();
            run.stats.Add(Limit(house, percent: 0.5f));   // 4.5

            Assert.That(house.LimitIn(run), Is.EqualTo(4));

            run.stats.Add(Limit(house, flat: -10f));
            Assert.That(house.LimitIn(run), Is.EqualTo(0));
            Assert.IsTrue(house.AtLimit(run), "a limit cut to zero is full, not unlimited");
        }

        [Test]
        public void TheAgeAndPerkGates_AreReportedBeforeTheLimit()
        {
            house.maxCount = 1;
            house.requiredAge = 2;
            var run = RunWith(house, 1);   // the starting house

            Assert.That(house.LockState(0, run), Is.EqualTo(StructureLock.Age));

            house.lockedUntilPerk = true;
            Assert.That(house.LockState(0, run), Is.EqualTo(StructureLock.Perk));
        }

        // --- the card text ---------------------------------------------------------------------------

        [Test]
        public void TheModifierText_NamesTheBuilding_ByDisplayName_ElseByAsset()
        {
            Assert.That(StatModifierText.Describe(Limit(house, flat: 1f)), Is.EqualTo("+1 HOUSE LIMIT"));

            house.displayName = "Straw Hut";
            Assert.That(StatModifierText.Describe(Limit(house, flat: 1f)), Is.EqualTo("+1 STRAW HUT LIMIT"));
        }

        // --- the count -------------------------------------------------------------------------------

        [Test]
        public void CountOf_IsZero_ForAStructureNeverBuilt_AndForNull()
        {
            var run = new RunContext();

            Assert.That(run.CountOf(house), Is.EqualTo(0));
            Assert.That(run.CountOf(null), Is.EqualTo(0));
        }

        // --- the price -------------------------------------------------------------------------------

        [Test]
        public void CostAt_GrowsEachEntry_WithTheDefsOwnSteps()
        {
            house.cost = new System.Collections.Generic.List<ResourceCost>
            {
                new ResourceCost { resourceType = ResourceType.Wood, amount = 10f },
                new ResourceCost { resourceType = ResourceType.Food, amount = 4f },
            };
            house.costStepPercent = 0.5f;

            Assert.That(house.CostAt(0, 2), Is.EqualTo(20f));
            Assert.That(house.CostAt(1, 2), Is.EqualTo(8f));

            house.exponentialCostGrowth = true;
            Assert.That(house.CostAt(0, 2), Is.EqualTo(23f), "22.5 rounded half up");
            Assert.That(house.CostAt(1, 2), Is.EqualTo(9f));
        }
    }
}
