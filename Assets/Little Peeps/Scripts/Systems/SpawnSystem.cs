using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Coordinates unit spawning: bridges Spawner components with UnitPool, enforces a global
    // active-unit cap per KIND of unit (cap = sum of all registered spawner capacities), and keeps
    // UnitSystem's live registry in sync via direct calls (no events). Also owns the registry of
    // ALL structure spawners (unit Spawners and AnimalSpawners, via IStructureSpawner) so build
    // mode can despawn-all + re-warm everything on enter/exit.
    //
    // Population is counted per UnitDef — the kind of unit a house spawns — never per profession:
    // a rack changes what a unit does mid-outing, not what it is. Spawn and despawn must count
    // against the same key, and the def is the one thing about a unit that never changes.
    public class SpawnSystem : MonoBehaviour
    {
        [SerializeField] private UnitPool unitPool;
        [SerializeField] private UnitSystem unitSystem;
        [SerializeField] private IslandSystem islandSystem;   // injected into spawned units; kept for future island-aware behavior

        private readonly Dictionary<UnitDef, int> capByDef = new();
        private readonly Dictionary<UnitDef, int> activeByDef = new();

        private readonly List<IStructureSpawner> spawners = new();   // live spawners (units + animals), for build-mode reset/warmup
        private readonly List<Unit> despawnBuffer = new();  // reused snapshot for DespawnAll

        private RunStats stats;   // injected into spawned units so their speed reflects run modifiers

        // Exposed for spawners, which have no other route to the run's stats. Read at the point of use
        // and NEVER cached: Initialize rebinds this on every new run, and a cached copy would leave a
        // spawner applying the finished run's bonuses.
        public RunStats Stats => stats;

        // True between build-mode enter (DespawnAllAndResetSpawners) and exit (WarmupAllSpawners).
        // Spawners read it so a structure PLACED during build mode registers itself but DEFERS
        // spawning its entities to the exit warmup — nothing materializes while the player is still
        // building; it all appears together the moment build mode ends.
        public bool IsBuildMode { get; private set; }

        // Injected by RunManager.StartNewRun so units spawned this run carry the run's stat sheet.
        //
        // Also the ONE subscription to that sheet. A house's capacity is materialised into slots at
        // warmup, so a perk or age that raises it mid-run has to be pushed to the houses already
        // standing — and this is the registry that knows them. The previous run's sheet dies with its
        // run, so the unsubscribe is bookkeeping rather than a leak fix; it is here so a re-Initialize
        // can never leave two sheets driving one registry.
        public void Initialize(RunContext run)
        {
            if (stats != null) stats.Changed -= RefreshSpawnersFromStats;
            stats = run.stats;
            if (stats != null) stats.Changed += RefreshSpawnersFromStats;
        }

        // Sheet changed mid-run: every live spawner re-resolves what it materialised from it.
        private void RefreshSpawnersFromStats()
        {
            for (int i = 0; i < spawners.Count; i++)
                spawners[i].RefreshFromStats();
        }

        private void Start()
        {
            if (islandSystem == null)
                Debug.LogWarning("SpawnSystem has no IslandSystem — spawned units won't receive an island reference. Wire the Island System field.", this);
        }

        // A spawner registers/unregisters itself when it warms up / is destroyed.
        public void RegisterSpawner(IStructureSpawner spawner)
        {
            if (spawner != null && !spawners.Contains(spawner)) spawners.Add(spawner);
        }

        public void UnregisterSpawner(IStructureSpawner spawner)
        {
            spawners.Remove(spawner);
        }

        // A spawner registers/unregisters its capacity when placed/removed.
        public void RegisterCapacity(UnitDef def, int amount)
        {
            if (def == null) return;
            capByDef.TryGetValue(def, out var cur);
            capByDef[def] = cur + amount;
        }

        public void UnregisterCapacity(UnitDef def, int amount)
        {
            if (def == null) return;
            capByDef.TryGetValue(def, out var cur);
            capByDef[def] = Mathf.Max(0, cur - amount);
        }

        public bool CanSpawn(UnitDef def)
        {
            if (def == null) return false;
            capByDef.TryGetValue(def, out var cap);
            activeByDef.TryGetValue(def, out var active);
            return active < cap;
        }

        // Create a unit at position if the global cap for its kind allows it. The caller launches it.
        public Unit TrySpawn(UnitDef def, Vector2 position)
        {
            if (!CanSpawn(def)) return null;

            var unit = unitPool.Get(def);
            if (unit == null) return null;

            unit.SetIsland(islandSystem);
            unit.SetStats(stats);
            unit.transform.position = position;
            activeByDef.TryGetValue(def, out var active);
            activeByDef[def] = active + 1;

            if (unitSystem != null) unitSystem.Add(unit);
            return unit;
        }

        // Every exit from the field goes through here (house teardown, build mode, run teardown), so
        // this is where a mid-outing profession ends: Unequip before the pool takes the unit — the
        // tool goes back on its rack — and the count comes off the def TrySpawn put it on.
        public void Despawn(Unit unit)
        {
            if (unit == null) return;

            unit.Unequip();

            if (unit.def != null)
            {
                activeByDef.TryGetValue(unit.def, out var active);
                activeByDef[unit.def] = Mathf.Max(0, active - 1);
            }

            if (unitSystem != null) unitSystem.Remove(unit);
            unitPool.Release(unit);
        }

        // Build-mode enter: return every live unit to the pool, then let each spawner clear its
        // per-entity bookkeeping (unit Spawners drop stale slot refs; AnimalSpawners destroy
        // their animals themselves — animals aren't pooled units).
        public void DespawnAllAndResetSpawners()
        {
            IsBuildMode = true;
            DespawnAll();
            for (int i = 0; i < spawners.Count; i++)
                spawners[i].ResetForBuildMode();
        }

        // Build-mode exit: each spawner re-warms (fresh resting unit per slot / fresh animals in
        // the territory) — this is "everything respawns from its building".
        public void WarmupAllSpawners()
        {
            IsBuildMode = false;
            for (int i = 0; i < spawners.Count; i++)
                spawners[i].Warmup();
        }

        // Run teardown: every unit back to the pool and every scrap of per-run bookkeeping dropped, so
        // the next run starts from a clean sheet instead of inheriting caps and spawner entries.
        //
        // Call this AFTER StructureSystem.ClearAll, which tears the spawners down synchronously — by the
        // time we get here the registries should already be empty and the Clear calls are the backstop
        // that guarantees it. IsBuildMode is forced off because a run can end from inside build mode:
        // left true, every spawner of the NEW run would register and then wait for a build-mode exit
        // that is never coming, and the fresh village would stand empty.
        public void ResetForNewRun()
        {
            IsBuildMode = false;
            DespawnAll();
            capByDef.Clear();
            activeByDef.Clear();
            spawners.Clear();
        }

        private void DespawnAll()
        {
            if (unitSystem == null) return;

            // Snapshot first: Despawn mutates UnitSystem's list via Remove while we iterate.
            despawnBuffer.Clear();
            despawnBuffer.AddRange(unitSystem.ActiveUnits);
            for (int i = 0; i < despawnBuffer.Count; i++)
                Despawn(despawnBuffer[i]);
            despawnBuffer.Clear();
        }
    }
}
