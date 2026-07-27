using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // Guards the accounting hazard at the run boundary: Destroy() defers OnDestroy to the END of the
    // frame, but a prestige ends one run and starts the next INSIDE one frame. Left to OnDestroy, the
    // old run's spawners give their capacity back after the new run has already claimed its own, so the
    // fresh village is short by the old total — and because UnregisterCapacity clamps at zero, an old
    // run that grew bigger than the new one's starting layout drives the new cap to 0 and nothing ever
    // spawns. Nothing about that is visible in a diff, which is why it is pinned here.
    //
    // The fix is IStructureSpawner.Teardown: unregister synchronously, then make OnDestroy a no-op.
    //
    // These tests build real GameObjects, so they run in the Editor's Test Runner and are skipped by the
    // offline reflection harness (no native Unity there).
    public class RunTeardownTests
    {
        private GameObject systemsGo;
        private SpawnSystem spawnSystem;
        private UnitDef unitDef;

        // Lumberjack, not Farmer: UnitType.Farmer is 0, so a test written on it still passes when the
        // per-type bookkeeping does nothing at all. Same reasoning as the scope tests in RunStatsTests.
        private const UnitType Worker = UnitType.Lumberjack;

        [SetUp]
        public void SetUp()
        {
            systemsGo = new GameObject("SpawnSystem");
            spawnSystem = systemsGo.AddComponent<SpawnSystem>();

            unitDef = ScriptableObject.CreateInstance<UnitDef>();
            unitDef.unitType = Worker;

            // Park the system in build mode: Warmup then registers capacity and reserves slots but
            // returns before pulling units out of the pool, so these tests need no unit prefab.
            spawnSystem.DespawnAllAndResetSpawners();
        }

        [TearDown]
        public void TearDown()
        {
            if (systemsGo != null) Object.DestroyImmediate(systemsGo);
            if (unitDef != null) Object.DestroyImmediate(unitDef);
        }

        private Spawner MakeSpawner(int capacity)
        {
            var go = new GameObject($"Spawner({capacity})");   // [RequireComponent] pulls in Structure
            var spawner = go.AddComponent<Spawner>();
            spawner.unitDef = unitDef;
            spawner.capacity = capacity;
            spawner.Initialize(spawnSystem, null, null);
            spawner.Warmup();
            return spawner;
        }

        [Test]
        public void Teardown_ThenTheDeferredOnDestroy_DoesNotUnregisterTheCapacityTwice()
        {
            // A big finished run: five slots registered.
            var oldRun = MakeSpawner(5);
            Assert.IsTrue(spawnSystem.CanSpawn(Worker), "sanity: capacity is registered on warmup");

            // Prestige: tear down, then Unity gets round to OnDestroy.
            oldRun.Teardown();
            Object.DestroyImmediate(oldRun.gameObject);

            // The new run's starting layout is smaller than what the player had grown.
            MakeSpawner(1);

            Assert.IsTrue(spawnSystem.CanSpawn(Worker),
                          "the new run's single slot must survive: if the old spawner's 5 were taken " +
                          "away a second time the cap clamps to 0 and the village never spawns");
        }

        [Test]
        public void Teardown_IsSafeToCallTwice()
        {
            var spawner = MakeSpawner(3);

            spawner.Teardown();
            Assert.DoesNotThrow(() => spawner.Teardown());

            MakeSpawner(1);
            Assert.IsTrue(spawnSystem.CanSpawn(Worker), "a repeated teardown must not keep subtracting");

            Object.DestroyImmediate(spawner.gameObject);
        }

        [Test]
        public void OnDestroyAlone_StillUnregisters_WhenNoTeardownRan()
        {
            // The ordinary single-structure path (sell/remove) never calls Teardown, so OnDestroy has to
            // keep doing the whole job on its own — the guard must not have turned it into a no-op.
            var spawner = MakeSpawner(2);
            Object.DestroyImmediate(spawner.gameObject);

            Assert.IsFalse(spawnSystem.CanSpawn(Worker),
                           "destroying the only spawner must give its capacity back");
        }

        [Test]
        public void ResetForNewRun_ClearsTheCapAndLeavesBuildModeOff()
        {
            MakeSpawner(4);

            spawnSystem.ResetForNewRun();

            Assert.IsFalse(spawnSystem.CanSpawn(Worker), "capacity from the finished run must be gone");
            Assert.IsFalse(spawnSystem.IsBuildMode,
                           "a run ended from inside build mode must not leave the flag set: the next " +
                           "run's spawners would wait for an exit that never comes");
        }
    }
}
