using System;
using System.Collections.Generic;

// Persists across runs; serialized to JSON by SaveSystem.
// Note: Dictionary<> is not supported by JsonUtility — SaveSystem wraps this in a serializable list.
[Serializable]
public class MetaContext
{
    public int prestigePoints;

    // Keyed by UpgradeId; tracks how many times each global upgrade has been purchased
    [NonSerialized] public Dictionary<UpgradeId, int> globalUpgrades = new();

    // Return level for a specific upgrade; 0 if never purchased
    public int GetUpgradeLevel(UpgradeId id)
    {
        return globalUpgrades != null && globalUpgrades.TryGetValue(id, out var level) ? level : 0;
    }
}
