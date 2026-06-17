using System.Collections.Generic;
using UnityEngine;

// Live registry of all active (out-of-pool) units. Fed directly by SpawnSystem on spawn/despawn
// (no events — single consumer). Home for bulk operations over the current population
// (e.g. tap-AoE boost — added later).
public class UnitSystem : MonoBehaviour
{
    private readonly List<Unit> activeUnits = new();

    public IReadOnlyList<Unit> ActiveUnits => activeUnits;

    public void Add(Unit unit)
    {
        if (unit != null) activeUnits.Add(unit);
    }

    public void Remove(Unit unit)
    {
        if (unit != null) activeUnits.Remove(unit);
    }
}
