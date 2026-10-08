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
        // Raised by PrestigeSystem.CashIn and by nothing else. Deliberately not "best age
        // reached" / "most ever harvested": a run abandoned without prestiging was never cashed in, so
        // it must not burn a record the player has not been paid for.
        //
        // Stored as POINTS rather than as ages and resources, so both terms subtract the same way and a
        // new term needs no new shape. The trade-off is that retuning PrestigeFormula does not
        // retroactively re-price what has already been paid out.
        public int agePointsAwarded;
        public int harvestPointsAwarded;

        // Keyed by GlobalUpgradeDef.id; tracks how many levels of each global upgrade have been purchased
        [NonSerialized] public Dictionary<string, int> globalUpgrades = new();

        // Bank a finished run: credit the payout and raise each record to what the run was worth GROSS.
        //
        // Max, never assignment. A run that fell short of a record must leave it standing — lowering it
        // would hand the difference back as payable, and the player could then earn the same stretch
        // again by repeating a run they had already been paid for. That is the whole mechanism, in one
        // operator, and getting it wrong is silent: everything still works, prestige just quietly
        // becomes farmable.
        //
        // Takes the gross terms rather than computing them, because the formula that produces them is
        // authored data (PrestigeFormula) and this class is plain profile state.
        public void BankPayout(int payout, int agePointsEarned, int harvestPointsEarned)
        {
            prestigePoints      += Math.Max(0, payout);
            agePointsAwarded     = Math.Max(agePointsAwarded, agePointsEarned);
            harvestPointsAwarded = Math.Max(harvestPointsAwarded, harvestPointsEarned);
        }

        // Return level for a specific upgrade; 0 if never purchased
        public int GetUpgradeLevel(string id)
        {
            return id != null && globalUpgrades != null && globalUpgrades.TryGetValue(id, out var level) ? level : 0;
        }

        // The spending side of prestigePoints. prestigePoints is everything ever banked and is never spent
        // DOWN: a level holds one point, and the points not held by any level are what the meta screen
        // offers. That is what makes re-spending free — lowering a level gives its point straight back,
        // with nothing to refund and no second balance to keep in step.
        public int PointsInvested
        {
            get
            {
                int sum = 0;
                if (globalUpgrades != null)
                    foreach (int level in globalUpgrades.Values) sum += Math.Max(0, level);
                return sum;
            }
        }

        public int FreePoints => Math.Max(0, prestigePoints - PointsInvested);

        // Plain ids and limits rather than GlobalUpgradeDef, so the rules stay testable without a
        // ScriptableObject. PrestigeSystem passes the def's id and maxLevel.
        public bool CanRaise(string id, int maxLevel) =>
            !string.IsNullOrEmpty(id) && FreePoints > 0 && GetUpgradeLevel(id) < maxLevel;

        public bool CanLower(string id) => GetUpgradeLevel(id) > 0;

        public bool TryRaise(string id, int maxLevel)
        {
            if (!CanRaise(id, maxLevel)) return false;

            globalUpgrades ??= new Dictionary<string, int>();
            globalUpgrades[id] = GetUpgradeLevel(id) + 1;
            return true;
        }

        public bool TryLower(string id)
        {
            if (!CanLower(id)) return false;

            int level = GetUpgradeLevel(id) - 1;
            if (level > 0) globalUpgrades[id] = level;
            else globalUpgrades.Remove(id);   // 0 is "never bought"; keep the book free of empty rows
            return true;
        }
    }
}
