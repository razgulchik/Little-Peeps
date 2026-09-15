using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // HouseCapacity is the one MATERIALISED stat: Spawner.Warmup turns it into slots and a global cap
    // and never reads it again, so unlike every other stat a perk bought mid-run does not "just work"
    // on the next read — it has to be pushed (RunStats.Changed → SpawnSystem → RefreshFromStats).
    // These tests pin both halves: the resolve at warmup and the push after it.
    //
    // Same harness as RunTeardownTests: real GameObjects, SpawnSystem parked in build mode so Warmup
    // registers and reserves slots without pulling units from a pool this test does not have. That
    // also means growth here adds EMPTY slots, exactly what the build-mode branch promises; the
    // "new unit rests first" half of a mid-run raise is FillSlot's existing behaviour, not tested here.
    // Editor Test Runner only — the offline harness skips anything that builds a GameObject.
    public class HouseCapacityTests
    {
        private GameObject systemsGo;
        private SpawnSystem spawnSystem;
        private RunContext run;
        private UnitDef unitDef;
        private readonly List<GameObject> spawned = new();

        // Lumberjack, not Farmer: UnitType.Farmer is 0, so the unit-scope test below would pass on it
        // even if the scope were ignored. Same reasoning as RunStatsTests.
        private const UnitType Worker = UnitType.Lumberjack;
        private const UnitType Other = UnitType.Farmer;

        [SetUp]
        public void SetUp()
        {
            systemsGo = new GameObject("SpawnSystem");
            spawnSystem = systemsGo.AddComponent<SpawnSystem>();

            run = new RunContext();
            spawnSystem.Initialize(run);

            unitDef = ScriptableObject.CreateInstance<UnitDef>();
            unitDef.unitType = Worker;

            spawnSystem.DespawnAllAndResetSpawners();   // build mode: no pool needed, see class comment
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
            if (systemsGo != null) Object.DestroyImmediate(systemsGo);
            if (unitDef != null) Object.DestroyImmediate(unitDef);
        }

        private Spawner MakeSpawner(int baseCapacity)
        {
            var go = new GameObject($"Spawner({baseCapacity})");   // [RequireComponent] pulls in Structure
            spawned.Add(go);
            var spawner = go.AddComponent<Spawner>();
            spawner.unitDef = unitDef;
            spawner.capacity = baseCapacity;
            spawner.Initialize(spawnSystem, null, null);
            spawner.Warmup();
            return spawner;
        }

        private static StatModifier Capacity(float flat = 0f, float percent = 0f, UnitType unit = Worker)
            => new StatModifier { id = StatId.HouseCapacity, unitScope = unit, flat = flat, percent = percent };

        // --- resolve at warmup -----------------------------------------------------------------------

        [Test]
        public void Warmup_AppliesTheRunModifierOnTopOfTheBase()
        {
            run.stats.Add(Capacity(flat: 1f));

            Assert.That(MakeSpawner(1).SlotCount, Is.EqualTo(2));
        }

        [Test]
        public void Warmup_IsScopedToTheUnitType()
        {
            run.stats.Add(Capacity(flat: 1f, unit: Other));

            Assert.That(MakeSpawner(1).SlotCount, Is.EqualTo(1), "a farmer bonus must not grow a lumberjack house");
        }

        [Test]
        public void Warmup_RoundsDown()
        {
            run.stats.Add(Capacity(percent: 0.5f));

            Assert.That(MakeSpawner(1).SlotCount, Is.EqualTo(1), "1.5 is still one slot");
            Assert.That(MakeSpawner(2).SlotCount, Is.EqualTo(3));
        }

        [Test]
        public void Warmup_NeverGoesBelowOneSlot()
        {
            run.stats.Add(Capacity(flat: -5f));

            Assert.That(MakeSpawner(1).SlotCount, Is.EqualTo(1));
        }

        [Test]
        public void Warmup_DoesNotLoseASlotToFloatNoise()
        {
            // Three -20% on a base of 10 is exactly 4, but the float sum of the percents makes it
            // 3.9999998 — a raw floor would hand out three. Pins that the spawner resolves through
            // RunStats.ApplyCount (which carries the epsilon) and not a floor of its own.
            for (int i = 0; i < 3; i++) run.stats.Add(Capacity(percent: -0.2f));

            Assert.That(MakeSpawner(10).SlotCount, Is.EqualTo(4));
        }

        // --- push after warmup -----------------------------------------------------------------------

        [Test]
        public void ARaiseMidRun_GrowsTheHousesAlreadyStanding()
        {
            var house = MakeSpawner(1);
            Assert.That(house.SlotCount, Is.EqualTo(1), "sanity");

            run.stats.Add(Capacity(flat: 1f));

            Assert.That(house.SlotCount, Is.EqualTo(2));
        }

        [Test]
        public void ASecondRaise_GrowsByTheDifferenceOnly()
        {
            var house = MakeSpawner(1);

            run.stats.Add(Capacity(flat: 1f));
            run.stats.Add(Capacity(flat: 1f));

            Assert.That(house.SlotCount, Is.EqualTo(3), "each refresh resolves the TOTAL and grows to it, never stacks on itself");
        }

        [Test]
        public void ANegativeModifierMidRun_DoesNotShrinkAStandingHouse()
        {
            var standing = MakeSpawner(2);

            run.stats.Add(Capacity(flat: -1f));

            Assert.That(standing.SlotCount, Is.EqualTo(2), "DecreaseCapacity is a stub: growth only");
            Assert.That(MakeSpawner(2).SlotCount, Is.EqualTo(1), "but a house warmed up after it resolves the smaller number");
        }

        [Test]
        public void Teardown_GivesBackTheGrownCount_NotTheBase()
        {
            var house = MakeSpawner(1);
            run.stats.Add(Capacity(flat: 1f));   // cap is now 2

            house.Teardown();

            Assert.IsFalse(spawnSystem.CanSpawn(Worker),
                           "the cap must be back at 0: giving back only the base of 1 would leave a phantom slot");
        }

        [Test]
        public void ANewRun_UnbindsTheOldSheet()
        {
            var house = MakeSpawner(1);

            // Mirrors RunManager: the new run's starting modifiers land BEFORE the systems initialise
            // on it. With the old subscription still alive, the old sheet's raise below would refresh
            // the house against THIS sheet and grow it.
            var next = new RunContext();
            next.stats.Add(Capacity(flat: 1f));
            spawnSystem.Initialize(next);

            run.stats.Add(Capacity(flat: 1f));

            Assert.That(house.SlotCount, Is.EqualTo(1), "a raise on the finished run's sheet must reach nobody");
        }
    }
}
