using System.Collections.Generic;
using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // RunStats is the bonuses layer of the stat system: it aggregates authored modifiers and owns the
    // one stacking formula, (base + Sum(flat)) * (1 + Sum(percent)).
    //
    // The part worth guarding is the scope normalisation in MakeKey. A modifier authored with a scope
    // its stat does not use must still be found by a query that passes no scope — otherwise the failure
    // is silent and user-visible as "the perk was bought and nothing happened".
    //
    // Every scope test below uses NON-ZERO enum values on purpose. Farmer, Food and Grass are all 0, so
    // the same test written on them would pass even if MakeKey did nothing at all.
    public class RunStatsTests
    {
        private const float Tolerance = 1e-4f;

        private static StatModifier Mod(StatId id, float flat = 0f, float percent = 0f,
                                        UnitType unit = default, ResourceType res = default)
            => new StatModifier
            {
                id = id,
                unitScope = unit,
                resourceScope = res,
                flat = flat,
                percent = percent,
            };

        // --- the formula ---------------------------------------------------------------------------

        [Test]
        public void Apply_ReturnsTheBaseValue_WhenNothingModifiesTheStat()
        {
            var stats = new RunStats();

            Assert.That(stats.Apply(7f, StatId.ProductionGlobal), Is.EqualTo(7f).Within(Tolerance));
        }

        [Test]
        public void Apply_AddsFlatBeforeApplyingPercent()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.ProductionGlobal, flat: 2f, percent: 0.5f));

            // (10 + 2) * 1.5 = 18. The other reading, 10 * 1.5 + 2 = 17, is the bug this pins.
            Assert.That(stats.Apply(10f, StatId.ProductionGlobal), Is.EqualTo(18f).Within(Tolerance));
        }

        [Test]
        public void Add_AccumulatesSeveralModifiersIntoOneKey()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.ProductionGlobal, flat: 1f, percent: 0.1f));
            stats.Add(Mod(StatId.ProductionGlobal, flat: 3f, percent: 0.4f));

            // Percents stack ADDITIVELY into one bucket: (10 + 4) * (1 + 0.5) = 21, not
            // (10 + 4) * 1.1 * 1.4 = 21.56.
            Assert.That(stats.Apply(10f, StatId.ProductionGlobal), Is.EqualTo(21f).Within(Tolerance));
        }

        [Test]
        public void Add_AppliesEveryModifierInAList()
        {
            var stats = new RunStats();
            stats.Add(new List<StatModifier>
            {
                Mod(StatId.ProductionGlobal, flat: 1f),
                Mod(StatId.ProductionGlobal, percent: 0.5f),
                Mod(StatId.UnitSpeed, flat: 5f, unit: UnitType.Miner),
            });

            Assert.That(stats.Apply(1f, StatId.ProductionGlobal), Is.EqualTo(3f).Within(Tolerance));
            Assert.That(stats.Apply(1f, StatId.UnitSpeed, UnitType.Miner), Is.EqualTo(6f).Within(Tolerance));
        }

        [Test]
        public void Add_IgnoresANullList()
        {
            var stats = new RunStats();

            // AgeDef.modifiers and friends are authored fields that can legitimately be left empty.
            Assert.DoesNotThrow(() => stats.Add((IReadOnlyList<StatModifier>)null));
        }

        [Test]
        public void Multiplier_IsApplyOnABaseOfOne()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.ProductionGlobal, percent: 0.25f));

            Assert.That(stats.Multiplier(StatId.ProductionGlobal), Is.EqualTo(1.25f).Within(Tolerance));
        }

        // --- scope normalisation -------------------------------------------------------------------

        [Test]
        public void GlobalStat_AuthoredWithStrayScopes_IsFoundByAnUnscopedQuery()
        {
            var stats = new RunStats();
            // ProductionGlobal has StatScope.None, so both scopes here are meaningless data — exactly
            // what an author leaves behind in a perk asset by accident. MakeKey must zero them out on
            // the way in, or this modifier becomes unreachable.
            stats.Add(Mod(StatId.ProductionGlobal, percent: 0.25f,
                          unit: UnitType.Miner, res: ResourceType.Metal));

            Assert.That(stats.Multiplier(StatId.ProductionGlobal), Is.EqualTo(1.25f).Within(Tolerance));
        }

        [Test]
        public void GlobalStat_AuthoredUnscoped_IsFoundByAQueryThatPassesScopes()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.ProductionGlobal, percent: 0.25f));

            // The mirror case: normalisation has to happen on the READ side too, not just on Add.
            Assert.That(stats.Multiplier(StatId.ProductionGlobal, UnitType.Miner, ResourceType.Metal),
                        Is.EqualTo(1.25f).Within(Tolerance));
        }

        [Test]
        public void UnitScopedStat_DropsAStrayResourceScope_ButKeepsTheUnit()
        {
            var stats = new RunStats();
            // UnitSpeed is Unit-only: the Metal must be discarded, the Miner must NOT be.
            stats.Add(Mod(StatId.UnitSpeed, percent: 0.5f, unit: UnitType.Miner, res: ResourceType.Metal));

            Assert.That(stats.Apply(2f, StatId.UnitSpeed, UnitType.Miner),
                        Is.EqualTo(3f).Within(Tolerance));
        }

        [Test]
        public void UnitScopedStat_DoesNotLeakToAnotherUnit()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.UnitSpeed, percent: 0.5f, unit: UnitType.Miner));

            Assert.That(stats.Apply(2f, StatId.UnitSpeed, UnitType.Lumberjack),
                        Is.EqualTo(2f).Within(Tolerance), "a Miner buff must not reach a Lumberjack");
        }

        [Test]
        public void FullyScopedStat_KeepsBothDimensionsApart()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.ResourceYield, flat: 1f, unit: UnitType.Miner, res: ResourceType.Stone));

            Assert.That(stats.Apply(1f, StatId.ResourceYield, UnitType.Miner, ResourceType.Stone),
                        Is.EqualTo(2f).Within(Tolerance), "exact scope match");
            Assert.That(stats.Apply(1f, StatId.ResourceYield, UnitType.Lumberjack, ResourceType.Stone),
                        Is.EqualTo(1f).Within(Tolerance), "wrong unit");
            Assert.That(stats.Apply(1f, StatId.ResourceYield, UnitType.Miner, ResourceType.Metal),
                        Is.EqualTo(1f).Within(Tolerance), "wrong resource");
        }

        [Test]
        public void DifferentStats_DoNotShareABucket()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.ProductionGlobal, flat: 100f));

            Assert.That(stats.Apply(1f, StatId.UnitSpeed, UnitType.Miner),
                        Is.EqualTo(1f).Within(Tolerance));
        }
    }
}
