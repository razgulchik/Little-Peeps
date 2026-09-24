using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Where one tile of a rising zone is in its life.
    public enum IslandRisePhase
    {
        Waiting,      // not started: only water there
        Underwater,   // rising from the depth, seen only through the water
        Hop,          // broke the surface, in the air
        Squash,       // touched down, springing back
        Settled       // a real tilemap tile from here on
    }

    // One tile at one moment: its phase, how far through that phase it is, and how long ago it broke the
    // surface (negative before) — the drying runs across phases, so it needs its own clock.
    public readonly struct IslandRiseTileState
    {
        public readonly IslandRisePhase phase;
        public readonly float progress;      // 0..1 through the phase
        public readonly float sinceBreach;   // seconds

        public IslandRiseTileState(IslandRisePhase phase, float progress, float sinceBreach)
        {
            this.phase = phase;
            this.progress = progress;
            this.sinceBreach = sinceBreach;
        }
    }

    // Something that pops up in act 2 — a tree, a mountain, a den: the cells it stands on and how long its
    // pop lasts. It waits for every one of its cells to settle, so a two-cell den never pops half on water.
    // `flowIndex` places it along the zone's river (its index on the river's path; -1 = not river): river
    // cells pop one after another from the source instead of each on its own.
    public readonly struct IslandRiseItem
    {
        public readonly IReadOnlyCollection<Vector2Int> cells;
        public readonly float duration;
        public readonly int flowIndex;

        public IslandRiseItem(IReadOnlyCollection<Vector2Int> cells, float duration, int flowIndex = -1)
        {
            this.cells = cells;
            this.duration = duration;
            this.flowIndex = flowIndex;
        }
    }

    // When every tile of a rising zone does what, and when every pop of its content starts — worked out
    // once, before the first frame. The whole rise is then a function of one number, the time: any frame
    // can be asked for directly (TileAt, ItemProgressAt), which is what gives scrubbing, the tap speed-up
    // and the skip for free, and why nothing in the rise is a tween.
    //
    // Pure C#: no scene, no assets, no Unity calls that need the engine. The noise is its own hash for
    // that reason — Mathf.PerlinNoise lives in the engine, and the offline test harness cannot reach it.
    //
    // The wave: every cell gets a place in the zone between 0 (rises first) and 1 (last) from the order
    // mode, nudged by the jitter, and starts at leadIn + cascade · place^(1/crescendo). The place is
    // normalised, so a big zone takes as long as a small one; the root bends the pace, so the first
    // tiles come one by one and the last ones in a rush.
    //
    // The finale is a breath: from the join outwards, a cell a step further along starting a moment later,
    // the new zone lifts a little and settles back. The banner waits for it to cross the whole zone.
    public sealed class IslandRiseSchedule
    {
        // Largest jitter shift, as a share of the cascade, at jitter 1.
        private const float JitterReach = 0.15f;

        // Salts keep the random draws independent: the order, the jitter and the pops each get their own.
        private const int SaltRandomOrder = 1;
        private const int SaltNoise = 2;
        private const int SaltJitter = 3;
        private const int SaltPop = 4;

        // The zone's cells in IslandShape.CellOrder — a fixed order whatever set they came in, so the same
        // zone and seed always give the same schedule. Every per-cell accessor takes an index into this.
        public IReadOnlyList<Vector2Int> Cells => cells;

        // Cell indices in the order they start rising (which is also the order they break the surface,
        // touch down and settle: every tile takes the same time for each).
        public IReadOnlyList<int> CellsInOrder => cellOrder;

        // Item indices in the order they pop.
        public IReadOnlyList<int> ItemsInOrder => itemOrder;
        public int ItemCount => itemStarts.Length;

        // When the finale starts — the breath and the sparkles: everything has settled and popped, plus the
        // finale delay.
        public float Finale { get; }

        // When the rise is over and the age banner goes up: the finale's length or the breath's end,
        // whichever is later. 0 for an empty zone: nothing to play.
        public float Duration { get; }

        private readonly List<Vector2Int> cells;
        private readonly Dictionary<Vector2Int, int> index = new();
        private readonly float[] starts;
        private readonly int[] notes;
        private readonly int[] cellOrder;
        private readonly int[] steps;      // side-to-side steps from the join (the breath travels by these)
        private readonly float[] itemStarts;
        private readonly float[] itemDurations;
        private readonly int[] itemOrder;

        // Copied from the timing at build time: a profile tuned while this plays must not move the phase
        // edges of tiles whose start times were computed from the old values.
        private readonly float depthRise, hop, squash, breathTime, breathSpeed;

        // zone: the new cells. isOldLand: whether a cell is land already — zone cells are ignored, so the
        // island's grid can be passed as is even after the zone went into it. items: act 2, may be null.
        public IslandRiseSchedule(IslandRiseTiming timing, IReadOnlyCollection<Vector2Int> zone,
                                  Func<Vector2Int, bool> isOldLand, IReadOnlyList<IslandRiseItem> items, int seed)
        {
            if (timing == null) throw new ArgumentNullException(nameof(timing));

            depthRise = Mathf.Max(1e-4f, timing.depthRise);
            hop = Mathf.Max(1e-4f, timing.hop);
            squash = Mathf.Max(0f, timing.squash);
            breathTime = Mathf.Max(0f, timing.breathTime);
            breathSpeed = Mathf.Max(1f, timing.breathSpeed);

            cells = zone != null ? IslandShape.Sorted(zone) : new List<Vector2Int>();
            for (int i = 0; i < cells.Count; i++) index[cells[i]] = i;

            int n = cells.Count;
            starts = new float[n];
            notes = new int[n];
            cellOrder = new int[n];

            if (n == 0)
            {
                steps = Array.Empty<int>();
                itemStarts = Array.Empty<float>();
                itemDurations = Array.Empty<float>();
                itemOrder = Array.Empty<int>();
                return;
            }

            var join = JoinCells(isOldLand);
            steps = StepsFromJoin(join);
            var place = Normalised(RawOrder(timing, join, seed));
            float crescendo = Mathf.Max(1f, timing.crescendo);
            for (int i = 0; i < n; i++)
            {
                var c = cells[i];
                float shift = (Hash01(c.x, c.y, seed, SaltJitter) - 0.5f) * 2f * timing.jitter * JitterReach;
                float p = Mathf.Clamp01(place[i] + shift);
                starts[i] = timing.leadIn + timing.cascade * Mathf.Pow(p, 1f / crescendo);
            }

            for (int i = 0; i < n; i++) cellOrder[i] = i;
            Array.Sort(cellOrder, (a, b) => starts[a] != starts[b] ? starts[a].CompareTo(starts[b]) : a.CompareTo(b));
            for (int rank = 0; rank < n; rank++) notes[cellOrder[rank]] = rank;

            float end = SettleOf(cellOrder[n - 1]);
            (itemStarts, itemDurations, itemOrder) = ScheduleItems(timing, items, seed);
            for (int j = 0; j < itemStarts.Length; j++) end = Mathf.Max(end, itemStarts[j] + itemDurations[j]);

            Finale = end + timing.finaleDelay;
            int deepest = 0;
            foreach (int s in steps) deepest = Math.Max(deepest, s);
            Duration = Finale + Mathf.Max(timing.finaleLength, deepest / breathSpeed + breathTime);
        }

        public bool TryGetIndex(Vector2Int cell, out int i) => index.TryGetValue(cell, out i);

        public float StartOf(int cell) => starts[cell];
        public float BreachOf(int cell) => starts[cell] + depthRise;
        public float TouchDownOf(int cell) => BreachOf(cell) + hop;
        public float SettleOf(int cell) => TouchDownOf(cell) + squash;

        // Rank of the cell's breach, 0 = first to break the surface: the step of its note up the scale.
        public int NoteOf(int cell) => notes[cell];

        public IslandRiseTileState TileAt(int cell, float t)
        {
            float local = t - starts[cell];
            float sinceBreach = local - depthRise;
            if (local < 0f) return new IslandRiseTileState(IslandRisePhase.Waiting, 0f, sinceBreach);
            if (local < depthRise) return new IslandRiseTileState(IslandRisePhase.Underwater, local / depthRise, sinceBreach);
            if (sinceBreach < hop) return new IslandRiseTileState(IslandRisePhase.Hop, sinceBreach / hop, sinceBreach);
            float sinceTouchDown = sinceBreach - hop;
            if (sinceTouchDown < squash) return new IslandRiseTileState(IslandRisePhase.Squash, sinceTouchDown / squash, sinceBreach);
            return new IslandRiseTileState(IslandRisePhase.Settled, 1f, sinceBreach);
        }

        // When the breath reaches the cell, and how far through its own breath it is at t: 0..1 while it
        // breathes, and false outside that.
        public float BreathStartOf(int cell) => Finale + steps[cell] / breathSpeed;

        public bool BreathAt(int cell, float t, out float progress)
        {
            float local = t - BreathStartOf(cell);
            progress = breathTime > 0f ? local / breathTime : 1f;
            return local >= 0f && local < breathTime;
        }

        public float ItemStartOf(int item) => itemStarts[item];
        public float ItemDurationOf(int item) => itemDurations[item];

        public bool ItemStartedAt(int item, float t) => t >= itemStarts[item];

        // 0 at the start of the pop, 1 once it is done (and before it starts — ask ItemStartedAt first).
        public float ItemProgressAt(int item, float t)
        {
            if (t < itemStarts[item]) return 0f;
            float duration = itemDurations[item];
            return duration > 0f ? Mathf.Clamp01((t - itemStarts[item]) / duration) : 1f;
        }

        // ---- the wave ----

        // Each cell's raw place in the wave, in whatever units the order mode measures; lower = earlier.
        private float[] RawOrder(IslandRiseTiming timing, List<int> join, int seed)
        {
            int n = cells.Count;
            var raw = new float[n];

            if (timing.order == IslandRiseOrder.Random)
            {
                for (int i = 0; i < n; i++) raw[i] = Hash01(cells[i].x, cells[i].y, seed, SaltRandomOrder);
                return raw;
            }

            switch (timing.order)
            {
                case IslandRiseOrder.Radial:
                {
                    var centre = Centroid(join);
                    for (int i = 0; i < n; i++) raw[i] = Vector2.Distance(cells[i], centre);
                    break;
                }
                case IslandRiseOrder.Sweep:
                {
                    var from = Centroid(join);
                    var all = new List<int>(n);
                    for (int i = 0; i < n; i++) all.Add(i);
                    var direction = Centroid(all) - from;
                    if (direction.sqrMagnitude < 1e-6f) direction = Vector2.up;   // the join IS the middle: any line will do
                    direction.Normalize();
                    for (int i = 0; i < n; i++) raw[i] = Vector2.Dot((Vector2)cells[i] - from, direction);
                    break;
                }
                default:   // Join, JoinNoise
                {
                    int deepest = 0;
                    foreach (int s in steps) deepest = Math.Max(deepest, s);

                    float bend = timing.order == IslandRiseOrder.JoinNoise ? deepest * timing.noiseStrength : 0f;
                    float scale = Mathf.Max(0.5f, timing.noiseScale);
                    for (int i = 0; i < n; i++)
                    {
                        raw[i] = steps[i];
                        if (bend > 0f)
                            raw[i] += (ValueNoise(cells[i].x / scale, cells[i].y / scale, seed) - 0.5f) * bend;
                    }
                    break;
                }
            }
            return raw;
        }

        // Zone cells that touch the old island along a side. A zone always touches it, but one handed in
        // on its own (a test, the tuning window without an island) still needs somewhere to grow from:
        // then it grows from its first cell rather than all at once.
        private List<int> JoinCells(Func<Vector2Int, bool> isOldLand)
        {
            var join = new List<int>();
            if (isOldLand != null)
                for (int i = 0; i < cells.Count; i++)
                    foreach (var d in IslandShape.Dirs)
                    {
                        var neighbour = cells[i] + d;
                        if (index.ContainsKey(neighbour) || !isOldLand(neighbour)) continue;
                        join.Add(i);
                        break;
                    }
            if (join.Count == 0) join.Add(0);
            return join;
        }

        // Steps from the join through the zone's own cells, side to side. A piece of the zone the join
        // cannot reach (the generator does not make them, but nothing here relies on that) rises last.
        private int[] StepsFromJoin(List<int> join)
        {
            var steps = new int[cells.Count];
            for (int i = 0; i < steps.Length; i++) steps[i] = -1;

            var queue = new Queue<int>();
            foreach (int j in join)
            {
                steps[j] = 0;
                queue.Enqueue(j);
            }

            int deepest = 0;
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                foreach (var d in IslandShape.Dirs)
                {
                    if (!index.TryGetValue(cells[i] + d, out int k) || steps[k] >= 0) continue;
                    steps[k] = steps[i] + 1;
                    deepest = Math.Max(deepest, steps[k]);
                    queue.Enqueue(k);
                }
            }

            for (int i = 0; i < steps.Length; i++)
                if (steps[i] < 0) steps[i] = deepest + 1;
            return steps;
        }

        private Vector2 Centroid(List<int> of)
        {
            var sum = Vector2.zero;
            foreach (int i in of) sum += cells[i];
            return sum / of.Count;
        }

        // Rescaled to 0..1. A zone whose cells all measure the same (one cell; a mode with nothing to tell
        // them apart) starts together.
        private static float[] Normalised(float[] raw)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (float v in raw)
            {
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }

            var result = new float[raw.Length];
            float span = max - min;
            if (span <= 1e-6f) return result;
            for (int i = 0; i < raw.Length; i++) result[i] = (raw[i] - min) / span;
            return result;
        }

        // ---- act 2 ----

        // The pops follow the land: ordered by when the last of their cells settles, spread evenly over
        // contentSpread from contentStart on, and never before their own land is down.
        private (float[] starts, float[] durations, int[] order) ScheduleItems(
            IslandRiseTiming timing, IReadOnlyList<IslandRiseItem> items, int seed)
        {
            int m = items?.Count ?? 0;
            var popStarts = new float[m];
            var durations = new float[m];
            var order = new int[m];
            if (m == 0) return (popStarts, durations, order);

            var ready = new float[m];   // when the item's land has settled
            for (int j = 0; j < m; j++)
            {
                durations[j] = Mathf.Max(0f, items[j].duration);
                if (items[j].cells == null) continue;
                foreach (var cell in items[j].cells)
                    if (index.TryGetValue(cell, out int i)) ready[j] = Mathf.Max(ready[j], SettleOf(i));
            }

            for (int j = 0; j < m; j++) order[j] = j;
            Array.Sort(order, (a, b) => ready[a] != ready[b] ? ready[a].CompareTo(ready[b]) : a.CompareTo(b));

            float act2 = timing.leadIn + timing.cascade * timing.contentStart;
            for (int rank = 0; rank < m; rank++)
            {
                int j = order[rank];
                float spread = m > 1 ? (float)rank / (m - 1) * timing.contentSpread : 0f;
                float shift = (Hash01(j, rank, seed, SaltPop) - 0.5f) * 2f * timing.contentJitter;
                popStarts[j] = Mathf.Max(act2 + spread + shift, ready[j] + timing.contentGap);
            }
            RunRiver(timing, items, ready, popStarts);

            // The gap can push a pop past the next one; the order is the order they actually start in.
            Array.Sort(order, (a, b) => popStarts[a] != popStarts[b] ? popStarts[a].CompareTo(popStarts[b]) : a.CompareTo(b));
            return (popStarts, durations, order);
        }

        // The river runs in from its source — whichever end of it has its land down first — one cell every
        // riverStep, each still waiting for its own land. The source cell keeps the start it got with the rest.
        private static void RunRiver(IslandRiseTiming timing, IReadOnlyList<IslandRiseItem> items, float[] ready, float[] popStarts)
        {
            var flow = new List<int>();
            for (int j = 0; j < items.Count; j++)
                if (items[j].flowIndex >= 0) flow.Add(j);
            if (flow.Count < 2) return;

            flow.Sort((a, b) => items[a].flowIndex.CompareTo(items[b].flowIndex));
            if (ready[flow[flow.Count - 1]] < ready[flow[0]]) flow.Reverse();

            for (int k = 1; k < flow.Count; k++)
            {
                int j = flow[k];
                popStarts[j] = Mathf.Max(popStarts[flow[k - 1]] + timing.riverStep, ready[j] + timing.contentGap);
            }
        }

        // ---- noise ----

        // Uniform in [0, 1) from integer coordinates, a seed and a salt. Integer mixing only, so it is the
        // same on every platform and never touches the engine.
        private static float Hash01(int x, int y, int seed, int salt)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 982451653u + (uint)salt * 2654435761u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h >> 8) * (1f / 16777216f);
            }
        }

        // Smooth value noise in [0, 1]: hashed corners of the unit grid, blended with smoothstep.
        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float tx = x - x0, ty = y - y0;
            float u = tx * tx * (3f - 2f * tx), v = ty * ty * (3f - 2f * ty);

            float a = Hash01(x0, y0, seed, SaltNoise), b = Hash01(x0 + 1, y0, seed, SaltNoise);
            float c = Hash01(x0, y0 + 1, seed, SaltNoise), d = Hash01(x0 + 1, y0 + 1, seed, SaltNoise);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
