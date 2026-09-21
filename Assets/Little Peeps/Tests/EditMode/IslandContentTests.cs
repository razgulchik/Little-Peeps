using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static LittlePeeps.Tests.TestBiomes;

namespace LittlePeeps.Tests
{
    // The content pass, pinned the way the prototype pinned it (Prototypes/IslandShapeLab/
    // test_island_resources.py): budgets, the start's guarantees, content never disturbing the shape
    // rng, old zones surviving growth, rivers and their bridge candidates, and a mixed-biome sweep
    // through every invariant. Biome profiles come from TestBiomes (the prototype's PROFILES table in
    // code), so nothing needs a ScriptableObject and the whole class runs under the offline harness.
    public class IslandContentTests
    {
        private const int HouseSide = 2;
        private static readonly Vector2Int House = new(HouseSide, HouseSide);

        private static IslandRules Rules(int startArea = 36) => new() { startArea = startArea };

        private static HashSet<Vector2Int> Set(IEnumerable<Vector2Int> cells) => new(cells);

        // Start a populated island: shape, then the starting biome with the house.
        private static IslandGenerator Start(int seed, IslandBiome biome, int startArea = 36)
        {
            var g = new IslandGenerator(Rules(startArea), seed);
            var candidate = g.GenerateStart();
            var content = g.Populate(candidate, biome, House);
            Assert.IsNotNull(content, $"seed {seed}: the start could not be populated");
            g.Commit(candidate, content);
            return g;
        }

        // Grow by one zone of `biome`, taking the first proposal its content fits on.
        private static IslandSection Expand(IslandGenerator g, IslandBiome biome)
        {
            foreach (var candidate in g.Propose(3))
            {
                var content = g.Populate(candidate, biome);
                if (content != null) return g.Commit(candidate, content);
            }
            Assert.Fail($"seed {g.Seed}: biome '{biome.id}' fits no proposed zone");
            return null;
        }

        private static List<IslandSectionContent> Contents(IslandGenerator g)
        {
            var list = new List<IslandSectionContent>();
            foreach (var s in g.Sections) if (s.Content != null) list.Add(s.Content);
            return list;
        }

        private static HashSet<Vector2Int> Walkable(IslandGenerator g, bool withBridges)
        {
            var contents = Contents(g);
            var ground = Set(g.Land);
            ground.ExceptWith(IslandContent.CombinedBlocked(contents));
            if (withBridges) ground.UnionWith(IslandContent.PlannedBridges(contents));
            return ground;
        }

        private static int Count(IslandSectionContent c, IslandObjectRule rule)
        {
            int n = 0;
            foreach (var r in c.objects.Values) if (r == rule) n++;
            return n;
        }

        // --- budgets and recipe ---------------------------------------------------------------------

        [Test]
        public void Budgets_RoundDownWithoutTransferringCapacity()
        {
            StartingGrasslands().Budgets(36, out int m, out int r, out int o); Assert.AreEqual((0, 0, 7), (m, r, o));
            Grasslands().Budgets(48, out m, out r, out o);                     Assert.AreEqual((0, 16, 7), (m, r, o));
            StoneDesert().Budgets(48, out m, out r, out o);                    Assert.AreEqual((16, 2, 7), (m, r, o));
            Forest().Budgets(48, out m, out r, out o);                         Assert.AreEqual((0, 9, 16), (m, r, o));
        }

        [Test]
        public void Recipe_FiresRulesThenFillsByWeight()
        {
            var guaranteed = new Dictionary<IslandObjectRule, int>();
            var berry = Rule(chance: 1f, min: 2, max: 2, weight: 0f);
            var tree = Rule();
            var biome = new IslandBiome { id = "r", objectPercent = 20, objects = { berry, tree } };

            Assert.IsEmpty(IslandContent.Recipe(new IslandRng(1), biome, 0, guaranteed), "no budget, no objects");
            Assert.IsEmpty(guaranteed);

            for (int seed = 0; seed < 20; seed++)
            {
                guaranteed.Clear();
                var kinds = IslandContent.Recipe(new IslandRng(seed), biome, 6, guaranteed);
                int berries = 0, trees = 0;
                foreach (var k in kinds) { if (k == berry) berries++; else if (k == tree) trees++; }
                Assert.AreEqual(2, berries, "a fired rule asks for exactly its min..max");
                Assert.AreEqual(2, guaranteed[berry]);
                Assert.That(kinds.Count, Is.InRange(4, 6), "fill reaches a target inside the budget");
                Assert.AreEqual(kinds.Count - 2, trees, "the fill draws only from weighted rules");
            }
        }

        // --- the start --------------------------------------------------------------------------------

        [Test]
        public void Start_GuaranteesFoodWoodAndUnbridgedHouseAccess()
        {
            foreach (int size in new[] { 25, 36, 64 })
                for (int seed = 0; seed < 10; seed++)
                {
                    string at = $"size {size}, seed {seed}";
                    var biome = StartingGrasslands();
                    var g = Start(seed, biome, size);
                    Assert.DoesNotThrow(() => IslandContent.ValidateAll(g.Sections), at);

                    var c = g.Sections[0].Content;
                    Assert.IsTrue(c.Starting, at);
                    Assert.That(Count(c, biome.objects[0]), Is.GreaterThanOrEqualTo(2), $"{at}: berries");
                    Assert.That(Count(c, biome.objects[1]), Is.GreaterThanOrEqualTo(2), $"{at}: trees");
                    Assert.AreEqual(c.objects.Count, Count(c, biome.objects[0]) + Count(c, biome.objects[1]), $"{at}: only berries and trees");
                    Assert.AreEqual(HouseSide * HouseSide, c.house.Count, $"{at}: house footprint");
                    Assert.IsTrue(c.house.Contains(c.houseOrigin), $"{at}: house origin inside its footprint");
                    Assert.IsTrue(IslandShape.Connected(Walkable(g, withBridges: false)), $"{at}: the start needs no bridges");
                    Assert.IsEmpty(c.river, at);
                    Assert.IsEmpty(c.mountains, at);
                }
        }

        // --- growth -----------------------------------------------------------------------------------

        [Test]
        public void Content_DoesNotChangeShapeRng_AndOldContentSurvivesExpansion()
        {
            for (int seed = 0; seed < 3; seed++)
            {
                var shape = new IslandGenerator(Rules(), seed);
                shape.Commit(shape.GenerateStart());
                var world = Start(seed, StartingGrasslands());

                foreach (var biome in Cycle())
                {
                    var prior = new List<IslandSectionContent>(Contents(world));
                    var priorObjects = new List<int>();
                    foreach (var c in prior) priorObjects.Add(c.objects.Count);

                    // Shape-only growth takes the first proposal; populated growth must land on the same
                    // shapes for the same seed, whatever content did in between.
                    shape.Commit(shape.Propose(3)[0]);
                    Expand(world, biome);

                    Assert.AreEqual(shape.Sections.Count, world.Sections.Count);
                    for (int i = 0; i < shape.Sections.Count; i++)
                        Assert.IsTrue(Set(shape.Sections[i].Cells).SetEquals(world.Sections[i].Cells), $"seed {seed}: section {i} differs");

                    var now = Contents(world);
                    for (int i = 0; i < prior.Count; i++)
                    {
                        Assert.AreSame(prior[i], now[i], $"seed {seed}: zone {i} content replaced");
                        Assert.AreEqual(priorObjects[i], prior[i].objects.Count, $"seed {seed}: zone {i} content changed");
                    }
                    Assert.DoesNotThrow(() => IslandContent.ValidateAll(world.Sections), $"seed {seed}");
                }
            }
        }

        [Test]
        public void FutureExpansion_IsNotSealedOffByExistingForest()
        {
            var world = Start(16, StartingGrasslands());
            foreach (var biome in new[] { Grasslands(), StoneDesert(), Forest(), Grasslands(), StoneDesert(), Forest() })
            {
                Expand(world, biome);
                Assert.DoesNotThrow(() => IslandContent.ValidateAll(world.Sections));
            }
            Assert.IsTrue(IslandShape.Connected(Walkable(world, withBridges: true)));
        }

        [Test]
        public void PopulationFailure_LeavesTheIslandAndItsShapesUntouched()
        {
            var impossible = new IslandBiome
            {
                id = "impossible", objectPercent = 20,
                objects = { Rule(chance: 1f, min: 100, max: 100) },   // can never be met on 44..52 cells
            };
            var world = Start(0, StartingGrasslands());
            var twin = Start(0, StartingGrasslands());

            var candidate = world.Propose(1)[0];
            Assert.IsNull(world.Populate(candidate, impossible));
            Assert.AreEqual(1, world.Sections.Count, "nothing committed");

            // The failed content pass consumed nothing from the shape stream: the next proposal of the
            // island that tried it equals the next proposal of one that never did.
            var forest = Forest();
            var a = world.Commit(candidate, world.Populate(candidate, forest));
            var b = twin.Commit(twin.Propose(1)[0], null);
            Assert.IsTrue(Set(a.Cells).SetEquals(b.Cells));
            Assert.IsTrue(Set(world.Propose(1)[0].Cells).SetEquals(twin.Propose(1)[0].Cells));
        }

        [Test]
        public void MixedBiomes_RespectCapsAllowedFeaturesAndReservedAccess()
        {
            for (int seed = 0; seed < 6; seed++)
            {
                var world = Start(seed, StartingGrasslands());
                for (int round = 0; round < 2; round++)
                    foreach (var biome in Cycle())
                    {
                        Expand(world, biome);
                        Assert.DoesNotThrow(() => IslandContent.ValidateAll(world.Sections), $"seed {seed}");
                        Assert.IsTrue(IslandShape.Connected(Walkable(world, withBridges: true)), $"seed {seed}");
                        foreach (var c in Contents(world))
                        {
                            Assert.IsFalse(c.Natural().Overlaps(c.reserved), $"seed {seed}: feature on a reserved cell");
                            // Independent budget and overlap checks in addition to validation.
                            Assert.That(c.OpenCells / (double)c.land.Count, Is.GreaterThanOrEqualTo(c.biome.openPercent / 100.0), $"seed {seed}: open land");
                            var terrainLike = Set(c.mountains);
                            terrainLike.UnionWith(c.river);
                            Assert.IsFalse(terrainLike.Overlaps(c.objects.Keys), $"seed {seed}: object on mountain or river");
                        }
                    }
            }
        }

        // --- rivers -----------------------------------------------------------------------------------

        [Test]
        public void River_BlocksMovement_ButBridgeCandidatesRestoreAccess()
        {
            // An explicit river cutting a 7×7 square into two halves, with one crossing marked.
            var land = new HashSet<Vector2Int>();
            for (int x = 0; x < 7; x++) for (int y = 0; y < 7; y++) land.Add(new Vector2Int(x, y));
            var c = new IslandSectionContent(Grasslands(), land);
            for (int y = 0; y < 7; y++) c.river.Add(new Vector2Int(3, y));
            c.bridges[new Vector2Int(3, 3)] = (new Vector2Int(2, 3), new Vector2Int(4, 3));

            var ground = Set(land);
            ground.ExceptWith(c.Blocked());
            Assert.IsFalse(IslandShape.Connected(ground), "the river splits the land");
            Assert.IsTrue(IslandContent.AccessOk(land, new[] { c }), "the planned bridge rejoins it");
        }

        [Test]
        public void GeneratedRiver_EndpointsAndBanksRemainUnchangedAfterGrowth()
        {
            // Rivers are chancy (80% on grassland, cap permitting); take the first seed that gets one.
            IslandGenerator world = null;
            for (int seed = 0; seed < 40 && world == null; seed++)
            {
                var candidate = Start(seed, StartingGrasslands());
                Expand(candidate, Grasslands());
                if (candidate.Sections[1].Content.river.Count > 0) world = candidate;
            }
            Assert.IsNotNull(world, "no grassland zone with a river in 40 seeds");

            var c = world.Sections[1].Content;
            var river = new List<Vector2Int>(c.river);
            var bridges = new Dictionary<Vector2Int, (Vector2Int a, Vector2Int b)>(c.bridges);
            Assert.IsNotEmpty(c.bridges, "a river marks at least one crossing");
            Assert.IsNotEmpty(c.millAccess, "a river reserves a mill bank");
            Assert.IsFalse(Set(c.river).Overlaps(Walkable(world, withBridges: false)), "river cells block");
            Assert.IsTrue(Set(c.bridges.Keys).IsSubsetOf(Walkable(world, withBridges: true)), "crossings count as walkable");
            // Both ends are underground: inside the land, not on the coast's water.
            foreach (var end in new[] { c.river[0], c.river[c.river.Count - 1] })
                Assert.IsTrue(c.land.Contains(end), "river endpoint on land");

            Expand(world, Forest());
            Assert.AreSame(c, world.Sections[1].Content);
            CollectionAssert.AreEqual(river, c.river, "growth rewrote the river");
            CollectionAssert.AreEquivalent(bridges.Keys, c.bridges.Keys, "growth rewrote the crossings");
            Assert.DoesNotThrow(() => IslandContent.ValidateAll(world.Sections));
        }

        [Test]
        public void SmallDesert_SkipsRiverInsteadOfExceedingBudget()
        {
            // 5% of a 44..52-cell zone is 2 cells: below the 4-cell minimum, so no river at all.
            for (int seed = 0; seed < 5; seed++)
            {
                var world = Start(seed, StartingGrasslands());
                var zone = Expand(world, StoneDesert());
                Assert.IsEmpty(zone.Content.river, $"seed {seed}");
            }
        }

        // --- sweep ------------------------------------------------------------------------------------

        // The content counterpart of the shape sweep: with the prototype's profiles, every zone of every
        // biome populates and every invariant holds, step after step.
        [Test]
        public void BiomeSweep_PopulatesEveryZoneAndKeepsEveryInvariant()
        {
            const int seeds = 40, steps = 6;
            for (int seed = 0; seed < seeds; seed++)
            {
                var world = Start(seed, StartingGrasslands());
                var cycle = Cycle();
                for (int step = 0; step < steps; step++)
                {
                    var section = Expand(world, cycle[step % cycle.Length]);
                    Assert.IsNotNull(section.Content, $"seed {seed}, step {step}");
                    Assert.AreEqual(cycle[step % cycle.Length].terrain, section.Content.biome.terrain);
                    Assert.DoesNotThrow(() => IslandContent.ValidateAll(world.Sections), $"seed {seed}, step {step}");
                }
            }
        }
    }
}
