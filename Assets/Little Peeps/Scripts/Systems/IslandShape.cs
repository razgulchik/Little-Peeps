using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Geometry of the island generator: what a good land shape is, and the operations that build and
    // repair one. Everything works on sets of signed cell coordinates — the same cells IslandGrid
    // stores — and nothing here touches the grid or the scene. Where a step samples, the rng is passed
    // in, so the whole thing is reproducible from a seed.
    //
    // Ported from Prototypes/IslandShapeLab/island_shapes.py; the README there is the design spec. The
    // checks are deliberately axis-aligned grid approximations, not proofs — cheap and conservative,
    // as the prototype puts it. Sets are small (hundreds of cells) and generation is a cold path, so
    // plain HashSets are used throughout; no dense buffers.
    //
    // Determinism: HashSet iteration order is not stable, so every place that turns a set into a list
    // to SAMPLE from sorts it first (CellOrder) — the prototype's sorted() calls. Pure set queries
    // (connectivity, holes) don't care about order and skip the sort.
    public static class IslandShape
    {
        public static readonly Vector2Int[] Dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        // x first, then y.
        public static readonly IComparer<Vector2Int> CellOrder = Comparer<Vector2Int>.Create(
            (a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        public static List<Vector2Int> Sorted(IEnumerable<Vector2Int> cells)
        {
            var list = new List<Vector2Int>(cells);
            list.Sort(CellOrder);
            return list;
        }

        // Inclusive min/max corner of a non-empty cell set.
        public readonly struct CellBounds
        {
            public readonly Vector2Int Min;
            public readonly Vector2Int Max;

            public CellBounds(Vector2Int min, Vector2Int max) { Min = min; Max = max; }

            public int Width => Max.x - Min.x + 1;
            public int Height => Max.y - Min.y + 1;

            public bool Contains(Vector2Int c) =>
                c.x >= Min.x && c.x <= Max.x && c.y >= Min.y && c.y <= Max.y;
        }

        public static CellBounds Bounds(IReadOnlyCollection<Vector2Int> cells)
        {
            if (cells.Count == 0) throw new ArgumentException("Bounds of an empty cell set.");
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var c in cells)
            {
                if (c.x < x0) x0 = c.x;
                if (c.y < y0) y0 = c.y;
                if (c.x > x1) x1 = c.x;
                if (c.y > y1) y1 = c.y;
            }
            return new CellBounds(new Vector2Int(x0, y0), new Vector2Int(x1, y1));
        }

        // --- Validity checks -----------------------------------------------------------------------

        // The two flood fills below run on a dense mask of the bounding box rather than on the set:
        // they are called for every proposal the generator tries, and hashing every neighbour lookup
        // made them the bulk of an expansion's cost. A box of a few hundred cells is a few hundred bytes.

        // Four-connected. Diagonal contact is not connectivity: a unit can't cross a corner.
        public static bool Connected(HashSet<Vector2Int> cells)
        {
            if (cells.Count == 0) return false;
            var b = Bounds(cells);
            int w = b.Width, h = b.Height;
            var land = new bool[w * h];
            int start = -1;
            foreach (var c in cells)
            {
                int i = (c.x - b.Min.x) + (c.y - b.Min.y) * w;
                land[i] = true;
                if (start < 0) start = i;
            }
            return FloodFill(land, w, h, start, over: true) == cells.Count;
        }

        // Enclosed water: non-land cells inside the bounding box that the surrounding ocean can't
        // reach. Flood-fills the ocean from a corner of the box, which is padded by one so the ocean
        // surrounds the land.
        public static HashSet<Vector2Int> Holes(HashSet<Vector2Int> cells)
        {
            var result = new HashSet<Vector2Int>();
            if (cells.Count == 0) return result;

            var b = Bounds(cells);
            var min = b.Min - Vector2Int.one;
            int w = b.Width + 2, h = b.Height + 2;
            var land = new bool[w * h];
            foreach (var c in cells) land[(c.x - min.x) + (c.y - min.y) * w] = true;

            var ocean = new bool[w * h];
            FloodFill(land, w, h, 0, over: false, seen: ocean);

            for (int i = 0; i < land.Length; i++)
                if (!land[i] && !ocean[i]) result.Add(new Vector2Int(min.x + i % w, min.y + i / w));
            return result;
        }

        // Four-connected fill from `start` across cells whose mask value equals `over`; returns the
        // count reached. `seen` (optional) receives the reached cells.
        private static int FloodFill(bool[] mask, int w, int h, int start, bool over, bool[] seen = null)
        {
            seen ??= new bool[w * h];
            var stack = new int[w * h];
            int top = 0, count = 0;
            seen[start] = true;
            stack[top++] = start;
            while (top > 0)
            {
                int i = stack[--top];
                count++;
                int x = i % w, y = i / w;
                if (x > 0)     Visit(i - 1);
                if (x < w - 1) Visit(i + 1);
                if (y > 0)     Visit(i - w);
                if (y < h - 1) Visit(i + w);
            }
            return count;

            void Visit(int j)
            {
                if (mask[j] == over && !seen[j]) { seen[j] = true; stack[top++] = j; }
            }
        }

        // Maximal runs of consecutive integers, as inclusive (start, end) pairs. Sorts in place.
        public static List<(int start, int end)> Runs(List<int> values)
        {
            var runs = new List<(int, int)>();
            if (values.Count == 0) return runs;
            values.Sort();
            int start = values[0], previous = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                int value = values[i];
                if (value != previous + 1)
                {
                    runs.Add((start, previous));
                    start = value;
                }
                previous = value;
            }
            runs.Add((start, previous));
            return runs;
        }

        // Group cells into lines: axis 0 keys by x (columns, values are y), axis 1 keys by y (rows).
        private static Dictionary<int, List<int>> Lines(IEnumerable<Vector2Int> cells, int axis)
        {
            var lines = new Dictionary<int, List<int>>();
            foreach (var p in cells)
            {
                int line = axis == 0 ? p.x : p.y;
                int value = axis == 0 ? p.y : p.x;
                if (!lines.TryGetValue(line, out var values)) lines[line] = values = new List<int>();
                values.Add(value);
            }
            return lines;
        }

        // Conservative axis-aligned thickness test: true if any row or column contains a run of land
        // shorter than width. Also rejects skinny tip cells.
        public static bool NarrowLand(HashSet<Vector2Int> cells, int width)
        {
            for (int axis = 0; axis < 2; axis++)
                foreach (var values in Lines(cells, axis).Values)
                    foreach (var (start, end) in Runs(values))
                        if (end - start + 1 < width) return true;
            return false;
        }

        // Water cells that violate the bay rule — a grid approximation, not a physics guarantee.
        //
        // A bay slice is water bracketed by land on opposite sides along a row or column. Slices
        // narrower than minBay are pockets outright. Otherwise, if land closes a perpendicular end,
        // the distance to it may be at most depthRatio × the slice width; through-channels with no
        // closed end are subject only to the width rule.
        public static HashSet<Vector2Int> PocketCells(HashSet<Vector2Int> cells, int minBay, double depthRatio)
        {
            var result = new HashSet<Vector2Int>();
            if (cells.Count == 0) return result;
            var box = Bounds(cells);
            var depths = new List<int>(2);

            for (int axis = 0; axis < 2; axis++)
            {
                foreach (var kv in Lines(cells, axis))
                {
                    int line = kv.Key;
                    var spans = Runs(kv.Value);
                    for (int i = 0; i + 1 < spans.Count; i++)
                    {
                        int end = spans[i].end, start = spans[i + 1].start;
                        int width = start - end - 1;
                        for (int value = end + 1; value < start; value++)
                        {
                            var p = axis == 0 ? new Vector2Int(line, value) : new Vector2Int(value, line);
                            if (width < minBay)
                            {
                                result.Add(p);
                                continue;
                            }
                            depths.Clear();
                            for (int direction = -1; direction <= 1; direction += 2)
                            {
                                for (int distance = 1; ; distance++)
                                {
                                    var q = axis == 0
                                        ? new Vector2Int(p.x + direction * distance, p.y)
                                        : new Vector2Int(p.x, p.y + direction * distance);
                                    if (!box.Contains(q)) break;
                                    if (cells.Contains(q)) { depths.Add(distance); break; }
                                }
                            }
                            if (depths.Count > 0 && Min(depths) > width * depthRatio) result.Add(p);
                        }
                    }
                }
            }
            return result;
        }

        private static int Min(List<int> values)
        {
            int min = values[0];
            for (int i = 1; i < values.Count; i++) if (values[i] < min) min = values[i];
            return min;
        }

        // A continuous straight shared boundary of at least width cell edges between old land and the
        // addition — not a count of scattered contacts.
        public static bool BroadJoin(HashSet<Vector2Int> old, HashSet<Vector2Int> added, int width)
        {
            foreach (var d in Dirs)
            {
                var lines = new Dictionary<int, List<int>>();
                foreach (var c in added)
                {
                    if (!old.Contains(c + d)) continue;
                    int line = d.x != 0 ? c.x : c.y;
                    int value = d.x != 0 ? c.y : c.x;
                    if (!lines.TryGetValue(line, out var values)) lines[line] = values = new List<int>();
                    values.Add(value);
                }
                foreach (var values in lines.Values)
                    foreach (var (start, end) in Runs(values))
                        if (end - start + 1 >= width) return true;
            }
            return false;
        }

        // Any size×size block of cells fully inside the set — usable interior, not just a ribbon.
        public static bool HasSquare(HashSet<Vector2Int> cells, int size)
        {
            foreach (var c in cells)
            {
                bool full = true;
                for (int i = 0; i < size && full; i++)
                    for (int j = 0; j < size; j++)
                        if (!cells.Contains(new Vector2Int(c.x + i, c.y + j))) { full = false; break; }
                if (full) return true;
            }
            return false;
        }

        // --- Silhouettes ---------------------------------------------------------------------------

        // Translation-independent form of a cell set: shifted so its min corner is (0,0), in CellOrder.
        public static Vector2Int[] Normalized(IReadOnlyCollection<Vector2Int> cells)
        {
            var b = Bounds(cells);
            var result = new Vector2Int[cells.Count];
            int i = 0;
            foreach (var c in cells) result[i++] = c - b.Min;
            Array.Sort(result, CellOrder);
            return result;
        }

        // The distinct quarter-turn orientations of a shape, in key order. Rotations of one shape are
        // one family: the lottery gives a family one entry however many rotations were proposed.
        public static List<SilhouetteKey> Orientations(IReadOnlyCollection<Vector2Int> cells)
        {
            var result = new List<SilhouetteKey>(4);
            var current = new List<Vector2Int>(cells);
            for (int turn = 0; turn < 4; turn++)
            {
                var key = new SilhouetteKey(Normalized(current));
                if (!result.Contains(key)) result.Add(key);
                for (int i = 0; i < current.Count; i++)
                    current[i] = new Vector2Int(-current[i].y, current[i].x);
            }
            result.Sort();
            return result;
        }

        // Canonical identity of a shape family: its smallest orientation.
        public static SilhouetteKey Silhouette(IReadOnlyCollection<Vector2Int> cells) => Orientations(cells)[0];

        // Pick one of the shape's orientations at random and centre it on the origin.
        public static HashSet<Vector2Int> OrientMass(IslandRng rng, IReadOnlyCollection<Vector2Int> cells)
        {
            var chosen = rng.Choice(Orientations(cells)).Cells;   // normalized: min corner is (0,0)
            var b = Bounds(chosen);
            var shift = new Vector2Int((b.Max.x + 1) / 2, (b.Max.y + 1) / 2);
            var result = new HashSet<Vector2Int>(chosen.Length);
            foreach (var c in chosen) result.Add(c - shift);
            return result;
        }

        // --- Building ------------------------------------------------------------------------------

        // A w×h rectangle with each corner cut back diagonally by cuts[i] cells (order: bottom-left,
        // bottom-right, top-left, top-right), centred on the origin.
        public static HashSet<Vector2Int> Chamfer(int w, int h, int[] cuts)
        {
            var cells = new HashSet<Vector2Int>(w * h);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    int rx = w - 1 - x, ry = h - 1 - y;
                    if (x + y >= cuts[0] && rx + y >= cuts[1] && x + ry >= cuts[2] && rx + ry >= cuts[3])
                        cells.Add(new Vector2Int(x - w / 2, y - h / 2));
                }
            return cells;
        }

        // A compact, asymmetric chamfered rectangle of roughly `area` cells; no shoreline noise. This is
        // the prototype's controlled baseline, not a final organic-shore algorithm.
        public static HashSet<Vector2Int> MakeMass(IslandRng rng, int area, int width)
        {
            // Reserve some area for the cells the corner cuts remove.
            int envelope = area + Math.Max(4, RoundHalfEven(area * 0.12));
            int w = Math.Max(width + 2, RoundHalfEven(Math.Sqrt(envelope) * rng.Uniform(0.85, 1.3)));
            int h = Math.Max(width + 2, RoundHalfEven((double)envelope / w));

            // Each corner may stay square or cut deeper. The margin is NOT divided equally between the
            // corners: the finished land is validated instead.
            int cutLimit = Math.Max(1, Math.Min(w, h) - width);
            var cuts = new int[4];
            bool any = false;
            for (int i = 0; i < 4; i++)
            {
                cuts[i] = rng.Range(0, cutLimit);
                any |= cuts[i] > 0;
            }
            if (!any) cuts[rng.Range(0, 3)] = 1;

            // Orientation is sampled independently of the width, so horizontal forms get no proposal
            // advantage — a candidate can still fail validation or attachment later.
            return OrientMass(rng, Chamfer(w, h, cuts));
        }

        // Fill holes and pockets until the shape passes, or give up: null when the result would exceed
        // `limit` cells, or when 16 rounds don't converge. Never mutates the input.
        public static HashSet<Vector2Int> Repair(HashSet<Vector2Int> cells, IslandRules rules, int limit)
        {
            var result = new HashSet<Vector2Int>(cells);
            if (result.Count > limit) return null;
            for (int round = 0; round < 16; round++)
            {
                var fill = Holes(result);
                fill.UnionWith(PocketCells(result, rules.minBay, rules.depthRatio));
                if (fill.Count == 0) return result;
                result.UnionWith(fill);
                if (result.Count > limit) return null;
            }
            return null;
        }

        // Python's round(): half to even. Kept so the port's size arithmetic matches the prototype.
        public static int RoundHalfEven(double value) => (int)Math.Round(value, MidpointRounding.ToEven);
    }

    // Identity of a shape up to translation and quarter-turn rotation: the normalized cells of its
    // canonical orientation. Ordered (lexicographically by cell, shorter first) so a set of keys can be
    // listed in one deterministic order before the rng picks from it.
    public sealed class SilhouetteKey : IEquatable<SilhouetteKey>, IComparable<SilhouetteKey>
    {
        public readonly Vector2Int[] Cells;
        private readonly int hash;

        public SilhouetteKey(Vector2Int[] normalizedCells)
        {
            Cells = normalizedCells;
            int h = Cells.Length;
            foreach (var c in Cells) h = unchecked(h * 31 + c.x * 17 + c.y);
            hash = h;
        }

        public HashSet<Vector2Int> ToSet() => new(Cells);

        public int CompareTo(SilhouetteKey other)
        {
            int n = Math.Min(Cells.Length, other.Cells.Length);
            for (int i = 0; i < n; i++)
            {
                int c = IslandShape.CellOrder.Compare(Cells[i], other.Cells[i]);
                if (c != 0) return c;
            }
            return Cells.Length.CompareTo(other.Cells.Length);
        }

        public bool Equals(SilhouetteKey other)
        {
            if (other is null || other.hash != hash || other.Cells.Length != Cells.Length) return false;
            for (int i = 0; i < Cells.Length; i++)
                if (Cells[i] != other.Cells[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as SilhouetteKey);
        public override int GetHashCode() => hash;
    }
}
