using System;
using System.Collections.Generic;

namespace LittlePeeps
{
    // Persists across runs; serialized to JSON by SaveSystem.
    // Note: Dictionary<> is not supported by JsonUtility — SaveSystem wraps this in a serializable list.
    [Serializable]
    public class MetaContext
    {
        public int prestigePoints;

        // The prestige this profile has ALREADY been paid, one high-water mark per term of the payout.
        // A run earns only what it BEATS:
        //
        //     payout = max(0, ageTerm - agePointsAwarded) + max(0, harvestTerm - harvestPointsAwarded)
        //
        // So prestiging at age 3 on repeat, or farming the same harvest again, is worth exactly nothing,
        // and the lifetime total equals the payout of the single best run so far. Pushing further is the
        // only way to earn.
        //
        // Raised by PrestigeSystem.ExecutePrestige and by nothing else. Deliberately not "best age
        // reached" / "most ever harvested": a run abandoned without prestiging was never cashed in, so
        // it must not burn a record the player has not been paid for.
        //
        // Stored as POINTS rather than as ages and resources, so both terms subtract the same way and a
        // new term needs no new shape. The trade-off is that retuning PrestigeFormula does not
        // retroactively re-price what has already been paid out.
        public int agePointsAwarded;
        public int harvestPointsAwarded;

        // Keyed by UpgradeId; tracks how many times each global upgrade has been purchased
        [NonSerialized] public Dictionary<UpgradeId, int> globalUpgrades = new();

        // Return level for a specific upgrade; 0 if never purchased
        public int GetUpgradeLevel(UpgradeId id)
        {
            return globalUpgrades != null && globalUpgrades.TryGetValue(id, out var level) ? level : 0;
        }
    }
}
