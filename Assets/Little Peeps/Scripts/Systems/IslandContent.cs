using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Fills a zone with a biome's content: reservations first, then mountains, a river, and scattered
    // objects, each placed only where the island stays reachable. Ported from island_resources.py
    // (Daniil's prototype); the README's "Biomes and resources" section is the spec.
    //
    // Two rules shape everything here:
    //  - Earlier zones never change. A new zone only adds to its own cells; what it reserves, it reserves
    //    for zones that don't exist yet (gates along the coast, bank cells for bridges).
    //  - Access is checked with the PLANNED bridges counted as walkable: a river may cut land off as long
    //    as a marked crossing would restore it. That is the prototype's rule and a design decision of the
    //    game (2026-09-18); bridges themselves are a later, unrelated feature.
    //
    // Content draws from its own rng streams (IslandRng.Derive), never from the shape's, so retrying a
    // zone's content leaves the island's future shapes untouched.
    public static class IslandContent
    {
        private const int Attempts = 32;
        private const int ClearingSize = 3;   // the free block every zone keeps: the shape rule's minimum width
        private const int MinRiver = 4;
        private const int MinMountain = 4;

        // --- Access ----------------------------------------------------------------------------------

        public static HashSet<Vector2Int> CombinedBlocked(IReadOnlyList<IslandSectionContent> contents)
        {
            var result = new HashSet<Vector2Int>();
            foreach (var c in contents) result.UnionWith(c.Blocked());
            return result;
        }

        public static HashSet<Vector2Int> PlannedBridges(IReadOnlyList<IslandSectionContent> contents)
        {
            var result = new HashSet<Vector2Int>();
            foreach (var c in contents) result.UnionWith(c.bridges.Keys);
            return result;
        }

        // Potential connectivity after building the marked bridges, not current access: all walkable
        // land is one piece, and every object can be approached from a walkable cell.
        public static bool AccessOk(HashSet<Vector2Int> land, IReadOnlyList<IslandSectionContent> contents)
        {
            var walkable = new HashSet<Vector2Int>(land);
            walkable.ExceptWith(CombinedBlocked(contents));
            walkable.UnionWith(PlannedBridges(contents));
            if (!IslandShape.Connected(walkable)) return false;

            foreach (var c in contents)
                foreach (var p in c.objects.Keys)
                    if (!HasNeighbourIn(p, walkable)) return false;
            return true;
        }

        private static bool HasNeighbourIn(Vector2Int p, HashSet<Vector2Int> set)
        {
            foreach (var d in IslandShape.Dirs)
                if (set.Contains(p + d)) return true;
            return false;
        }

        private static List<IslandSectionContent> With(IReadOnlyList<IslandSectionContent> previous, IslandSectionContent content)
        {
            var list = new List<IslandSectionContent>(previous.Count + 1);
            list.AddRange(previous);
            list.Add(content);
            return list;
        }

        // --- Geometry helpers -----------------------------------------------------------------------

        // Cells nearest the set's centroid first; ties in CellOrder, so the result is deterministic.
        public static List<Vector2Int> CentreOrder(IReadOnlyCollection<Vector2Int> cells)
        {
            double cx = 0, cy = 0;
            foreach (var c in cells) { cx += c.x; cy += c.y; }
            cx /= cells.Count;
            cy /= cells.Count;

            var list = new List<Vector2Int>(cells);
            list.Sort((a, b) =>
            {
                double da = (a.x - cx) * (a.x - cx) + (a.y - cy) * (a.y - cy);
                double db = (b.x - cx) * (b.x - cx) + (b.y - cy) * (b.y - cy);
                return da != db ? da.CompareTo(db) : IslandShape.CellOrder.Compare(a, b);
            });
            return list;
        }

        // Every size-shaped block whose cells all lie in the set, anchored at its min corner, nearest the
        // centre first.
        public static List<HashSet<Vector2Int>> Rects(HashSet<Vector2Int> cells, Vector2Int size)
        {
            var result = new List<HashSet<Vector2Int>>();
            foreach (var origin in CentreOrder(cells))
            {
                var block = new HashSet<Vector2Int>();
                bool full = true;
                for (int i = 0; i < size.x && full; i++)
                    for (int j = 0; j < size.y; j++)
                    {
                        var p = new Vector2Int(origin.x + i, origin.y + j);
                        if (!cells.Contains(p)) { full = false; break; }
                        block.Add(p);
                    }
                if (full) result.Add(block);
            }
            return result;
        }

        public static List<HashSet<Vector2Int>> Squares(HashSet<Vector2Int> cells, int size) =>
            Rects(cells, new Vector2Int(size, size));

        // --- Reservations ----------------------------------------------------------------------------

        // Any future join of three or more edges must contain at least one accessible land cell: along
        // every stretch of coast, of each pair of neighbouring coast cells at least one stays free. Gaps
        // are reserved now rather than clearing resources retroactively when the island grows.
        public static void ReserveFutureGates(IslandSectionContent content, IReadOnlyList<IslandSectionContent> previous, HashSet<Vector2Int> land)
        {
            var oldWalk = new HashSet<Vector2Int>(land);
            oldWalk.ExceptWith(content.land);
            oldWalk.ExceptWith(CombinedBlocked(previous));
            oldWalk.UnionWith(PlannedBridges(previous));

            var sortedLand = IslandShape.Sorted(land);
            foreach (var d in IslandShape.Dirs)
            {
                var lines = new Dictionary<int, List<int>>();
                foreach (var p in sortedLand)
                {
                    if (land.Contains(p + d)) continue;
                    int line = d.x != 0 ? p.x : p.y;
                    int value = d.x != 0 ? p.y : p.x;
                    if (!lines.TryGetValue(line, out var values)) lines[line] = values = new List<int>();
                    values.Add(value);
                }
                foreach (var kv in lines)
                {
                    int line = kv.Key;
                    foreach (var (start, end) in IslandShape.Runs(kv.Value))
                        for (int value = start; value < end; value++)
                        {
                            var a = d.x != 0 ? new Vector2Int(line, value) : new Vector2Int(value, line);
                            var b = d.x != 0 ? new Vector2Int(line, value + 1) : new Vector2Int(value + 1, line);
                            if (IsGate(a, content, oldWalk) || IsGate(b, content, oldWalk)) continue;
                            if (content.land.Contains(a) && !content.house.Contains(a)) content.reserved.Add(a);
                            else if (content.land.Contains(b) && !content.house.Contains(b)) content.reserved.Add(b);
                        }
                }
            }
        }

        private static bool IsGate(Vector2Int p, IslandSectionContent content, HashSet<Vector2Int> oldWalk) =>
            oldWalk.Contains(p) || (content.reserved.Contains(p) && !content.house.Contains(p));

        // --- Features --------------------------------------------------------------------------------

        // Straight interior river cells whose two banks are clear land: where a bridge could go.
        public static Dictionary<Vector2Int, (Vector2Int a, Vector2Int b)> RiverBanks(IReadOnlyList<Vector2Int> path, HashSet<Vector2Int> land, HashSet<Vector2Int> blocked)
        {
            var result = new Dictionary<Vector2Int, (Vector2Int, Vector2Int)>();
            for (int i = 1; i + 1 < path.Count; i++)
            {
                var p = path[i];
                var before = path[i - 1];
                var after = path[i + 1];
                Vector2Int a, b;
                if (before.x == p.x && after.x == p.x)      { a = new Vector2Int(p.x - 1, p.y); b = new Vector2Int(p.x + 1, p.y); }
                else if (before.y == p.y && after.y == p.y) { a = new Vector2Int(p.x, p.y - 1); b = new Vector2Int(p.x, p.y + 1); }
                else continue;
                if (land.Contains(a) && land.Contains(b) && !blocked.Contains(a) && !blocked.Contains(b))
                    result[p] = (a, b);
            }
            return result;
        }

        // One compact patch of 2×2 blocks, with two approach cells reserved for a future mine. Skipped
        // when the cap can't hold a patch.
        //
        // The prototype grew the patch cell by cell and threw away any result with a one-cell-wide part.
        // On our zones that rejected almost every try: after the clearing, the join and the coastal gates
        // are reserved, the free land is small and fragmented, and a cell-wise walk through it is thin
        // more often than not — only one desert zone in five got mountains. Growing by whole 2×2 blocks
        // makes every row and column run at least two wide BY CONSTRUCTION, so nothing has to be
        // rejected for thinness; the patch just stops when no block fits.
        public static void AddMountains(IslandRng rng, IslandSectionContent content, IReadOnlyList<IslandSectionContent> previous, HashSet<Vector2Int> land, int cap)
        {
            if (cap < MinMountain) return;
            var available = new HashSet<Vector2Int>(content.land);
            available.ExceptWith(content.reserved);

            var blocks = Squares(available, 2);           // every 2×2 that fits the free land, centre first
            var seeds = new List<HashSet<Vector2Int>>(blocks);
            rng.Shuffle(seeds);
            int tries = Math.Min(seeds.Count, 48);
            var ranked = new List<HashSet<Vector2Int>>();
            var weights = new List<double>();
            for (int s = 0; s < tries; s++)
            {
                var patch = new HashSet<Vector2Int>(seeds[s]);
                int target = rng.Range(Math.Max(MinMountain, cap * 2 / 3), cap);
                while (patch.Count < target)
                {
                    // Blocks that would extend the patch and stay attached to it: overlapping it, or
                    // touching it side-on. Weighted towards the ones that share the most with it, for
                    // compact clusters rather than chains.
                    ranked.Clear();
                    weights.Clear();
                    foreach (var block in blocks)
                    {
                        int touching = 0, added = 0;
                        foreach (var p in block)
                        {
                            if (patch.Contains(p)) { touching++; continue; }
                            added++;
                            foreach (var d in IslandShape.Dirs) if (patch.Contains(p + d)) { touching++; break; }
                        }
                        if (added == 0 || touching == 0 || patch.Count + added > cap) continue;   // must grow, stay attached, stay in budget
                        ranked.Add(block);
                        weights.Add(1 + 5 * touching);
                    }
                    if (ranked.Count == 0) break;
                    patch.UnionWith(rng.WeightedChoice(ranked, weights));
                }

                content.mountains.UnionWith(patch);
                if (!AccessOk(land, With(previous, content)))
                {
                    content.mountains.Clear();
                    continue;
                }

                var blocked = content.Blocked();
                var approaches = new HashSet<Vector2Int>();
                foreach (var p in patch)
                    foreach (var d in IslandShape.Dirs)
                    {
                        var q = p + d;
                        if (content.land.Contains(q) && !blocked.Contains(q)) approaches.Add(q);
                    }
                if (approaches.Count == 0)
                {
                    content.mountains.Clear();
                    continue;
                }
                var ordered = CentreOrder(approaches);
                for (int i = 0; i < Math.Min(2, ordered.Count); i++) content.mineAccess.Add(ordered[i]);
                content.reserved.UnionWith(content.mineAccess);
                return;
            }
        }

        // A one-cell-wide self-avoiding path with a bias to run straight, kept clear of older rivers,
        // with underground endpoints (both ends inside the land — the sea never has to be reachable, so
        // a later zone can't bury an outlet). Marks the bridge candidates that keep access whole, and
        // a bank cell for a future mill.
        public static void AddRiver(IslandRng rng, IslandSectionContent content, IReadOnlyList<IslandSectionContent> previous, HashSet<Vector2Int> land, int cap)
        {
            if (cap < MinRiver || rng.NextDouble() > content.biome.riverChance) return;

            var oldRivers = new HashSet<Vector2Int>();
            foreach (var c in previous) oldRivers.UnionWith(c.river);

            var available = new HashSet<Vector2Int>();
            foreach (var p in content.land)
            {
                if (content.reserved.Contains(p) || content.mountains.Contains(p)) continue;
                if (HasNeighbourIn(p, oldRivers)) continue;
                available.Add(p);
            }
            if (available.Count < MinRiver) return;

            var sortedAvailable = IslandShape.Sorted(available);
            var choices = new List<Vector2Int>(4);
            var weights = new List<double>(4);
            for (int attempt = 0; attempt < 100; attempt++)
            {
                int target = rng.Range(MinRiver, cap);
                var path = new List<Vector2Int> { rng.Choice(sortedAvailable) };
                var onPath = new HashSet<Vector2Int> { path[0] };
                while (path.Count < target)
                {
                    var last = path[path.Count - 1];
                    choices.Clear();
                    weights.Clear();
                    foreach (var d in IslandShape.Dirs)
                    {
                        var p = last + d;
                        if (!available.Contains(p) || onPath.Contains(p)) continue;
                        int touching = 0;
                        foreach (var e in IslandShape.Dirs) if (onPath.Contains(p + e)) touching++;
                        if (touching != 1) continue;   // no nonconsecutive contact with itself
                        bool straight = path.Count > 1 && d == last - path[path.Count - 2];
                        choices.Add(p);
                        weights.Add(straight ? 3 : 1);
                    }
                    if (choices.Count == 0) break;
                    var next = rng.WeightedChoice(choices, weights);
                    path.Add(next);
                    onPath.Add(next);
                }
                if (path.Count < MinRiver) continue;

                content.river.AddRange(path);
                var blocked = CombinedBlocked(With(previous, content));
                // Only bank sites inside the new zone: expansion must not alter old content.
                var bankOptions = new Dictionary<Vector2Int, (Vector2Int a, Vector2Int b)>();
                foreach (var kv in RiverBanks(path, land, blocked))
                    if (content.land.Contains(kv.Value.a) && content.land.Contains(kv.Value.b)) bankOptions[kv.Key] = kv.Value;
                if (bankOptions.Count == 0)
                {
                    content.river.Clear();
                    continue;
                }

                foreach (var kv in bankOptions) content.bridges[kv.Key] = kv.Value;
                if (!AccessOk(land, With(previous, content)))
                {
                    content.river.Clear();
                    content.bridges.Clear();
                    continue;
                }

                // Keep one optional crossing, or the minimum sampled set needed for connectivity.
                foreach (var p in IslandShape.Sorted(bankOptions.Keys))
                {
                    if (content.bridges.Count == 1) break;
                    var banks = content.bridges[p];
                    content.bridges.Remove(p);
                    if (!AccessOk(land, With(previous, content))) content.bridges[p] = banks;
                }

                var millOptions = new HashSet<Vector2Int>();
                foreach (var kv in bankOptions)
                    if (!content.bridges.ContainsKey(kv.Key)) { millOptions.Add(kv.Value.a); millOptions.Add(kv.Value.b); }
                if (millOptions.Count == 0)
                {
                    content.river.Clear();
                    content.bridges.Clear();
                    continue;
                }
                content.millAccess.Add(rng.Choice(IslandShape.Sorted(millOptions)));
                content.reserved.UnionWith(content.millAccess);
                foreach (var banks in content.bridges.Values) { content.reserved.Add(banks.a); content.reserved.Add(banks.b); }
                return;
            }
        }

        // Put one object of `rule` on a free cell that keeps access whole: near its own kind when
        // clustered, away from it otherwise. False when no cell works.
        public static bool PlaceObject(IslandRng rng, IslandObjectRule rule, IslandSectionContent content, IReadOnlyList<IslandSectionContent> previous, HashSet<Vector2Int> land)
        {
            var free = new HashSet<Vector2Int>(content.land);
            free.ExceptWith(content.reserved);
            free.ExceptWith(content.Blocked());
            var available = IslandShape.Sorted(free);
            rng.Shuffle(available);

            var peers = new List<Vector2Int>();
            foreach (var kv in content.objects) if (kv.Value == rule) peers.Add(kv.Key);
            if (peers.Count > 0)
            {
                // Stable sort by distance to the nearest peer, so equal distances keep the shuffled order.
                var keyed = new List<(int dist, int order, Vector2Int cell)>(available.Count);
                for (int i = 0; i < available.Count; i++)
                {
                    int best = int.MaxValue;
                    foreach (var q in peers) best = Math.Min(best, Math.Abs(available[i].x - q.x) + Math.Abs(available[i].y - q.y));
                    keyed.Add((rule.clustered ? best : -best, i, available[i]));
                }
                keyed.Sort((a, b) => a.dist != b.dist ? a.dist.CompareTo(b.dist) : a.order.CompareTo(b.order));
                for (int i = 0; i < keyed.Count; i++) available[i] = keyed[i].cell;
            }

            foreach (var p in available)
            {
                content.objects[p] = rule;
                if (AccessOk(land, With(previous, content))) return true;
                content.objects.Remove(p);
            }
            return false;
        }

        // The kinds to place in a zone with an object budget of `cap`: what each fired rule asks for,
        // then a weighted fill up to a target inside the budget. `guaranteed` collects each fired rule's
        // minimum, for the check after placement.
        public static List<IslandObjectRule> Recipe(IslandRng rng, IslandBiome biome, int cap, Dictionary<IslandObjectRule, int> guaranteed)
        {
            var kinds = new List<IslandObjectRule>();
            if (cap <= 0) return kinds;
            int target = rng.Range(Math.Max(1, cap * 2 / 3), cap);

            foreach (var rule in biome.objects)
            {
                if (rule.maxCount <= 0 || rng.NextDouble() >= rule.chance) continue;
                int count = rng.Range(rule.minCount, rule.maxCount);
                for (int i = 0; i < count; i++) kinds.Add(rule);
                guaranteed[rule] = rule.minCount;
            }

            var fillers = new List<IslandObjectRule>();
            var weights = new List<double>();
            foreach (var rule in biome.objects)
                if (rule.weight > 0f) { fillers.Add(rule); weights.Add(rule.weight); }
            if (fillers.Count > 0)
                while (kinds.Count < target) kinds.Add(rng.WeightedChoice(fillers, weights));

            // Over-asking is an authoring error; the budget is the rule that wins.
            if (kinds.Count > cap) kinds.RemoveRange(cap, kinds.Count - cap);
            return kinds;
        }

        // --- Populate --------------------------------------------------------------------------------

        // Fill `section` (zone number `index` of the island seeded `seed`) with `biome`'s content, given
        // the content of every earlier zone. A non-zero `houseSize` marks the starting zone: a footprint of
        // that size is reserved in the clearing and the zone is regenerated until every fired rule's
        // minimum is met.
        // Null when no attempt produced a valid zone — the caller decides what to do about it.
        public static IslandSectionContent Populate(int seed, int index, IslandBiome biome, IReadOnlyCollection<Vector2Int> section, IReadOnlyList<IslandSectionContent> previous, Vector2Int houseSize = default)
        {
            if (biome == null) throw new ArgumentNullException(nameof(biome));
            biome.Validate();

            var sectionCells = new HashSet<Vector2Int>(section);
            var land = new HashSet<Vector2Int>(sectionCells);
            foreach (var c in previous) land.UnionWith(c.land);
            var oldLand = new HashSet<Vector2Int>(land);
            oldLand.ExceptWith(sectionCells);

            var clearings = Squares(sectionCells, ClearingSize);
            if (clearings.Count == 0)
            {
                Debug.LogError($"IslandContent: zone {index} has no {ClearingSize}×{ClearingSize} clearing; it cannot be populated without changing its shape.");
                return null;
            }

            var guaranteed = new Dictionary<IslandObjectRule, int>();
            var placed = new Dictionary<IslandObjectRule, int>();
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var rng = IslandRng.Derive(seed, (ulong)index, IslandRng.Hash(biome.id), (ulong)attempt);
                var content = new IslandSectionContent(biome, sectionCells, houseSize);
                content.clearing.UnionWith(clearings[attempt % clearings.Count]);
                content.reserved.UnionWith(content.clearing);

                // Preserve every boundary cell that connects this zone to older land.
                foreach (var p in sectionCells)
                    if (HasNeighbourIn(p, oldLand)) content.reserved.Add(p);

                if (content.Starting)
                {
                    var options = Rects(content.clearing, houseSize);
                    if (options.Count == 0)
                    {
                        Debug.LogError($"IslandContent: a {houseSize.x}×{houseSize.y} house does not fit the {ClearingSize}×{ClearingSize} clearing.");
                        return null;
                    }
                    var footprint = options[attempt % options.Count];
                    content.house.UnionWith(footprint);
                    content.houseOrigin = IslandShape.Bounds(footprint).Min;
                    content.reserved.UnionWith(footprint);
                    // The ring around the house, diagonals included, so its border stays clear too.
                    foreach (var p in footprint)
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                var q = new Vector2Int(p.x + dx, p.y + dy);
                                if (sectionCells.Contains(q)) content.reserved.Add(q);
                            }
                }

                ReserveFutureGates(content, previous, land);
                if (!AccessOk(land, With(previous, content))) continue;

                biome.Budgets(sectionCells.Count, out int mountainCap, out int riverCap, out int objectCap);
                AddMountains(rng, content, previous, land, mountainCap);
                AddRiver(rng, content, previous, land, riverCap);

                guaranteed.Clear();
                placed.Clear();
                foreach (var rule in Recipe(rng, biome, objectCap, guaranteed))
                    if (PlaceObject(rng, rule, content, previous, land))
                        placed[rule] = placed.TryGetValue(rule, out int n) ? n + 1 : 1;

                bool short_ = false;
                foreach (var kv in guaranteed)
                    if (!placed.TryGetValue(kv.Key, out int n) || n < kv.Value) { short_ = true; break; }
                if (short_) continue;

                return content;
            }
            return null;
        }

        // --- Validation (tests and the seed sweep; nothing in play calls this) ----------------------

        public static void Validate(IslandSectionContent c)
        {
            var biome = c.biome;
            biome.Budgets(c.land.Count, out int mountainCap, out int riverCap, out int objectCap);
            Check(c.mountains.Count <= mountainCap && c.river.Count <= riverCap && c.objects.Count <= objectCap, "budget exceeded");
            Check(c.OpenCells * 100 >= c.land.Count * biome.openPercent, "open land below the biome's minimum");

            var natural = c.Natural();
            Check(natural.IsSubsetOf(c.land) && c.reserved.IsSubsetOf(c.land) && c.house.IsSubsetOf(c.reserved), "content outside the zone");
            Check(!natural.Overlaps(c.reserved), "natural feature on a reserved cell");
            Check(natural.Count == c.mountains.Count + c.river.Count + c.objects.Count, "features overlap");
            foreach (var rule in c.objects.Values) Check(biome.objects.Contains(rule), "object of a rule the biome does not have");

            Check(c.mountains.Count == 0 || (biome.mountainPercent > 0 && IslandShape.Connected(c.mountains) && !IslandShape.NarrowLand(c.mountains, 2)),
                  "mountains must be one thick patch in a biome that allows them");

            if (c.river.Count > 0)
            {
                Check(c.river.Count >= MinRiver && new HashSet<Vector2Int>(c.river).Count == c.river.Count, "river too short or self-overlapping");
                var onPath = new HashSet<Vector2Int>(c.river);
                for (int i = 0; i < c.river.Count; i++)
                {
                    var p = c.river[i];
                    int touching = 0;
                    foreach (var d in IslandShape.Dirs) if (onPath.Contains(p + d)) touching++;
                    int expected = (i > 0 ? 1 : 0) + (i + 1 < c.river.Count ? 1 : 0);
                    Check(touching == expected, "river touches itself");
                }
                Check(c.bridges.Count > 0 && c.millAccess.Count > 0, "a river needs a bridge site and a mill bank");
                var banks = RiverBanks(c.river, c.land, c.Blocked());
                foreach (var kv in c.bridges)
                    Check(banks.TryGetValue(kv.Key, out var b) && b == kv.Value, "bridge site is not a clear straight river cell");
            }

            Check(c.mineAccess.IsSubsetOf(c.reserved) && c.millAccess.IsSubsetOf(c.reserved), "approach cells must be reserved");
            foreach (var p in c.mineAccess) Check(HasNeighbourIn(p, c.mountains), "mine approach not beside the mountains");

            if (c.Starting)
            {
                Check(c.house.Count == c.houseSize.x * c.houseSize.y, "house footprint incomplete");
                foreach (var rule in biome.objects)
                {
                    if (rule.chance < 1f || rule.maxCount <= 0) continue;
                    int n = 0;
                    foreach (var r in c.objects.Values) if (r == rule) n++;
                    Check(n >= rule.minCount, "a guaranteed object kind is short");
                }
            }
        }

        // Every zone against its section, and the island as a whole: access with planned bridges, and
        // rivers of different zones never touching.
        public static void ValidateAll(IReadOnlyList<IslandSection> sections)
        {
            var land = new HashSet<Vector2Int>();
            var contents = new List<IslandSectionContent>();
            foreach (var s in sections)
            {
                land.UnionWith(s.Cells);
                if (s.Content == null) continue;
                Check(s.Content.land.SetEquals(s.Cells), $"zone {s.Index}: content land differs from the section");
                Validate(s.Content);
                contents.Add(s.Content);
            }
            Check(AccessOk(land, contents), "island access broken");
            for (int i = 0; i < contents.Count; i++)
                for (int j = 0; j < i; j++)
                {
                    var older = new HashSet<Vector2Int>(contents[j].river);
                    foreach (var p in contents[i].river)
                        Check(!HasNeighbourIn(p, older), "rivers of two zones touch");
                }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("IslandContent: " + message);
        }
    }
}
