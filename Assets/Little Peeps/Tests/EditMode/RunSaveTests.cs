using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static LittlePeeps.Tests.TestBiomes;

namespace LittlePeeps.Tests
{
    // The island in a saved run: every zone's shape AND generation plan come back exactly, and the restored
    // island grows on. Biome profiles from TestBiomes, so this class runs under the offline harness too.
    public class IslandSnapshotTests
    {
        private static readonly Footprint House = Footprint.Rect(2, 2);
        private static readonly Footprint Pier = Footprint.Rect(1, 3);
        private static readonly Footprint[] Tent = { Footprint.Parse("#.", "##") };   // a masked one on purpose

        private static IslandRules Rules() => new() { startArea = 36 };

        // A start with the house, the pier and an extra, then two zones of the cycle's biomes.
        private static (IslandGenerator generator, Dictionary<string, IslandBiome> biomes) Island(int seed)
        {
            var biomes = new Dictionary<string, IslandBiome>();
            var start = StartingGrasslands();
            biomes[start.id] = start;
            foreach (var b in Cycle()) biomes[b.id] = b;

            var g = new IslandGenerator(Rules(), seed);
            var candidate = g.GenerateStart();
            var content = g.Populate(candidate, start, House, Pier, Tent);
            Assume.That(content, Is.Not.Null, $"seed {seed}: the start could not be populated");
            g.Commit(candidate, content);

            foreach (var biome in new[] { biomes["grass"], biomes["forest"] })
            {
                bool grown = false;
                foreach (var c in g.Propose(3))
                {
                    var zone = g.Populate(c, biome);
                    if (zone == null) continue;
                    g.Commit(c, zone);
                    grown = true;
                    break;
                }
                Assume.That(grown, $"seed {seed}: '{biome.id}' fits no zone");
            }
            return (g, biomes);
        }

        private static IslandSectionContent RoundTrip(IslandSection section, Dictionary<string, IslandBiome> biomes,
                                                      out HashSet<Vector2Int> cells)
        {
            var save = IslandSnapshot.Save(section);
            Assert.That(IslandSnapshot.TryLoad(save, id => biomes.TryGetValue(id ?? "", out var b) ? b : null,
                                               out cells, out var content, out string problem), Is.True, problem);
            return content;
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        public void EveryZone_ComesBackWithItsWholePlan(int seed)
        {
            var (g, biomes) = Island(seed);

            foreach (var section in g.Sections)
            {
                var a = section.Content;
                var b = RoundTrip(section, biomes, out var cells);

                Assert.That(cells.SetEquals(section.Cells), $"section {section.Index}: cells");
                Assert.That(b.biome, Is.SameAs(a.biome), "the zone's biome profile");
                Assert.That(b.land.SetEquals(a.land), "land");
                Assert.That(b.objects.Keys.ToHashSet().SetEquals(a.objects.Keys), "planned objects");
                Assert.That(b.mountains.SetEquals(a.mountains), "mountains");
                Assert.That(b.river, Is.EqualTo(a.river), "the river, in path order");
                Assert.That(b.house.SetEquals(a.house) && b.houseOrigin == a.houseOrigin, "house");
                Assert.That(b.pier.SetEquals(a.pier) && b.pierOrigin == a.pierOrigin, "pier");
                Assert.That(b.extras.SetEquals(a.extras), "extras");
                Assert.That(b.extraOrigins, Is.EqualTo(a.extraOrigins), "extra origins");
                Assert.That(b.clearing.SetEquals(a.clearing), "clearing");
                Assert.That(b.reserved.SetEquals(a.reserved), "reserved");
                Assert.That(b.bridges, Is.EquivalentTo(a.bridges), "planned bridges with their banks");
                Assert.That(b.mineAccess.SetEquals(a.mineAccess), "mine access");
                Assert.That(b.millAccess.SetEquals(a.millAccess), "mill access");
                Assert.That(b.Starting, Is.EqualTo(a.Starting), "whether it is the start");
                Assert.That(b.houseFootprint.CellCount, Is.EqualTo(a.houseFootprint.CellCount));
                Assert.That(b.pierFootprint.CellCount, Is.EqualTo(a.pierFootprint.CellCount));
            }
        }

        [Test]
        public void AMaskedFootprint_KeepsItsShape()
        {
            var (g, biomes) = Island(7);
            var tent = RoundTrip(g.Sections[0], biomes, out _).extraFootprints[0];

            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    Assert.That(tent.Contains(x, y), Is.EqualTo(Tent[0].Contains(x, y)), $"({x}, {y})");
        }

        // The point of storing the plan: the restored island is one the generator can keep growing.
        [TestCase(1)]
        [TestCase(42)]
        public void ARestoredIsland_IsTheSameLand_AndGrowsOn(int seed)
        {
            var (g, biomes) = Island(seed);

            var restored = new IslandGenerator(Rules(), seed);
            foreach (var section in g.Sections)
            {
                var content = RoundTrip(section, biomes, out var cells);
                restored.Restore(cells, content);
            }

            Assert.That(restored.Land.ToHashSet().SetEquals(g.Land), "the same land");
            Assert.That(restored.Sections.Count, Is.EqualTo(g.Sections.Count));

            bool grows = restored.Propose(3).Any(c => restored.Populate(c, biomes["desert"]) != null);
            Assert.That(grows, "the restored island takes a new zone");
        }

        private static List<HashSet<Vector2Int>> Offered(IslandGenerator g) =>
            g.Propose(3).Select(c => new HashSet<Vector2Int>(c.Cells)).ToList();

        private static void AssertSameZones(List<HashSet<Vector2Int>> a, List<HashSet<Vector2Int>> b, string what)
        {
            Assert.That(b.Count, Is.EqualTo(a.Count), what);
            for (int i = 0; i < a.Count; i++) Assert.That(b[i].SetEquals(a[i]), $"{what}: zone {i}");
        }

        // The whole of stage 3 for the island: a game closed on the zone pick and continued is offered the
        // zones it left, because the restored island draws them from the same dice.
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        public void ARestoredIsland_IsOfferedTheSameZones(int seed)
        {
            var (g, biomes) = Island(seed);

            var restored = new IslandGenerator(Rules(), seed);
            foreach (var section in g.Sections)
            {
                var content = RoundTrip(section, biomes, out var cells);
                restored.Restore(cells, content);
            }

            AssertSameZones(Offered(g), Offered(restored), "restored");
        }

        // Each expansion draws from its own dice: what an age offers is fixed by the seed and the island that
        // stands, so proposals made during an earlier age (the game looking, then closing) cannot shift it.
        [Test]
        public void AnEarlierAgesProposals_DoNotShiftALaterOnesOffers()
        {
            var (g, biomes) = Island(7);
            var once = new IslandGenerator(Rules(), 7);
            var twice = new IslandGenerator(Rules(), 7);
            for (int i = 0; i < g.Sections.Count; i++)
            {
                var content = RoundTrip(g.Sections[i], biomes, out var cells);
                if (i > 0) twice.Propose(3);   // looked at this age's offers once more than `once` did
                once.Restore(cells, content);
                twice.Restore(cells, RoundTrip(g.Sections[i], biomes, out _));
            }

            AssertSameZones(Offered(once), Offered(twice), "after extra proposals in earlier ages");
        }

        [Test]
        public void AZoneWhoseBiomeIsGone_IsRefused()
        {
            var (g, _) = Island(7);
            var save = IslandSnapshot.Save(g.Sections[1]);

            Assert.That(IslandSnapshot.TryLoad(save, _ => null, out _, out _, out string problem), Is.False);
            Assert.That(problem, Does.Contain("biome"));
        }
    }

    // The run's file format. JsonUtility is native, so these run in the Test Runner, not offline.
    public class RunFileTests
    {
        private static RunSnapshot Sample() => new()
        {
            seed = 1234,
            currentAge = 3,
            offeringCycle = 4,
            offeringElapsed = 37.125,
            playSeconds = 612.3456789,
            perkPicksOwed = 1,
            freeAgesLeft = 0,
            tiredGoHome = true,
            chosenZoom = 0,
            structures = { new StructureSave { id = "house", x = 2, y = -1 }, new StructureSave { id = "tree", x = 5, y = 3 } },
            bridges = { new StructureSave { id = "bridge", x = 0, y = 4, site = true } },
            fences = { new FenceSave { id = "fence", x = 1, y = 1, horizontal = true } },
            resources = { new AmountSave { type = "Food", amount = 12.5f } },
            harvested = { new AmountSave { type = "Wood", amount = 40f } },
            perks = { "geese", "geese", "magnet" },
            upgrades = { "productivity" },
            sections = { new SectionSave { cells = { 0, 0, 1, 0 } } },
        };

        [Test]
        public void RoundTrip_KeepsTheWholeRun()
        {
            Assert.That(RunFile.TryRead(RunFile.ToJson(Sample()), out RunSnapshot run, out string problem), Is.True,
                        problem);

            Assert.That(run.seed, Is.EqualTo(1234));
            Assert.That(run.currentAge, Is.EqualTo(3));
            Assert.That(run.offeringElapsed, Is.EqualTo(37.125));
            Assert.That(run.playSeconds, Is.EqualTo(612.3456789).Within(1e-9), "the clocks are doubles");
            Assert.That(run.tiredGoHome, Is.True);
            Assert.That(run.chosenZoom, Is.EqualTo(0), "the view the player picked, 0 included");
            Assert.That(run.structures.Select(s => s.id), Is.EqualTo(new[] { "house", "tree" }));
            Assert.That(run.bridges[0].site, Is.True, "a site goes back up as a site");
            Assert.That(run.fences[0].horizontal, Is.True);
            Assert.That(run.perks, Is.EqualTo(new[] { "geese", "geese", "magnet" }), "perk levels, in order");
            Assert.That(run.sections[0].cells, Is.EqualTo(new[] { 0, 0, 1, 0 }));
        }

        [Test]
        public void AChangedNumber_FailsTheChecksum()
        {
            string json = RunFile.ToJson(Sample());
            string edited = json.Replace("\"currentAge\":3", "\"currentAge\":8");
            Assume.That(edited, Is.Not.EqualTo(json), "the edit must land for the test to mean anything");

            Assert.That(RunFile.TryRead(edited, out _, out string problem), Is.False);
            Assert.That(problem, Does.Contain("checksum"));
        }

        [TestCase("")]
        [TestCase("{ not json")]
        [TestCase("{}")]
        public void NotARun_IsRefused(string json)
        {
            Assert.That(RunFile.TryRead(json, out RunSnapshot run, out _), Is.False);
            Assert.That(run, Is.Null);
        }

        // The point of a checksum over the text: a file written before a field existed still reads, the field
        // at its default. Made here as such a game would have written it — no "states" anywhere.
        [Test]
        public void AFileFromBeforeAFieldExisted_StillReads()
        {
            string older = System.Text.RegularExpressions.Regex.Replace(RunFile.ToJson(Sample()), ",\"states\":\\[\\]", "");
            older = System.Text.RegularExpressions.Regex.Replace(older, "\"checksum\":\"[0-9a-f]+\"", "\"checksum\":\"\"");
            Assume.That(older, Does.Not.Contain("states"));
            older = older.Replace("\"checksum\":\"\"", $"\"checksum\":\"{SafeFile.Checksum(older)}\"");

            Assert.That(RunFile.TryRead(older, out RunSnapshot run, out string problem), Is.True, problem);
            Assert.That(run.structures[0].states, Is.Empty);
        }

        // A run saved before the camera's view was part of it: the player picked none, so the view follows the
        // age — not view 0, the farthest, which is what a plain int's default would have made it.
        [Test]
        public void AFileFromBeforeTheChosenZoom_ReadsAsNotChosen()
        {
            string older = System.Text.RegularExpressions.Regex.Replace(RunFile.ToJson(Sample()), ",\"chosenZoom\":-?\\d+", "");
            older = System.Text.RegularExpressions.Regex.Replace(older, "\"checksum\":\"[0-9a-f]+\"", "\"checksum\":\"\"");
            Assume.That(older, Does.Not.Contain("chosenZoom"));
            older = older.Replace("\"checksum\":\"\"", $"\"checksum\":\"{SafeFile.Checksum(older)}\"");

            Assert.That(RunFile.TryRead(older, out RunSnapshot run, out string problem), Is.True, problem);
            Assert.That(run.chosenZoom, Is.EqualTo(RunContext.NotChosen));
        }

        [Test]
        public void AnOtherVersion_IsRefusedAsSuch()
        {
            string json = RunFile.ToJson(Sample()).Replace($"\"version\":{RunFile.CurrentVersion}", "\"version\":1");

            Assert.That(RunFile.TryRead(json, out _, out string problem), Is.False);
            Assert.That(problem, Does.Contain("version"));
        }

        [Test]
        public void StructureStates_RoundTrip()
        {
            var sample = Sample();
            sample.structures[1].states.Add(new StateSave { key = "source", values = { 2f, 1f, 12.5f } });

            Assert.That(RunFile.TryRead(RunFile.ToJson(sample), out RunSnapshot run, out string problem), Is.True, problem);
            Assert.That(run.structures[1].states[0].key, Is.EqualTo("source"));
            Assert.That(run.structures[1].states[0].values, Is.EqualTo(new[] { 2f, 1f, 12.5f }));
        }
    }

    // What a structure's parts carry through a save (IRunState): saved and put back as they were, and clamped
    // when the numbers are not ones the part allows. Components, so Test Runner only; Edit Mode runs no Awake,
    // and none of these needs one to take its state.
    public class PartStateTests
    {
        private GameObject go;

        [SetUp]
        public void SetUp() => go = new GameObject("part");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        private static List<float> Saved(IRunState part)
        {
            var values = new List<float>();
            part.SaveState(values);
            return values;
        }

        [Test]
        public void Mill_KeepsItsSacks_AndNeverMoreThanItHolds()
        {
            var mill = go.AddComponent<Mill>();

            mill.LoadState(new[] { 1f, 2.5f });
            Assert.That(mill.Stored, Is.EqualTo(1));
            Assert.That(Saved(mill), Is.EqualTo(new[] { 1f, 2.5f }));

            mill.LoadState(new[] { 99f, 2.5f });
            Assert.That(mill.Stored, Is.EqualTo(mill.Capacity), "a full mill");
            Assert.That(Saved(mill)[1], Is.Zero, "a full mill grinds nothing toward the next sack");
        }

        [Test]
        public void Sluice_KeepsItsStage_AndNeverMoreThanFull()
        {
            var sluice = go.AddComponent<Sluice>();

            sluice.LoadState(new[] { 1f, 4.5f });
            Assert.That(sluice.Stage, Is.EqualTo(1));
            Assert.That(Saved(sluice), Is.EqualTo(new[] { 1f, 4.5f }));

            sluice.LoadState(new[] { 99f, 4.5f });
            Assert.That(sluice.IsFull, Is.True, "a full tray");
            Assert.That(Saved(sluice)[1], Is.Zero, "a full tray settles nothing toward the next stage");
        }

        [Test]
        public void Sawmill_KeepsTheEndItWaitsFor()
        {
            var saw = go.AddComponent<Sawmill>();

            saw.LoadState(new[] { 1f });
            Assert.That(saw.Awaited, Is.EqualTo(Sawmill.End.Right));
            saw.LoadState(new[] { 0f });
            Assert.That(saw.Awaited, Is.EqualTo(Sawmill.End.Left));
        }

        [Test]
        public void Forge_KeepsItsHeat_AndIsNeverLockedCold()
        {
            var forge = go.AddComponent<ForgeHeat>();

            forge.LoadState(new[] { 3f, 1f });
            Assert.That(forge.IsOverheated, Is.True);
            Assert.That(Saved(forge), Is.EqualTo(new[] { 3f, 1f }));

            forge.LoadState(new[] { 0f, 1f });
            Assert.That(forge.IsOverheated, Is.False, "the lock lifts only at zero heat, so none at zero");
        }

        // A state goes back only onto a part of the same kind, so two kinds sharing a key would take each
        // other's numbers.
        [Test]
        public void EveryKind_HasAKeyOfItsOwn()
        {
            var parts = new IRunState[]
            {
                go.AddComponent<ResourceSource>(), go.AddComponent<Mill>(), go.AddComponent<Sawmill>(),
                go.AddComponent<ForgeHeat>(), go.AddComponent<RicePaddy>(), go.AddComponent<AnimalSpawner>(),
                go.AddComponent<Sluice>(),
            };

            Assert.That(parts.Select(p => p.StateKey), Is.Unique);
        }
    }

    // A saved run finds its structures again by id through the catalogue, so a StructureDef missing from it
    // is a structure every continued run loses. Loads the shipped assets — Test Runner only.
    public class StructureCatalogueTests
    {
        private const string CataloguePath =
            "Assets/Little Peeps/ScriptableObjects/StructureDef/Structure Catalogue Def.asset";

        [Test]
        public void EveryStructureDef_IsInTheCatalogue()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<StructureCatalogueDef>(CataloguePath);
            Assert.That(catalogue, Is.Not.Null, CataloguePath);

            var listed = new HashSet<StructureDef>(catalogue.structures);
            var missing = AssetDatabase.FindAssets("t:StructureDef")
                .Select(guid => AssetDatabase.LoadAssetAtPath<StructureDef>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(def => def != null && !listed.Contains(def))
                .Select(def => def.name)
                .ToList();

            Assert.That(missing, Is.Empty, "add these to the Structure Catalogue Def, or continued runs drop them");
        }

        [Test]
        public void CatalogueIds_AreSetAndUnique()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<StructureCatalogueDef>(CataloguePath);
            Assert.That(catalogue, Is.Not.Null, CataloguePath);

            var defs = catalogue.structures.Where(d => d != null).ToList();
            Assert.That(defs.Where(d => string.IsNullOrEmpty(d.id)).Select(d => d.name), Is.Empty, "defs without an id");
            Assert.That(defs.GroupBy(d => d.id).Where(g => g.Count() > 1).Select(g => g.Key), Is.Empty, "ids used twice");
            Assert.That(catalogue.structures.Count(d => d == null), Is.Zero, "empty slots");
        }
    }

    // A meta perk rebuilds only its stats on a continued run (GlobalUpgradeDef.AddStats) — the same sheet its
    // ApplyToRun gave the run it was built for. ScriptableObjects: Test Runner only.
    public class UpgradeRebuildTests
    {
        [Test]
        public void AStatUpgrade_AddsTheSameStats_AsWhenTheRunWasBuilt()
        {
            var upgrade = ScriptableObject.CreateInstance<StatUpgradeDef>();
            try
            {
                upgrade.modifiers = new List<StatModifier>
                {
                    new StatModifier { id = StatId.TapRadius, kind = ModifierKind.Flat, value = 4f },
                };
                var built = new RunContext();
                upgrade.ApplyToRun(built);
                var rebuilt = new RunStats();
                upgrade.AddStats(rebuilt);

                Assert.That(rebuilt.Apply(10f, StatId.TapRadius), Is.EqualTo(built.stats.Apply(10f, StatId.TapRadius)));
            }
            finally
            {
                Object.DestroyImmediate(upgrade);
            }
        }

        // The one-time grants are NOT part of the rebuild: the saved numbers already hold them.
        [Test]
        public void AOneTimeGrant_AddsNoStats()
        {
            var upgrade = ScriptableObject.CreateInstance<StartPerkPickUpgradeDef>();
            try
            {
                var stats = new RunStats();
                upgrade.AddStats(stats);
                Assert.That(stats.Apply(10f, StatId.TapRadius), Is.EqualTo(10f));
            }
            finally
            {
                Object.DestroyImmediate(upgrade);
            }
        }
    }
}
