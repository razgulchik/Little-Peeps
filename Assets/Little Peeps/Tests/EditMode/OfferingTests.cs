using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The schedule's rules: which quota a cycle asks for, how it grows past the authored list, which
    // cycles are special. Pure statics on plain lists, so these run in the offline harness too.
    public class OfferingScheduleTests
    {
        private static OfferingQuota Q(params (ResourceType type, float amount)[] entries)
        {
            var q = new OfferingQuota();
            foreach (var (type, amount) in entries)
                q.resources.Add(new ResourceCost { resourceType = type, amount = amount });
            return q;
        }

        private static readonly List<OfferingQuota> Cycles = new()
        {
            Q((ResourceType.Wood, 10)),
            Q((ResourceType.Wood, 20), (ResourceType.Food, 10)),
        };

        [Test]
        public void InsideTheList_TheCycleGetsItsOwnEntry()
        {
            var first = OfferingScheduleDef.QuotaFor(Cycles, 1.5f, 1);
            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(first[0].resourceType, Is.EqualTo(ResourceType.Wood));
            Assert.That(first[0].amount, Is.EqualTo(10f));

            var second = OfferingScheduleDef.QuotaFor(Cycles, 1.5f, 2);
            Assert.That(second.Count, Is.EqualTo(2));
            Assert.That(second[1].resourceType, Is.EqualTo(ResourceType.Food));
        }

        [Test]
        public void PastTheList_TheLastSetRepeats_GrowingOncePerCycle()
        {
            var third = OfferingScheduleDef.QuotaFor(Cycles, 1.5f, 3);    // one past: × 1.5
            Assert.That(third[0].amount, Is.EqualTo(30f));
            Assert.That(third[1].amount, Is.EqualTo(15f));

            var fourth = OfferingScheduleDef.QuotaFor(Cycles, 1.5f, 4);   // two past: × 2.25
            Assert.That(fourth[0].amount, Is.EqualTo(45f));
            Assert.That(fourth[1].amount, Is.EqualTo(23f), "rounded UP to whole units");
        }

        [Test]
        public void TheQuotaIsACopy_ChangingItLeavesTheScheduleAlone()
        {
            var quota = OfferingScheduleDef.QuotaFor(Cycles, 1.5f, 1);
            quota[0].amount = 999f;

            Assert.That(Cycles[0].resources[0].amount, Is.EqualTo(10f));
        }

        [Test]
        public void NothingAuthored_OrNoCycleYet_AsksForNothing()
        {
            Assert.That(OfferingScheduleDef.QuotaFor(new List<OfferingQuota>(), 1.5f, 1), Is.Empty);
            Assert.That(OfferingScheduleDef.QuotaFor(null, 1.5f, 1), Is.Empty);
            Assert.That(OfferingScheduleDef.QuotaFor(Cycles, 1.5f, 0), Is.Empty);
        }

        // An empty or zero line is a half-finished row in the table, not a demand for nothing.
        [Test]
        public void EmptyLines_AreSkipped()
        {
            var cycles = new List<OfferingQuota> { Q((ResourceType.Wood, 0), (ResourceType.Food, 5)) };
            cycles[0].resources.Add(null);

            var quota = OfferingScheduleDef.QuotaFor(cycles, 1.5f, 1);
            Assert.That(quota.Count, Is.EqualTo(1));
            Assert.That(quota[0].resourceType, Is.EqualTo(ResourceType.Food));
        }

        [Test]
        public void EveryThirdCycle_IsSpecial()
        {
            Assert.That(OfferingScheduleDef.IsSpecialCycle(1, 3), Is.False);
            Assert.That(OfferingScheduleDef.IsSpecialCycle(2, 3), Is.False);
            Assert.That(OfferingScheduleDef.IsSpecialCycle(3, 3), Is.True);
            Assert.That(OfferingScheduleDef.IsSpecialCycle(6, 3), Is.True);
        }

        [Test]
        public void ZeroEvery_MeansNoSpecialCycles()
        {
            Assert.That(OfferingScheduleDef.IsSpecialCycle(3, 0), Is.False);
            Assert.That(OfferingScheduleDef.IsSpecialCycle(0, 3), Is.False);
        }
    }

    // The end-of-cycle window's words. Pure strings, so they run offline too.
    public class OfferingQuestionTests
    {
        private static readonly List<ResourceCost> Quota = new()
        {
            new ResourceCost { resourceType = ResourceType.Wood, amount = 40 },
            new ResourceCost { resourceType = ResourceType.Food, amount = 20 },
        };

        [Test]
        public void Question_NamesTheCycleAndTheQuota()
        {
            string q = OfferingEndState.Question(3, Quota, true);
            Assert.That(q, Does.Contain("Cycle 3"));
            Assert.That(q, Does.Contain("40 Wood"));
            Assert.That(q, Does.Contain("20 Food"));
        }

        [Test]
        public void Question_SaysSoWhenTheStockCannotPay()
        {
            Assert.That(OfferingEndState.Question(1, Quota, false), Does.Contain("enough"));
            Assert.That(OfferingEndState.Question(1, Quota, true), Does.Not.Contain("enough"));
        }
    }

    // The end-of-cycle window freezes the game and waits, so a path that reaches it with no way to answer
    // is a softlock — and one that ends the run unasked is worse. Only the refusal paths are pinned, for
    // PerkSelectionStateTests' reason: the rest hangs off RunManager.CurrentRun, which only StartNewRun fills.
    public class OfferingEndStateTests
    {
        private float originalTimeScale;

        [SetUp]
        public void SetUp() => originalTimeScale = Time.timeScale;

        [TearDown]
        public void TearDown() => Time.timeScale = originalTimeScale;

        private static OfferingEndState Build(StateMachine fsm, PlayingState playing) =>
            new OfferingEndState(fsm, null, null, null, null, playing, null, null);

        [Test]
        public void WithNothingWired_ItHandsControlBackToPlaying_WithoutFreezing()
        {
            Time.timeScale = 1f;
            var fsm = new StateMachine();
            var playing = new PlayingState();
            var end = Build(fsm, playing);

            fsm.ChangeState(end);
            Assert.That(fsm.Current, Is.SameAs(end), "Enter must not change state from inside itself.");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Enter froze the game on a path it cannot finish.");

            fsm.Tick();
            Assert.That(fsm.Current, Is.SameAs(playing));
        }

        [Test]
        public void LeavingAlwaysUnfreezesTheGame()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState();
            var end = Build(fsm, playing);

            fsm.ChangeState(end);
            Time.timeScale = 0f;            // as if the window had gone up normally

            fsm.ChangeState(playing);

            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
