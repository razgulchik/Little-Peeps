using System.Collections.Generic;
using UnityEngine;

// Maintains the live registry of active units; driven by spawn/despawn events
public class UnitSystem : MonoBehaviour
{
    private readonly List<Unit> activeUnits = new();

    public IReadOnlyList<Unit> ActiveUnits => activeUnits;

    private void OnEnable()
    {
        // TODO: EventBus<UnitSpawnedEvent>.Subscribe(OnUnitSpawned); EventBus<UnitDespawnedEvent>.Subscribe(OnUnitDespawned)
    }

    private void OnDisable()
    {
        // TODO: unsubscribe both handlers
    }

    private void OnUnitSpawned(UnitSpawnedEvent e)
    {
        // TODO: activeUnits.Add(e.Unit)
    }

    private void OnUnitDespawned(UnitDespawnedEvent e)
    {
        // TODO: activeUnits.Remove(e.Unit)
    }
}
