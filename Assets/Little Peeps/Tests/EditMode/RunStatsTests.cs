using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

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

        // --- durations ------------------------------------------------------------------------------

        // Durations run on the SAME formula as everything else: the modifier scales the seconds, so a
        // shorter wait is a negative percent. There is no second code path and no rate arithmetic — the
        // sign in the asset is the whole mechanism.
        [Test]
        public void ADurationStat_IsScaledByTheOrdinaryFormula()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.SpawnerRecharge, percent: -0.25f, unit: UnitType.Miner));

            Assert.That(stats.Apply(4f, StatId.SpawnerRecharge, UnitType.Miner),
                        Is.EqualTo(3f).Within(Tolerance), "-25% means three seconds, not five");
        }

        [Test]
        public void ADurationStat_GoesNegativeRatherThanBlowingUp_PastMinusOneHundredPercent()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.SpawnerRecharge, percent: -1.5f, unit: UnitType.Miner));

            // Deliberately NOT clamped here. A negative timer is already <= 0 at the consumer, so the
            // worst an over-tuned stack can do is fire instantly — never Infinity, never NaN.
            Assert.That(stats.Apply(4f, StatId.SpawnerRecharge, UnitType.Miner),
                        Is.LessThan(0f));
        }

        // Each new StatId needs its own line in StatMeta.ScopeOf, and the `_ => None` default means a
        // forgotten one silently turns the stat GLOBAL — a Miner's bonus would reach every unit and
        // nothing would look wrong. One test per scoped duration stat, on non-zero units.
        [Test]
        public void SpawnerRecharge_IsScopedToItsUnit()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.SpawnerRecharge, percent: -0.5f, unit: UnitType.Miner));

            Assert.That(stats.Apply(4f, StatId.SpawnerRecharge, UnitType.Lumberjack),
                        Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void UnitFatigueDelay_IsScopedToItsUnit()
        {
            var stats = new RunStats();
            stats.Add(Mod(StatId.UnitFatigueDelay, percent: 1f, unit: UnitType.Miner));

            Assert.That(stats.Apply(3f, StatId.UnitFatigueDelay, UnitType.Miner),
                        Is.EqualTo(6f).Within(Tolerance), "+100% keeps a Miner out twice as long");
            Assert.That(stats.Apply(3f, StatId.UnitFatigueDelay, UnitType.Lumberjack),
                        Is.EqualTo(3f).Within(Tolerance));
        }
    }

    // The source axis (StatScope.Source) is the third scope dimension on ResourceYield. It exists
    // because ResourceType cannot tell two sources of the same resource apart — Market and Alpaka are
    // both Coins, Wheat and Boar are both Food — so without it a perk on alpaca silently buffs the
    // market as well.
    //
    // Unlike the enum dimensions, this one has a meaningful EMPTY value: an unset source means "any
    // source", so one modifier covers a village-wide bonus. That rule is what the bulk of these tests
    // pin, because the read side ALWAYS passes a concrete source — a modifier that only matched an
    // empty one could never fire at all.
    //
    // Separate class on purpose: these need real ResourceSourceDef instances, so they run in the
    // Editor's Test Runner and are skipped by the offline reflection harness. A [SetUp] calling
    // ScriptableObject.CreateInstance in the class above would take the pure arithmetic tests down
    // with it offline, where there is no native Unity to call into.
    public class RunStatsSourceScopeTests
    {
        private const float Tolerance = 1e-4f;

        // Non-zero on purpose, same reasoning as RunStatsTests: Farmer and Food are both 0, so a test
        // written on them would pass even if the scope were dropped entirely.
        private const UnitType Worker = UnitType.Miner;
        private const ResourceType Res = ResourceType.Stone;

        private ResourceSourceDef quarry;
        private ResourceSourceDef cave;

        [SetUp]
        public void SetUp()
        {
            // Both fakes must actually PRODUCE Res, not just be paired with it in the modifiers below.
            // MakeKey derives the resource from the source, so a fake left on the enum's default would
            // key every source-scoped modifier under Food while the source-less ones stayed on Res, and
            // the "any source" bucket would stop matching — failing these tests for a reason that has
            // nothing to do with what they are pinning.
            quarry = ScriptableObject.CreateInstance<ResourceSourceDef>();
            quarry.resource = Res;
            cave = ScriptableObject.CreateInstance<ResourceSourceDef>();
            cave.resource = Res;
        }

        [TearDown]
        public void TearDown()
        {
            if (quarry != null) Object.DestroyImmediate(quarry);
            if (cave != null) Object.DestroyImmediate(cave);
        }

        private static StatModifier Yield(float flat = 0f, float percent = 0f,
                                          ResourceSourceDef source = null)
            => new StatModifier
            {
                id = StatId.ResourceYield,
                unitScope = Worker,
                resourceScope = Res,
                sourceScope = source,
                flat = flat,
                percent = percent,
            };

        [Test]
        public void SourceScopedModifier_AppliesToItsOwnSource()
        {
            var stats = new RunStats();
            stats.Add(Yield(percent: 1f, source: quarry));

            Assert.That(stats.Apply(2f, StatId.ResourceYield, Worker, Res, quarry),
                        Is.EqualTo(4f).Within(Tolerance));
        }

        [Test]
        public void SourceScopedModifier_DoesNotLeakToAnotherSourceOfTheSameResource()
        {
            var stats = new RunStats();
            stats.Add(Yield(percent: 1f, source: quarry));

            // The entire reason the axis exists: same worker, same ResourceType, different source.
            Assert.That(stats.Apply(2f, StatId.ResourceYield, Worker, Res, cave),
                        Is.EqualTo(2f).Within(Tolerance),
                        "a bonus authored for one source must not reach another sharing its resource");
        }

        [Test]
        public void AResourceThatDisagreesWithItsSource_IsCorrectedToTheSourcesOwn()
        {
            var stats = new RunStats();
            stats.Add(new StatModifier
            {
                id = StatId.ResourceYield,
                unitScope = Worker,
                // A resource the source does not produce. Reachable by hand-editing YAML, by code, or
                // by an asset authored before the source axis existed. Taken at face value it would key
                // the modifier under (Coins, quarry) while the read side always asks for
                // (quarry.resource, quarry) — bought, saved, and silently dead.
                resourceScope = ResourceType.Coins,
                sourceScope = quarry,
                percent = 1f,
            });

            Assert.That(stats.Apply(2f, StatId.ResourceYield, Worker, Res, quarry),
                        Is.EqualTo(4f).Within(Tolerance), "the source's own resource wins");
            Assert.That(stats.Apply(2f, StatId.ResourceYield, Worker, Res, cave),
                        Is.EqualTo(2f).Within(Tolerance),
                        "and correcting it must not turn the modifier into a source-agnostic one");
        }

        [Test]
        public void ModifierWithNoSource_ReachesEverySource()
        {
            var stats = new RunStats();
            stats.Add(Yield(flat: 1f));   // sourceScope left empty — "any source"

            // This is the shape of every AgeDef modifier authored before the axis existed: Age 3 grants
            // (Farmer, Wood, flat 1) with no source, because there was no source field to fill in. If an
            // empty source meant "its own bucket" rather than "any", that bonus would go silently dead
            // the moment a real source appears in the query — and one always does.
            Assert.That(stats.Apply(1f, StatId.ResourceYield, Worker, Res, quarry),
                        Is.EqualTo(2f).Within(Tolerance));
            Assert.That(stats.Apply(1f, StatId.ResourceYield, Worker, Res, cave),
                        Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void ModifierWithNoSource_IsNotCountedTwice_WhenTheQueryHasNoSourceEither()
        {
            var stats = new RunStats();
            stats.Add(Yield(flat: 1f, percent: 0.5f));

            // With no source in the query, the exact key and the "any source" key are the SAME key, so
            // a naive "always read both buckets" adds this modifier to itself: (1+2) * 2 = 6, not 3.
            Assert.That(stats.Apply(1f, StatId.ResourceYield, Worker, Res),
                        Is.EqualTo(3f).Within(Tolerance));
        }

        [Test]
        public void SourceSpecificAndSourceAgnostic_StackAdditively_LikeEveryOtherPair()
        {
            var stats = new RunStats();
            stats.Add(Yield(percent: 0.5f));                  // +50% from anything
            stats.Add(Yield(percent: 0.5f, source: quarry));  // +50% more from this one

            // The buckets are summed and the formula runs ONCE: (10 + 0) * (1 + 1.0) = 20. Running it
            // per bucket would multiply instead — 10 * 1.5 * 1.5 = 22.5 — breaking the additive-percent
            // contract the rest of the balance is built on.
            Assert.That(stats.Apply(10f, StatId.ResourceYield, Worker, Res, quarry),
                        Is.EqualTo(20f).Within(Tolerance));

            Assert.That(stats.Apply(10f, StatId.ResourceYield, Worker, Res, cave),
                        Is.EqualTo(15f).Within(Tolerance),
                        "the other source still gets the agnostic half and nothing more");
        }

        [Test]
        public void FlatFromBothBuckets_IsSummedBeforeThePercent()
        {
            var stats = new RunStats();
            stats.Add(Yield(flat: 1f));
            stats.Add(Yield(flat: 2f, percent: 0.5f, source: quarry));

            // (10 + 1 + 2) * 1.5 = 19.5
            Assert.That(stats.Apply(10f, StatId.ResourceYield, Worker, Res, quarry),
                        Is.EqualTo(19.5f).Within(Tolerance));
        }

        [Test]
        public void SourceRespawn_IsScopedToItsSource_AndNotToAResource()
        {
            var stats = new RunStats();
            stats.Add(new StatModifier
            {
                id = StatId.SourceRespawn,
                sourceScope = quarry,
                // A stray resource scope: SourceRespawn's mask is Source ONLY, because a source already
                // fixes its resource. If Resource were in the mask, authoring just the source would key
                // the modifier under Food (the enum's zero) and it would never be found again.
                resourceScope = ResourceType.Metal,
                percent = -0.5f,
            });

            Assert.That(stats.Apply(4f, StatId.SourceRespawn, source: quarry),
                        Is.EqualTo(2f).Within(Tolerance), "-50% halves the regrow delay");
            Assert.That(stats.Apply(4f, StatId.SourceRespawn, source: cave),
                        Is.EqualTo(4f).Within(Tolerance), "and leaves every other source alone");
        }

        [Test]
        public void SourceRespawn_WithNoSource_SpeedsUpEverything()
        {
            var stats = new RunStats();
            stats.Add(new StatModifier { id = StatId.SourceRespawn, percent = -0.5f });

            Assert.That(stats.Apply(4f, StatId.SourceRespawn, source: quarry),
                        Is.EqualTo(2f).Within(Tolerance));
            Assert.That(stats.Apply(4f, StatId.SourceRespawn, source: cave),
                        Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void StatWithoutASourceDimension_IgnoresAStraySource()
        {
            var stats = new RunStats();
            // UnitSpeed's mask has no Source, so this reference is meaningless authored data — exactly
            // what gets left behind in a perk asset. It must not make the modifier unreachable.
            stats.Add(new StatModifier
            {
                id = StatId.UnitSpeed,
                unitScope = Worker,
                sourceScope = quarry,
                percent = 0.5f,
            });

            Assert.That(stats.Apply(2f, StatId.UnitSpeed, Worker), Is.EqualTo(3f).Within(Tolerance));
            Assert.That(stats.Apply(2f, StatId.UnitSpeed, Worker, default, cave),
                        Is.EqualTo(3f).Within(Tolerance),
                        "and a stray source on the QUERY side is just as irrelevant");
        }
    }
}
