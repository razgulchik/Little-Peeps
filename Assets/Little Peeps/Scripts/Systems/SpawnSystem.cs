using System.Collections.Generic;
using UnityEngine;

// Coordinates unit spawning: bridges Spawner components with UnitPool, enforces a global
// active-unit cap per UnitType (cap = sum of all registered spawner capacities), and keeps
// UnitSystem's live registry in sync via direct calls (no events). Also owns the spawner
// registry so build mode can despawn-all + re-warm everything on enter/exit.
public class SpawnSystem : MonoBehaviour
{
    [SerializeField] private UnitPool unitPool;
    [SerializeField] private UnitSystem unitSystem;
    [SerializeField] private IslandSystem islandSystem;   // injected into spawned units for the on-island containment backstop

    private readonly Dictionary<UnitType, int> capByType = new();
    private readonly Dictionary<UnitType, int> activeByType = new();

    private readonly List<Spawner> spawners = new();   // live spawners, for build-mode reset/warmup
    private readonly List<Unit> despawnBuffer = new();  // reused snapshot for DespawnAll

    private void Start()
    {
        if (islandSystem == null)
            Debug.LogWarning("SpawnSystem has no IslandSystem — units won't be kept on the island (containment backstop disabled). Wire the Island System field.", this);
    }

    // A spawner registers/unregisters itself when it warms up / is destroyed.
    public void RegisterSpawner(Spawner spawner)
    {
        if (spawner != null && !spawners.Contains(spawner)) spawners.Add(spawner);
    }

    public void UnregisterSpawner(Spawner spawner)
    {
        spawners.Remove(spawner);
    }

    // A spawner registers/unregisters its capacity when placed/removed.
    public void RegisterCapacity(UnitType type, int amount)
    {
        capByType.TryGetValue(type, out var cur);
        capByType[type] = cur + amount;
    }

    public void UnregisterCapacity(UnitType type, int amount)
    {
        capByType.TryGetValue(type, out var cur);
        capByType[type] = Mathf.Max(0, cur - amount);
    }

    public bool CanSpawn(UnitType type)
    {
        capByType.TryGetValue(type, out var cap);
        activeByType.TryGetValue(type, out var active);
        return active < cap;
    }

    // Create a unit at position if the global cap for its type allows it. The caller launches it.
    public Unit TrySpawn(UnitDef def, Vector2 position)
    {
        if (def == null || !CanSpawn(def.unitType)) return null;

        var unit = unitPool.Get(def);
        if (unit == null) return null;

        unit.SetIsland(islandSystem);
        unit.transform.position = position;
        activeByType.TryGetValue(def.unitType, out var active);
        activeByType[def.unitType] = active + 1;

        if (unitSystem != null) unitSystem.Add(unit);
        return unit;
    }

    public void Despawn(Unit unit)
    {
        if (unit == null) return;

        var type = unit.Type;
        activeByType.TryGetValue(type, out var active);
        activeByType[type] = Mathf.Max(0, active - 1);

        if (unitSystem != null) unitSystem.Remove(unit);
        unitPool.Release(unit);
    }

    // Build-mode enter: return every live unit to the pool, then clear each spawner's slot
    // bookkeeping (the resting/flying units it referenced are now pooled).
    public void DespawnAllAndResetSpawners()
    {
        DespawnAll();
        for (int i = 0; i < spawners.Count; i++)
            spawners[i].ResetSlots();
    }

    // Build-mode exit: each spawner re-warms (spawns a fresh resting unit per slot) — this is
    // "units respawn from their buildings".
    public void WarmupAllSpawners()
    {
        for (int i = 0; i < spawners.Count; i++)
            spawners[i].BeginWarmup();
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
