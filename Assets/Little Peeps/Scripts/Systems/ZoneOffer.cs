using System.Collections.Generic;

namespace LittlePeeps
{
    // One zone the player may grow the island into this age: a biome, a shape valid against the island
    // as it stands, and that shape ALREADY filled with the biome's content. Populating before the pick
    // is what lets the card promise exact numbers — the choice is between resources as much as between
    // places — and what makes the pick commit exactly what was shown rather than rolling again.
    //
    // Built by IslandSystem.ProposeZones, shown by ZoneSelectionUI, and handed back to
    // IslandSystem.CommitZone by the pick. The run never sees one: the offers of an age are dropped
    // with the state that made them, and only the committed section is remembered.
    public sealed class ZoneOffer
    {
        public BiomeDef Biome { get; }
        public IslandCandidate Candidate { get; }
        public IslandSectionContent Content { get; }

        // What the zone holds, per structure def, in the generator's placement order (mountains, then
        // the river, then the scattered objects). This is the card's text. A feature whose def is not
        // assigned is left out — it places nothing either (IslandSystem.PlaceContent reports it).
        public IReadOnlyList<(StructureDef def, int count)> Counts { get; }

        public ZoneOffer(BiomeDef biome, IslandCandidate candidate, IslandSectionContent content)
        {
            Biome = biome;
            Candidate = candidate;
            Content = content;
            Counts = CountFeatures(content);
        }

        private static List<(StructureDef, int)> CountFeatures(IslandSectionContent content)
        {
            var order = new List<StructureDef>();
            var counts = new Dictionary<StructureDef, int>();
            foreach (var (_, def) in content.Features(null))
            {
                if (def == null) continue;
                if (!counts.ContainsKey(def)) { order.Add(def); counts[def] = 0; }
                counts[def]++;
            }

            var result = new List<(StructureDef, int)>(order.Count);
            foreach (var def in order) result.Add((def, counts[def]));
            return result;
        }
    }

    // Pairs each biome with a proposed shape its content fits on. Pure: it works on profiles rather than
    // assets, so the offline tests can drive it end to end; IslandSystem maps the indices back to its
    // BiomeDefs and wraps the result in ZoneOffers.
    public static class ZoneOffers
    {
        // One entry per biome that fits somewhere, in biome order; `biome` is the index into `biomes`.
        // Each biome starts on its own shape (biome i on candidate i) so three biomes normally mean three
        // different places to grow; when a biome's content does not fit its shape, any other will do
        // rather than no card at all. A null biome slot is skipped. Empty when the generator found no
        // valid shape — that is the caller's to report; nothing here decides what the island does then.
        //
        // Nothing is committed: the generator's island is exactly as it was, and every candidate stays
        // valid until one of them is committed.
        public static List<(int biome, IslandCandidate candidate, IslandSectionContent content)> Build(
            IslandGenerator generator, IReadOnlyList<IslandBiome> biomes)
        {
            var offers = new List<(int, IslandCandidate, IslandSectionContent)>();
            if (biomes.Count == 0) return offers;

            var candidates = generator.Propose(biomes.Count);
            if (candidates.Count == 0) return offers;

            for (int i = 0; i < biomes.Count; i++)
            {
                var biome = biomes[i];
                if (biome == null) continue;

                for (int k = 0; k < candidates.Count; k++)
                {
                    var candidate = candidates[(i + k) % candidates.Count];
                    var content = generator.Populate(candidate, biome);
                    if (content == null) continue;

                    offers.Add((i, candidate, content));
                    break;
                }
            }

            return offers;
        }
    }
}
