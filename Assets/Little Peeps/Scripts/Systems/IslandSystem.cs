using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Tilemaps;
using Debug = UnityEngine.Debug;

namespace LittlePeeps
{
    // MonoBehaviour that owns IslandGrid and IslandGenerator; offers the zones an age may grow into
    // (ProposeZones), grows the island by the one the player picked (CommitZone) and puts each zone's
    // natural content on the grid through StructureSystem, the same path as anything the player builds.
    public class IslandSystem : MonoBehaviour
    {
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Tilemap tilemap;

        // Coastline outline. The art draws the island's edge OUTSIDE the land cell, so these tiles land on
        // water cells and need their own tilemap — one WITHOUT a collider, or the ground collider would
        // grow a cell past the coast in every direction.
        [SerializeField] private Tilemap trimTilemap;

        [Tooltip("Tiles for any terrain whose biome has no tile set of its own (and for the editor preview).")]
        [SerializeField] private IslandTileSet tileSet;

        [Tooltip("The biomes an age can grow into. Every age offers each of them once, each on its own " +
                 "zone shape, and the player picks one.")]
        [SerializeField] private List<BiomeDef> biomes = new();

        [Tooltip("Places the zones' natural content (trees, rivers, the starting house...).")]
        [SerializeField] private StructureSystem structureSystem;

        [Tooltip("Seed for the editor-only \"Generate Island\" preview (context menu). Runs take theirs " +
                 "from the StartConfig.")]
        [SerializeField] private int previewSeed = 1;

        public IslandGrid Grid { get; private set; }
        public IslandGenerator Generator { get; private set; }
        public IReadOnlyList<BiomeDef> Biomes => biomes;

        private StructureDef house;   // the run's starting house def, attached to the start zone's footprint

        // Generate the island for a new run. RunManager.StartNewRun() owns the timing — IslandSystem
        // never auto-builds in Awake, so generation isn't duplicated or ordered by chance. Seed 0 means
        // a fresh random seed; whichever seed is used is logged, so any island a player reports can be
        // rebuilt by copying it into the StartConfig. Null rules fall back to the defaults; a null start
        // biome leaves the start as bare land.
        public void GenerateForRun(int seed = 0, IslandRules rules = null, BiomeDef startBiome = null, StructureDef house = null)
        {
            if (seed == 0) seed = Random.Range(1, int.MaxValue);
            this.house = house;
            Build(seed, rules ?? new IslandRules(), startBiome);
        }

        // The zones this age may grow into: one per biome, each already populated, for the player to
        // choose between. Nothing changes here — CommitZone takes the chosen offer and the others are
        // simply dropped. Runs synchronously on the frame the age is bought: measured at a few ms for
        // three biomes even on a late-age island (the shape search dominates, content is ~1 ms a zone),
        // and the game is frozen for the pick anyway.
        //
        // Empty in two cases, both reported: no valid shape at all, which is a rules-tuning failure
        // (see IslandRules) the seed sweep test exists to catch; or no biome whose content fits any of
        // the shapes. Either way the age goes on without growing — the player paid for it.
        public List<ZoneOffer> ProposeZones()
        {
            var offers = new List<ZoneOffer>();
            if (Generator == null) return offers;
            if (biomes.Count == 0)
            {
                Debug.LogError("IslandSystem: no biomes assigned — there is nothing to grow the island into.", this);
                return offers;
            }

            var clock = Stopwatch.StartNew();
            var profiles = new List<IslandBiome>(biomes.Count);
            foreach (var biome in biomes) profiles.Add(Usable(biome)?.profile);
            var proposals = ZoneOffers.Build(Generator, profiles);
            clock.Stop();

            if (proposals.Count == 0)
            {
                Debug.LogError($"IslandSystem: no valid expansion in {Generator.LastAttempts} attempts (seed " +
                               $"{Generator.Seed}, {Generator.Land.Count} cells) — island unchanged. The " +
                               $"IslandRules need loosening; the seed sweep test should have caught this.", this);
                return offers;
            }

            var offered = new HashSet<int>();
            foreach (var (index, candidate, content) in proposals)
            {
                offers.Add(new ZoneOffer(biomes[index], candidate, content));
                offered.Add(index);
            }
            for (int i = 0; i < biomes.Count; i++)
                if (profiles[i] != null && !offered.Contains(i))
                    Debug.LogWarning($"IslandSystem: biome '{biomes[i].name}' fits none of this age's zone shapes " +
                                     $"(seed {Generator.Seed}) — no card for it. Loosen its budgets.", biomes[i]);

            Debug.Log($"IslandSystem: {offers.Count} zone offers in {clock.Elapsed.TotalMilliseconds:F1} ms " +
                      $"({Generator.LastAttempts} attempts; island {Generator.Land.Count} cells).", this);
            return offers;
        }

        // Grow the island by the chosen zone: committed, drawn and populated. Driven explicitly by
        // AgeSequencer (not an event) so expansion happens exactly once, in order, during the
        // transition. An offer from before an earlier commit is stale and the generator refuses it —
        // that is a bug in the flow, not a situation, so it is allowed to throw.
        public void CommitZone(ZoneOffer offer)
        {
            if (Generator == null || offer == null) return;

            var section = Generator.Commit(offer.Candidate, offer.Content);
            CommitToGrid(section);
            RefreshTilemap();
            PlaceContent(section);
            Debug.Log($"IslandSystem: age zone +{section.Cells.Count} cells of " +
                      $"{(offer.Biome != null ? offer.Biome.name : "no biome")} ({Generator.LastFill} repair cells); " +
                      $"island now {Generator.Land.Count} cells.", this);
        }

        // Right-click the component in Inspector → Generate Island
        [ContextMenu("Generate Island")]
        private void GenerateIsland()
        {
            house = null;
            Build(previewSeed, new IslandRules(), null);
    #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(tilemap);
            if (trimTilemap != null) UnityEditor.EditorUtility.SetDirty(trimTilemap);
    #endif
        }

        private void Build(int seed, IslandRules rules, BiomeDef startBiome)
        {
            ValidateBiomes(startBiome);
            startBiome = Usable(startBiome);

            var clock = Stopwatch.StartNew();
            Grid = new IslandGrid(cellSize);
            Generator = new IslandGenerator(rules, seed);
            var start = Generator.GenerateStart();

            IslandSectionContent content = null;
            if (startBiome != null)
            {
                content = Generator.Populate(start, startBiome.profile, house != null ? house.Footprint : default);
                if (content == null)
                    Debug.LogError($"IslandSystem: start biome '{startBiome.name}' does not fit the starting island " +
                                   $"(seed {seed}) — starting as bare land. Loosen its budgets.", this);
            }
            var section = Generator.Commit(start, content);
            clock.Stop();

            CommitToGrid(section);
            RefreshTilemap();
            PlaceContent(section);
            Debug.Log($"IslandSystem: seed {seed} → start of {section.Cells.Count} cells in " +
                      $"{clock.Elapsed.TotalMilliseconds:F1} ms ({Generator.LastAttempts} attempts).", this);
        }

        // A bad biome asset is reported at run start, not halfway through an age transition, and its
        // zones come out as bare land rather than throwing inside the transition.
        private readonly HashSet<BiomeDef> invalidBiomes = new();

        private void ValidateBiomes(BiomeDef startBiome)
        {
            invalidBiomes.Clear();
            var ids = new Dictionary<string, BiomeDef>();
            foreach (var biome in biomes) Validate(biome);
            if (startBiome != null) Validate(startBiome);

            void Validate(BiomeDef biome)
            {
                if (biome == null) { Debug.LogError("IslandSystem: an empty slot in the biome list.", this); return; }
                try { biome.profile.Validate(); }
                catch (System.ArgumentException e)
                {
                    invalidBiomes.Add(biome);
                    Debug.LogError($"IslandSystem: biome '{biome.name}' is skipped — {e.Message}", biome);
                    return;
                }
                // The id seeds the biome's content streams: two biomes sharing one would roll the same
                // dice for the same zone. Not fatal, but not what anyone meant.
                if (ids.TryGetValue(biome.profile.id, out var other) && other != biome)
                    Debug.LogWarning($"IslandSystem: biomes '{biome.name}' and '{other.name}' share the id '{biome.profile.id}' — give each its own.", biome);
                ids[biome.profile.id] = biome;
            }
        }

        private BiomeDef Usable(BiomeDef biome) => biome != null && !invalidBiomes.Contains(biome) ? biome : null;

        // Every generated cell is land; its terrain is the zone's biome (Grass for a bare zone).
        private void CommitToGrid(IslandSection section)
        {
            var terrain = section.Content?.biome.terrain ?? TerrainType.Grass;
            foreach (var cell in section.Cells) Grid.SetCell(cell, terrain);
        }

        // Natural content goes through the regular placement path, so it is registered, sellable (where
        // the def allows) and torn down with the run like anything else. Placement order is the
        // generator's: house first, then mountains and river, then objects.
        private void PlaceContent(IslandSection section)
        {
            if (section.Content == null) return;
            if (structureSystem == null)
            {
                Debug.LogWarning("IslandSystem: no StructureSystem assigned — zone content is not placed.", this);
                return;
            }

            var biome = section.Content.biome;
            bool missing = false;
            var wrongTerrain = new HashSet<StructureDef>();
            foreach (var (cell, def) in section.Content.Features(house))
            {
                if (def == null) { missing = true; continue; }
                // A def that forbids this biome's ground is a data conflict, not a placement problem:
                // one clear line per def instead of a warning per cell from PlaceInitial.
                if (!Allows(def, biome.terrain)) { wrongTerrain.Add(def); continue; }
                structureSystem.PlaceInitial(def, cell);
            }
            if (missing)
                Debug.LogWarning($"IslandSystem: biome '{biome.id}' has a feature with no StructureDef assigned — those cells stay empty.", this);
            foreach (var def in wrongTerrain)
                Debug.LogWarning($"IslandSystem: biome '{biome.id}' grows '{def.id}', but that def's allowedTerrain excludes " +
                                 $"{biome.terrain} — add it (or clear the list) or its cells stay empty.", def);
        }

        private static bool Allows(StructureDef def, TerrainType terrain)
        {
            var allowed = def.allowedTerrain;
            if (allowed == null || allowed.Length == 0) return true;
            foreach (var t in allowed) if (t == terrain) return true;
            return false;
        }

        // Full repaint of both tile layers from the grid. Every cell the grid holds is land — the outline
        // ring around it is worked out by the painter, not stored, so the island never has to be generated
        // oversized to leave room for its own edge. Each terrain draws with its biome's tile set, falling
        // back to the default one.
        private void RefreshTilemap()
        {
            var sets = new Dictionary<TerrainType, IslandTileSet>();
            foreach (var biome in biomes)
                if (biome != null && biome.tileSet != null && !sets.ContainsKey(biome.profile.terrain))
                    sets[biome.profile.terrain] = biome.tileSet;

            IslandTilePainter.Repaint(Grid, terrain => sets.TryGetValue(terrain, out var set) ? set : tileSet, tilemap, trimTilemap);
            EventBus<IslandRepaintedEvent>.Publish(new IslandRepaintedEvent());
        }
    }
}
