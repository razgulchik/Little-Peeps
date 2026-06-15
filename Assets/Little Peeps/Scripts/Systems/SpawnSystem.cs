using System.Collections.Generic;
using UnityEngine;

// Coordinates unit spawning: bridges Spawner components with UnitPool and enforces a
// global active-unit cap per UnitType (cap = sum of all registered spawner capacities).
public class SpawnSystem : MonoBehaviour
{
    [SerializeField] private UnitPool unitPool;

    private readonly Dictionary<UnitType, int> capByType = new();
    private readonly Dictionary<UnitType, int> activeByType = new();

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

        unit.transform.position = position;
        activeByType.TryGetValue(def.unitType, out var active);
        activeByType[def.unitType] = active + 1;
        return unit;
    }

    public void Despawn(Unit unit)
    {
        if (unit == null) return;

        var type = unit.Type;
        activeByType.TryGetValue(type, out var active);
        activeByType[type] = Mathf.Max(0, active - 1);

        unitPool.Release(unit);
    }
}
