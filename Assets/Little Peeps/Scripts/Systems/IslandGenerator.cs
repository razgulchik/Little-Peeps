using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Source of candidate masses for the generator. The default is IslandShape.MakeMass; tests script
    // it to drive the selection logic with known shapes.
    public delegate HashSet<Vector2Int> MassSource(IslandRng rng, int area, int width);

    // Pure C# — decides which cells exist (the island SHAPE), section by section. The grid just stores
    // whatever cells IslandSystem commits from here; nothing in this class knows about tiles, terrain
    // or structures.
    //
    // Growth is proposed, populated, then committed, because the player picks the zone: GenerateStart /
    // Propose(n) sample candidates that are each valid on their own against the current island,
    // Populate fills one with a biome's content (before or after the choice — it never touches the
    // shape rng), and Commit makes a candidate a section. Every sampled cell is drawn from the seeded
    // rng, so the same seed plus the same sequence of choices rebuilds the same island.
    //
    // Selection is by SHAPE FAMILY (translations and rotations grouped): each distinct family sampled
    // gets one entry, however often it was proposed, and a repeated proposal can only improve that
    // family's placement, never its odds. Ported from Prototypes/IslandShapeLab/island_shapes.py
    // (class Island); see the README there for the rules and their reasoning.
    public sealed class IslandGenerator
    {
        // How many distinct families to sample before choosing: the pool is uniform WITHIN itself, so
        // these bound the variety on offer, not the fairness.
        private const int StartFamilies = 24;
        private const int ExpansionFamilies = 10;

        private readonly IslandRules rules;
        private readonly IslandRng rng;
        private readonly MassSource massSource;
        private readonly List<IslandSection> sections = new();
        private readonly HashSet<Vector2Int> land = new();

        public IslandRules Rules => rules;
        public int Seed { get; }
        public IReadOnlyList<IslandSection> Sections => sections;

        // Union of every section; the island as it stands.
        public IReadOnlyCollection<Vector2Int> Land => land;

        // Diagnostics of the last GenerateStart / Propose / Commit, for the log and the tests.
        public int LastAttempts { get; private set; }
        public int LastFill { get; private set; }

        public IslandGenerator(IslandRules rules, int seed) : this(rules, seed, IslandShape.MakeMass) { }

        public IslandGenerator(IslandRules rules, int seed, MassSource massSource)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            rules.Validate();
            this.rules = rules;
            this.massSource = massSource ?? throw new ArgumentNullException(nameof(massSource));
            Seed = seed;
            rng = new IslandRng(seed);
        }

        // The starting island, as a candidate to populate and commit. Samples compact masses until
        // StartFamilies distinct valid families are in hand (or attempts run out), picks a family
        // uniformly, then one of its orientations.
        public IslandCandidate GenerateStart()
        {
            if (sections.Count > 0) throw new InvalidOperationException("The island already has a start.");
            rules.AreaLimits(rules.startArea, out int lower, out int upper);

            var families = new SortedSet<SilhouetteKey>();
            int attempt = 0;
            for (; attempt < rules.attempts; attempt++)
            {
                var initial = massSource(rng, rules.startArea, rules.minWidth);
                if (initial.Count >= lower && initial.Count <= upper
                    && !IslandShape.NarrowLand(initial, rules.minWidth)
                    && IslandShape.Connected(initial)
                    && IslandShape.Holes(initial).Count == 0
                    && IslandShape.PocketCells(initial, rules.minBay, rules.depthRatio).Count == 0)
                {
                    families.Add(IslandShape.Silhouette(initial));
                }
                if (families.Count >= StartFamilies) break;
            }
            LastAttempts = Math.Min(attempt + 1, rules.attempts);
            LastFill = 0;

            if (families.Count == 0)
                throw new InvalidOperationException($"IslandGenerator: no valid start in {rules.attempts} attempts with these rules.");

            var chosen = rng.Choice(new List<SilhouetteKey>(families));   // SortedSet enumerates in key order
            return new IslandCandidate(IslandShape.OrientMass(rng, chosen.Cells), 0, 0, 0);
        }

        // Up to `count` candidate zones, each valid alone against the current island, no two from the
        // same shape family. Fewer (down to none) when the rules leave too little room: that is a
        // failure to report, not something to paper over by loosening the rules here.
        public List<IslandCandidate> Propose(int count)
        {
            if (sections.Count == 0) throw new InvalidOperationException("Commit the start before Propose.");
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            rules.AreaLimits(rules.sectionArea, out int lower, out int upper);

            // Sorted traversal: the coast list, and so the seed's reproduction, must not depend on set
            // iteration order.
            var coast = new List<(Vector2Int cell, Vector2Int dir)>();
            foreach (var p in IslandShape.Sorted(land))
                foreach (var d in IslandShape.Dirs)
                    if (!land.Contains(p + d)) coast.Add((p, d));

            var candidates = new SortedDictionary<SilhouetteKey, IslandCandidate>();
            int attempt = 0;
            for (; attempt < rules.attempts; attempt++)
            {
                // Attachment consumes the overlapping cells; vary the mass budget to compensate.
                int massArea = IslandShape.RoundHalfEven(rules.sectionArea * rng.Uniform(1.0, 1.35));
                var mass = massSource(rng, massArea, rules.minWidth);
                var (anchor, dir) = rng.Choice(coast);

                // Push the mass outward from the coast cell by about half its depth: partial overlap.
                var mb = IslandShape.Bounds(mass);
                int depth = dir.x != 0 ? mb.Width : mb.Height;
                var centre = anchor + dir * Math.Max(1, depth / 2 - 1);

                var proposal = new HashSet<Vector2Int>(land);
                foreach (var m in mass) proposal.Add(m + centre);
                int proposalCount = proposal.Count;

                var combined = IslandShape.Repair(proposal, rules, land.Count + upper);
                if (combined == null) continue;

                var added = new HashSet<Vector2Int>(combined);
                added.ExceptWith(land);
                if (added.Count < lower || added.Count > upper) continue;
                if (!IslandShape.Connected(added) || !IslandShape.BroadJoin(land, added, rules.minWidth)) continue;
                if (IslandShape.NarrowLand(combined, rules.minWidth) || IslandShape.NarrowLand(added, 2)) continue;
                // A section needs usable interior, not just a ring or ribbon of new cells.
                if (!IslandShape.HasSquare(added, rules.minWidth)) continue;

                // Prefer compact overall growth, without requiring a rectangular outline.
                var cb = IslandShape.Bounds(combined);
                double aspect = Math.Max(cb.Width, cb.Height) / (double)Math.Min(cb.Width, cb.Height);
                double score = combined.Count / (double)(cb.Width * cb.Height) - 0.10 * aspect;

                var key = IslandShape.Silhouette(added);
                if (!candidates.TryGetValue(key, out var existing) || score > existing.Score)
                    candidates[key] = new IslandCandidate(added, combined.Count - proposalCount, score, sections.Count);
                if (candidates.Count >= ExpansionFamilies) break;
            }
            LastAttempts = Math.Min(attempt + 1, rules.attempts);

            // Uniform among the families found; the values come out in key order, so the shuffle is
            // the only thing that decides.
            var pool = new List<IslandCandidate>(candidates.Values);
            rng.Shuffle(pool);
            if (pool.Count > count) pool.RemoveRange(count, pool.Count - count);
            return pool;
        }

        // Fill a candidate with a biome's content, against the island as it stands. A non-zero
        // `houseSize` marks the starting zone. Null when the biome's rules can't be met on this shape —
        // nothing is committed, and the shape rng is untouched either way (content has its own streams).
        // Safe to call for several candidates and biomes before choosing one.
        public IslandSectionContent Populate(IslandCandidate candidate, IslandBiome biome, Vector2Int houseSize = default)
        {
            CheckCurrent(candidate);
            var previous = new List<IslandSectionContent>(sections.Count);
            foreach (var s in sections) if (s.Content != null) previous.Add(s.Content);
            return IslandContent.Populate(Seed, sections.Count, biome, candidate.cells, previous, houseSize);
        }

        // Make a proposed zone the island's next section, with its content — or bare, when the caller
        // has decided to keep the land without a biome (content pass failed).
        public IslandSection Commit(IslandCandidate candidate, IslandSectionContent content = null)
        {
            CheckCurrent(candidate);
            if (content != null && !content.land.SetEquals(candidate.cells))
                throw new InvalidOperationException("IslandGenerator: the content was populated for a different candidate.");
            LastFill = candidate.RepairFill;

            var section = new IslandSection(sections.Count, candidate.cells, content);
            sections.Add(section);
            land.UnionWith(candidate.cells);
            return section;
        }

        private void CheckCurrent(IslandCandidate candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (candidate.islandVersion != sections.Count)
                throw new InvalidOperationException("IslandGenerator: stale candidate — it was proposed against an older island. Propose again after every Commit.");
        }
    }
}
