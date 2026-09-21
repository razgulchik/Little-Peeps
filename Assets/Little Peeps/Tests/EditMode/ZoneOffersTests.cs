using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static LittlePeeps.Tests.TestBiomes;

namespace LittlePeeps.Tests
{
    // The offers an age puts in front of the player: one per biome, each on a shape its content fits,
    // nothing committed until the pick. Pinned on the pure builder (ZoneOffers.Build) over profiles, so
    // it runs under the offline harness; IslandSystem.ProposeZones only maps the result onto assets.
    public class ZoneOffersTests
    {
        private static readonly Footprint House = Footprint.Rect(2, 2);

        private static IslandGenerator Start(int seed, MassSource massSource = null)
        {
            var g = massSource == null
                ? new IslandGenerator(new IslandRules(), seed)
                : new IslandGenerator(new IslandRules(), seed, massSource);
            var candidate = g.GenerateStart();
            var content = g.Populate(candidate, StartingGrasslands(), House);
            Assert.IsNotNull(content, $"seed {seed}: the start could not be populated");
            g.Commit(candidate, content);
            return g;
        }

        private static List<int> Indices(List<(int biome, IslandCandidate candidate, IslandSectionContent content)> offers)
        {
            var result = new List<int>();
            foreach (var o in offers) result.Add(o.biome);
            return result;
        }

        // The contract the cards rely on: every biome gets a card, the content on it is that biome's and
        // it is for exactly the cells the card's preview shows.
        [Test]
        public void EveryBiomeIsOffered_WithItsOwnContentOnItsOwnCells()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var g = Start(seed);
                var biomes = Cycle();

                var offers = ZoneOffers.Build(g, biomes);

                Assert.That(Indices(offers), Is.EqualTo(new[] { 0, 1, 2 }), $"seed {seed}");
                var island = new HashSet<Vector2Int>(g.Land);
                foreach (var (biome, candidate, content) in offers)
                {
                    Assert.That(content.biome, Is.SameAs(biomes[biome]));
                    Assert.That(content.land.SetEquals(candidate.Cells), $"seed {seed}: content populated for other cells");
                    foreach (var cell in candidate.Cells)
                        Assert.That(island.Contains(cell), Is.False, $"seed {seed}: an offer overlaps the island");
                }
            }
        }

        // Three biomes, three places: the point of a choice is that the cards differ. Each biome starts
        // on its own shape, so with three shapes on offer no two cards share one.
        [Test]
        public void BiomesLandOnDifferentShapes()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var g = Start(seed);

                var offers = ZoneOffers.Build(g, Cycle());

                var shapes = new HashSet<IslandCandidate>();
                foreach (var o in offers) shapes.Add(o.candidate);
                Assert.That(shapes.Count, Is.EqualTo(offers.Count), $"seed {seed}: two biomes were offered on one shape");
            }
        }

        // Building offers is a question, not a change: the island is as it was, and whichever offer the
        // player takes is still valid to commit.
        [Test]
        public void OffersLeaveTheIslandUntouched_AndAnyOfThemCommits()
        {
            var g = Start(7);
            int landBefore = g.Land.Count;

            var offers = ZoneOffers.Build(g, Cycle());

            Assert.That(g.Sections.Count, Is.EqualTo(1));
            Assert.That(g.Land.Count, Is.EqualTo(landBefore));

            var (_, candidate, content) = offers[offers.Count - 1];
            var section = g.Commit(candidate, content);
            Assert.That(section.Cells, Is.EquivalentTo(candidate.Cells));
            Assert.That(section.Content, Is.SameAs(content));
        }

        // A biome whose rules cannot be met on any of this age's shapes has no card; the others are
        // unaffected and keep their indices, so the caller can still name the missing one.
        [Test]
        public void ABiomeThatFitsNoShapeGetsNoOffer()
        {
            var g = Start(3);
            var impossible = Grasslands();
            impossible.id = "impossible";
            impossible.objects.Add(Rule(chance: 1f, min: 100, max: 100));   // a guarantee no zone can keep

            var offers = ZoneOffers.Build(g, new[] { Grasslands(), impossible, Forest() });

            Assert.That(Indices(offers), Is.EqualTo(new[] { 0, 2 }));
        }

        [Test]
        public void ANullBiomeSlotIsSkipped()
        {
            var g = Start(3);

            var offers = ZoneOffers.Build(g, new[] { Grasslands(), null, Forest() });

            Assert.That(Indices(offers), Is.EqualTo(new[] { 0, 2 }));
        }

        [Test]
        public void NoBiomesMeansNoOffers()
        {
            var g = Start(3);

            Assert.That(ZoneOffers.Build(g, new IslandBiome[0]), Is.Empty);
        }

        // When the generator finds no valid shape there is nothing to offer — and that is all this layer
        // says about it. Reporting and what the age does then belong to IslandSystem and the state.
        [Test]
        public void NoShapesMeansNoOffers()
        {
            // Real masses for the start, then a single cell for every expansion attempt: too small to be
            // a section, so Propose comes back empty.
            bool started = false;
            var g = Start(3, (rng, area, width) => started
                ? new HashSet<Vector2Int> { Vector2Int.zero }
                : IslandShape.MakeMass(rng, area, width));
            started = true;

            var offers = ZoneOffers.Build(g, Cycle());

            Assert.That(offers, Is.Empty);
            Assert.That(g.Sections.Count, Is.EqualTo(1));
        }

        // The card's counts come from the features' defs; a rule with none assigned places nothing and
        // is not counted either. Profiles here have no defs at all, so the list is empty rather than a
        // row of nulls.
        [Test]
        public void CountsLeaveOutFeaturesWithoutADef()
        {
            var g = Start(5);
            var (_, candidate, content) = ZoneOffers.Build(g, Cycle())[0];

            var offer = new ZoneOffer(null, candidate, content);

            Assert.That(content.objects.Count, Is.GreaterThan(0), "the zone should hold something to count");
            Assert.That(offer.Counts, Is.Empty);
        }
    }
}
