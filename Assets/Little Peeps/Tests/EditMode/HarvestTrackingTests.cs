using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The prestige payout is built on RunContext.harvested, so what does and does not land in that
    // ledger IS the economy's exploit surface.
    //
    // ResourceSystem has two entry points on purpose: AddHarvest for production (multiplied) and
    // AddResource for spends, refunds and exact corrections (raw). Route a refund through the wrong one
    // and build → sell → build becomes an infinite prestige generator — the player never loses a
    // resource, and every rebuild pays again. Nothing about that shows up in a diff, so it is pinned here.
    //
    // These tests build real GameObjects, so they run in the Editor's Test Runner and are skipped by the
    // offline reflection harness (no native Unity there). The arithmetic of the payout itself needs no
    // scene and lives in PrestigeFormulaTests.
    public class HarvestTrackingTests
    {
        private GameObject systemsGo;
        private ResourceSystem resourceSystem;
        private RunContext run;

        // AddHarvest takes the whole source def, since the def is the yield modifier's source scope.
        private ResourceSourceDef stoneSource;
        private ResourceSourceDef woodSource;

        // Non-zero on purpose: Stone is not ResourceType 0 and Miner is not UnitType 0, so a per-type
        // ledger that ignored its key would still fail these. Same reasoning as RunStatsTests.
        private const ResourceType Harvested = ResourceType.Stone;
        private const UnitType Worker = UnitType.Miner;

        [SetUp]
        public void SetUp()
        {
            systemsGo = new GameObject("ResourceSystem");
            resourceSystem = systemsGo.AddComponent<ResourceSystem>();

            stoneSource = ScriptableObject.CreateInstance<ResourceSourceDef>();
            stoneSource.resource = Harvested;
            woodSource = ScriptableObject.CreateInstance<ResourceSourceDef>();
            woodSource.resource = ResourceType.Wood;

            run = new RunContext();
            resourceSystem.Initialize(run);
        }

        [TearDown]
        public void TearDown()
        {
            if (systemsGo != null) Object.DestroyImmediate(systemsGo);
            if (stoneSource != null) Object.DestroyImmediate(stoneSource);
            if (woodSource != null) Object.DestroyImmediate(woodSource);

            // AddResource publishes on a static bus; don't leave this test's traffic wired to the next.
            EventBus<ResourceChangedEvent>.Clear();
        }

        private float Ledger(ResourceType type)
            => run.harvested.TryGetValue(type, out float total) ? total : 0f;

        [Test]
        public void AddHarvest_CreditsTheWalletAndTheLedgerAlike()
        {
            resourceSystem.AddHarvest(stoneSource, Worker, 3f);

            Assert.That(resourceSystem.GetResource(Harvested), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(Ledger(Harvested), Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void AddHarvest_BooksTheAmountAfterTheProductionMultipliers()
        {
            // A village built well enough to double its output is worth double the prestige — that is
            // the design decision this pins. Booking baseAmount instead would make the payout measure
            // how many times a worker walked, and no perk or age would ever move it.
            run.stats.Add(new StatModifier { id = StatId.ProductionGlobal, percent = 1f });

            resourceSystem.AddHarvest(stoneSource, Worker, 3f);

            Assert.That(Ledger(Harvested), Is.EqualTo(6f).Within(1e-4f));
        }

        [Test]
        public void AddHarvest_AccumulatesPerType()
        {
            resourceSystem.AddHarvest(stoneSource, Worker, 3f);
            resourceSystem.AddHarvest(stoneSource, Worker, 4f);
            resourceSystem.AddHarvest(woodSource, Worker, 5f);

            Assert.That(Ledger(Harvested), Is.EqualTo(7f).Within(1e-4f));
            Assert.That(Ledger(ResourceType.Wood), Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void AddResource_DoesNotCountAsProduction()
        {
            // The refund path. The wallet moves, the ledger must not.
            resourceSystem.AddResource(Harvested, 50f);

            Assert.That(resourceSystem.GetResource(Harvested), Is.EqualTo(50f).Within(1e-4f));
            Assert.That(Ledger(Harvested), Is.EqualTo(0f),
                        "a resource the player was handed back is not a resource the village produced");
        }

        [Test]
        public void BuildingAndSellingInACycle_EarnsNoPrestige()
        {
            // The exploit, played out: harvest once, then spend and refund the same amount forever.
            resourceSystem.AddHarvest(stoneSource, Worker, 10f);

            for (int i = 0; i < 5; i++)
            {
                resourceSystem.AddResource(Harvested, -10f);   // build
                resourceSystem.AddResource(Harvested, 10f);    // sell it straight back
            }

            Assert.That(Ledger(Harvested), Is.EqualTo(10f).Within(1e-4f),
                        "five build/sell cycles must be worth exactly the one harvest that funded them");
        }

        [Test]
        public void Initialize_KeepsTheSameReactiveValue_SoTheUiStaysBound()
        {
            // ResourcePanel binds each row to this object ONCE, in Start(), and holds it for the rest of
            // the session. Replacing it on a new run strands the whole resource bar on the finished
            // run's instance: nothing writes to it again, so the numbers freeze at whatever they were
            // the moment the player prestiged while the real ones move on invisibly.
            var slot = resourceSystem.GetReactive(Harvested);
            float seenByTheUi = -1f;
            slot.OnChanged += v => seenByTheUi = v;

            var next = new RunContext();
            next.resources[Harvested] = 10f;
            resourceSystem.Initialize(next);

            Assert.That(resourceSystem.GetReactive(Harvested), Is.SameAs(slot),
                        "the slot belongs to the resource type, which outlives the run");
            Assert.That(seenByTheUi, Is.EqualTo(10f).Within(1e-4f),
                        "and the reset has to reach subscribers, so the bar drops to the new start");

            resourceSystem.AddHarvest(stoneSource, Worker, 5f);

            Assert.That(seenByTheUi, Is.EqualTo(15f).Within(1e-4f),
                        "and keep reaching them for the whole of the new run");
        }

        [Test]
        public void Initialize_BindsTheNewRunsLedger_AndLeavesTheFinishedOneAlone()
        {
            resourceSystem.AddHarvest(stoneSource, Worker, 10f);
            var finished = run;

            // What a prestige does: a fresh RunContext, then ResourceSystem.Initialize against it.
            run = new RunContext();
            resourceSystem.Initialize(run);
            resourceSystem.AddHarvest(stoneSource, Worker, 2f);

            Assert.That(Ledger(Harvested), Is.EqualTo(2f).Within(1e-4f),
                        "the new run starts its own count, not the finished run's");
            Assert.That(finished.harvested[Harvested], Is.EqualTo(10f).Within(1e-4f),
                        "the finished run's total must stay readable — the payout is computed from it");
        }
    }
}
