using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The offering cycle as data: how long each cycle lasts, what the gods want at the end of it, and
    // which cycles are special. One asset per run setup; OfferingSystem plays it.
    //
    // Time is counted in segments — the "years" of the time strip. One segment always lasts segmentSeconds
    // and is always as long on screen, so the strip moves at one speed for the whole run; a cycle is a
    // whole number of segments, so its offering always stands on a tick of the strip. segmentSeconds is the
    // one knob for the pace of the whole run.
    //
    // The list is authored cycle by cycle — "first only wood, then wood and food, then metal" is a design
    // decision, not a formula. A run is endless, so past the last entry the last set keeps coming back,
    // a little bigger every cycle (growthPastEnd), and as long as the last entry. The quota does NOT look
    // at the ages: a resource the player cannot produce yet is demanded all the same — the gods set the
    // pace, buying ages is how to keep up with it. So an entry must not ask for a resource before a normal
    // player reaches the age that opens it.
    [CreateAssetMenu(menuName = "LittlePeeps/Offering Schedule")]
    public class OfferingScheduleDef : ScriptableObject
    {
        [Tooltip("One segment of the time strip — one year — in seconds of play. The strip always moves one " +
                 "segment per this many seconds; how long a cycle lasts is set per cycle, in segments. The " +
                 "clock stands still on every screen that pauses the game (perk pick, age transition, the " +
                 "end-of-cycle window).")]
        [Min(1f)] public float segmentSeconds = 10f;

        [Tooltip("The cycles in order, the first entry is cycle 1: how many segments each lasts and what it " +
                 "asks for. Empty = no offerings at all, the cycle is switched off.")]
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

        // How long cycle `cycle` (1-based) lasts, in segments and in seconds.
        public int SegmentsOf(int cycle) => SegmentsFor(cycles, cycle);
        public float SecondsOf(int cycle) => SecondsFor(cycles, segmentSeconds, cycle);

        // How many segments the run has gone through when cycle `cycle` starts — its years so far.
        public int StartSegmentOf(int cycle) => StartSegmentFor(cycles, cycle);

        // The rules themselves, pure and static so they can be pinned without an asset.
        public static bool IsSpecialCycle(int cycle, int every) => every > 0 && cycle > 0 && cycle % every == 0;

        // Inside the list: the entry's own length. Past it: the last entry's — only the quota grows past the
        // list, never the length. Never less than one segment: a cycle of no time would be over the moment
        // it began. Before cycle 1 (or with nothing authored): nothing.
        public static int SegmentsFor(IReadOnlyList<OfferingQuota> cycles, int cycle)
        {
            if (cycles == null || cycles.Count == 0 || cycle < 1) return 0;

            var entry = cycles[Mathf.Min(cycle, cycles.Count) - 1];
            return entry != null ? Mathf.Max(1, entry.segments) : 1;
        }

        public static float SecondsFor(IReadOnlyList<OfferingQuota> cycles, float segmentSeconds, int cycle) =>
            SegmentsFor(cycles, cycle) * segmentSeconds;

        // The lengths of every cycle before this one, added up: the authored ones one by one, then the last
        // length once per cycle past the list. Cycle 1 (or nothing authored) starts at 0.
        public static int StartSegmentFor(IReadOnlyList<OfferingQuota> cycles, int cycle)
        {
            if (cycles == null || cycles.Count == 0 || cycle <= 1) return 0;

            int before = cycle - 1;
            int authored = Mathf.Min(before, cycles.Count);
            int start = 0;
            for (int c = 1; c <= authored; c++) start += SegmentsFor(cycles, c);
            return start + (before - authored) * SegmentsFor(cycles, cycles.Count);
        }

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

    // One cycle: how long it lasts and what the gods want at its end. A class of its own because Unity
    // cannot serialize a list of lists.
    [Serializable]
    public class OfferingQuota
    {
        [Tooltip("How long this cycle lasts, in segments of the time strip (× Segment Seconds). Its " +
                 "offering stands where the last segment ends.")]
        [Min(1)] public int segments = 6;

        public List<ResourceCost> resources = new();
    }
}
