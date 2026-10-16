using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The offering cycle as data: how long a cycle lasts, what the gods want at the end of each one, and
    // which cycles are special. One asset per run setup; OfferingSystem plays it.
    //
    // The list is authored cycle by cycle — "first only wood, then wood and food, then metal" is a design
    // decision, not a formula. A run is endless, so past the last entry the last set keeps coming back,
    // a little bigger every cycle (growthPastEnd). The quota does NOT look at the ages: a resource the
    // player cannot produce yet is demanded all the same — the gods set the pace, buying ages is how to
    // keep up with it. So an entry must not ask for a resource before a normal player reaches the age
    // that opens it.
    [CreateAssetMenu(menuName = "LittlePeeps/Offering Schedule")]
    public class OfferingScheduleDef : ScriptableObject
    {
        [Tooltip("Length of one cycle, in seconds of play. The clock stands still on every screen that " +
                 "pauses the game (perk pick, age transition, the end-of-cycle window).")]
        [Min(1f)] public float cycleSeconds = 180f;

        [Tooltip("What each cycle asks for, in order: the first entry is cycle 1. Empty = no offerings " +
                 "at all, the cycle is switched off.")]
        public List<OfferingQuota> cycles = new();

        [Tooltip("Past the last entry the last set repeats, multiplied by this once per extra cycle: " +
                 "1.25 = each cycle past the list asks a quarter more than the one before.")]
        [Min(1f)] public float growthPastEnd = 1.25f;

        [Tooltip("Every N-th cycle is special (3 = cycles 3, 6, 9…). 0 = none.")]
        [Min(0)] public int specialEvery = 3;

        public bool IsEmpty => cycles == null || cycles.Count == 0;

        public bool IsSpecial(int cycle) => IsSpecialCycle(cycle, specialEvery);

        // What cycle `cycle` (1-based) asks for — a fresh list, the caller may keep it.
        public List<ResourceCost> QuotaOf(int cycle) => QuotaFor(cycles, growthPastEnd, cycle);

        // The rules themselves, pure and static so they can be pinned without an asset.
        public static bool IsSpecialCycle(int cycle, int every) => every > 0 && cycle > 0 && cycle % every == 0;

        // Inside the list: the entry as authored. Past it: the last entry × growth^(cycles past the end),
        // rounded UP to whole units — a quota reads as a whole number, and rounding up can never turn a
        // demand into zero. Before cycle 1 (or with nothing authored): nothing.
        public static List<ResourceCost> QuotaFor(IReadOnlyList<OfferingQuota> cycles, float growth, int cycle)
        {
            var quota = new List<ResourceCost>();
            if (cycles == null || cycles.Count == 0 || cycle < 1) return quota;

            int index = Mathf.Min(cycle, cycles.Count) - 1;
            int past = cycle - cycles.Count;
            float scale = past > 0 ? Mathf.Pow(Mathf.Max(1f, growth), past) : 1f;

            var entry = cycles[index];
            if (entry == null || entry.resources == null) return quota;

            for (int i = 0; i < entry.resources.Count; i++)
            {
                var r = entry.resources[i];
                if (r == null || r.amount <= 0f) continue;

                float amount = past > 0 ? Mathf.Ceil(r.amount * scale) : r.amount;
                quota.Add(new ResourceCost { resourceType = r.resourceType, amount = amount });
            }
            return quota;
        }
    }

    // One cycle's demand. A class of its own because Unity cannot serialize a list of lists.
    [Serializable]
    public class OfferingQuota
    {
        public List<ResourceCost> resources = new();
    }
}
