using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Serialization;
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
        // water cells and need their own tilemaps — WITHOUT a collider, or the ground collider would grow a
        // cell past the coast in every direction. Two of them: the horizontal trim, the bands under the island,
        // and the vertical trim, the columns down its sides.
        [Tooltip("Horizontal trim: the outline's bands, under the island's south-facing coast. No collider. One " +
                 "of the water's coast tilemaps.")]
        [FormerlySerializedAs("trimTilemap")]
        [SerializeField] private Tilemap trimHorizontalTilemap;

        [Tooltip("Vertical trim: the outline's columns, beside the island's east- and west-facing coast — over " +
                 "the horizontal trim, so a bay's corner is a band with the wall's column on it. No collider; its " +
                 "material, sorting layer and order are set from the horizontal trim's (one above it). One of the " +
                 "water's coast tilemaps too. Empty: the columns go on the horizontal trim and bay corners show " +
                 "their band alone.")]
        [FormerlySerializedAs("trimSideTilemap")]
        [FormerlySerializedAs("trimOverlayTilemap")]
        [SerializeField] private Tilemap trimVerticalTilemap;

        [Tooltip("Decor specks over the ground (each tile set's Decor lists): no collider, the ground's " +
                 "material (so it bends with the land), sorted above the ground and below the borders. " +
                 "Empty: no decor.")]
        [SerializeField] private Tilemap decorTilemap;

        // The waves breaking on the coast: the outline's third layer, under the island, on tilemaps of their own
        // (no collider). Their tiles are made at run time from the Coast Wave Set, so they are drawn in Play Mode
        // only — the island's Edit Mode preview has no sea either.
        [Tooltip("The coast's waves: the artist's foam frames and their timing. Empty: no waves.")]
        [SerializeField] private CoastWaveSet coastWaves;

        [Tooltip("Waves under the island's bands (its south-facing coast). No collider; material, sorting layer and " +
                 "order are set from the horizontal trim's — under the whole island.")]
        [SerializeField] private Tilemap waveHorizontalTilemap;

        [Tooltip("Waves beside its columns (the east- and west-facing coast), over the horizontal waves. No collider; " +
                 "set up from the horizontal trim like the other.")]
        [SerializeField] private Tilemap waveVerticalTilemap;

        // The island's shadow on the water: the outline's lowest layer, under the waves, on a tilemap of its own
        // (no collider). An ordinary tile asset, so unlike the waves it can be painted outside Play Mode too.
        [Tooltip("The island's shadow on the water: one tile under each band (its south-facing coast), its sprite " +
                 "hanging two cells down. Empty: no shadow.")]
        [SerializeField] private TileBase coastShadowTile;

        [Tooltip("The shadow's tilemap. No collider; material, sorting layer and order are set from the horizontal " +
                 "trim's — under the waves.")]
        [SerializeField] private Tilemap coastShadowTilemap;

        // What a structure with a WaterProjection throws on the water. The art is the island's, for all of them.
        [Tooltip("The dark block of a structure's projection on the water, stretched to its footprint's width. A " +
                 "plain gradient, its pivot top centre: the block hangs from the footprint's bottom edge. Empty: none.")]
        [SerializeField] private Sprite projectionDark;

        [Tooltip("The light block over it, a little narrower (the prefab's Water Projection says how much). Empty: none.")]
        [SerializeField] private Sprite projectionLight;

        [Tooltip("Tiles for any terrain whose biome has no tile set of its own (and for the editor preview).")]
        [SerializeField] private IslandTileSet tileSet;

        [Tooltip("Soft borders between terrains, TOP FIRST: a terrain lies over every one listed after it (and " +
                 "every one not listed) and draws the border where they meet, on its own tilemap — sorted by " +
                 "this order, whatever its renderer says. List a terrain that covers others even before it has " +
                 "border art, or the ones below take it for lower.")]
        [SerializeField] private List<IslandBorderLayer> borders = new();

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

        // The section committed last — the start island at run start, then each age's zone — and the
        // structures placed on it: what the island rise brings up out of the sea.
        public IslandSection LastSection { get; private set; }
        public IReadOnlyList<StructureInstance> LastContent => lastContent;
        private readonly List<StructureInstance> lastContent = new();

        // How the island is drawn: its tilemaps, and which of its cells they show (all of them, except an
        // age's zone while it rises out of the sea). Made with the grid, so replaced every run.
        public IslandDrawing Drawing { get; private set; }

        // The island's tile layers as set up in the scene, for what copies their look — the rise's tuning
        // window draws its sample island with the same sorting, material and border art. Painted only
        // through Drawing.
        public Tilemap GroundTilemap => tilemap;
        public Tilemap TrimHorizontalTilemap => trimHorizontalTilemap;
        public Tilemap TrimVerticalTilemap => trimVerticalTilemap;
        public Tilemap DecorTilemap => decorTilemap;
        public IReadOnlyList<IslandBorderLayer> BorderLayers => borders;
        public CoastWaveSet CoastWaves => coastWaves;
        public Tilemap WaveHorizontalTilemap => waveHorizontalTilemap;
        public Tilemap WaveVerticalTilemap => waveVerticalTilemap;
        public TileBase CoastShadowTile => coastShadowTile;
        public Tilemap CoastShadowTilemap => coastShadowTilemap;

        // How this run's structures draw their projections on the water.
        public WaterProjectionLook ProjectionLook =>
            Drawing == null ? default : Drawing.ProjectionLook(projectionDark, projectionLight);

        // The waves' tiles, made once in Play Mode for every run's drawing; null without waves.
        private CoastWaveTiles waveTiles;

        // The run's starting house and pier defs, attached to the footprints the start zone keeps for them.
        private StructureDef house;
        private StructureDef pier;

        // Generate the island for a new run. RunManager.StartNewRun() owns the timing — IslandSystem
        // never auto-builds in Awake, so generation isn't duplicated or ordered by chance. Seed 0 means
        // a fresh random seed; whichever seed is used is logged, so any island a player reports can be
        // rebuilt by copying it into the StartConfig. Null rules fall back to the defaults; a null start
        // biome leaves the start as bare land — no house, no pier.
        //
        // The pier is start content like the house: it stands on the start's east coast for the whole
        // run, and the generator keeps the sea in front of it open through every age.
        public void GenerateForRun(int seed = 0, IslandRules rules = null, BiomeDef startBiome = null, StructureDef house = null, StructureDef pier = null)
        {
            if (seed == 0) seed = Random.Range(1, int.MaxValue);
            this.house = house;
            this.pier = pier;
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
        //
        // `forRise`: the zone is about to come up out of the sea (IslandRisePlayer), so it is committed
        // whole — grid, run, structures, everything the game knows — but not SHOWN: its land stays hidden
        // and its structures switched off. Off in the same frame they were made, so their Start waits for
        // the moment the rise switches them on: a den releases its animals, a tree builds its shadow, at its
        // own pop.
        public void CommitZone(ZoneOffer offer, bool forRise = false)
        {
            if (Generator == null || offer == null) return;

            var section = Generator.Commit(offer.Candidate, offer.Content);
            LastSection = section;
            CommitToGrid(section);
            if (forRise) Drawing.HideLand(section.Cells);
            else Drawing.RepaintAll();
            PlaceContent(section);
            if (forRise)
                foreach (var placed in lastContent)
                    placed.RuntimeObject.gameObject.SetActive(false);
            Debug.Log($"IslandSystem: age zone +{section.Cells.Count} cells of " +
                      $"{(offer.Biome != null ? offer.Biome.name : "no biome")} ({Generator.LastFill} repair cells); " +
                      $"island now {Generator.Land.Count} cells.", this);
        }

        private void OnDestroy()
        {
            waveTiles?.Dispose();
            waveTiles = null;
        }

        // The waves step in real time, so they keep breaking through every pause (the island rise plays at
        // timeScale 0).
        private void LateUpdate()
        {
            if (Drawing == null) return;
            Drawing.ShowWavesAt(Time.unscaledTimeAsDouble);
        }

        // Right-click the component in Inspector → Generate Island
        [ContextMenu("Generate Island")]
        private void GenerateIsland()
        {
            house = null;
            pier = null;
            Build(previewSeed, new IslandRules(), null);
    #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(tilemap);
            if (trimHorizontalTilemap != null) UnityEditor.EditorUtility.SetDirty(trimHorizontalTilemap);
            var vertical = Drawing.TrimVerticalTilemap;
            if (vertical != null)
            {
                UnityEditor.EditorUtility.SetDirty(vertical);
                // The drawing made it draw like the horizontal trim: keep the scene showing what is actually used.
                var renderer = vertical.GetComponent<TilemapRenderer>();
                if (renderer != null) UnityEditor.EditorUtility.SetDirty(renderer);
            }
            if (decorTilemap != null) UnityEditor.EditorUtility.SetDirty(decorTilemap);
            var shadow = Drawing.CoastShadowTilemap;
            if (shadow != null)
            {
                UnityEditor.EditorUtility.SetDirty(shadow);
                // The drawing sorted it under the island: keep the scene showing what is actually used.
                var renderer = shadow.GetComponent<TilemapRenderer>();
                if (renderer != null) UnityEditor.EditorUtility.SetDirty(renderer);
            }
            foreach (var layer in borders)
                if (layer != null && layer.tilemap != null)
                {
                    UnityEditor.EditorUtility.SetDirty(layer.tilemap);
                    // The drawing sorted it by the list: keep the scene showing the order actually used.
                    var renderer = layer.tilemap.GetComponent<TilemapRenderer>();
                    if (renderer != null) UnityEditor.EditorUtility.SetDirty(renderer);
                }
    #endif
        }

        private void Build(int seed, IslandRules rules, BiomeDef startBiome)
        {
            ValidateBiomes(startBiome);
            startBiome = Usable(startBiome);

            var clock = Stopwatch.StartNew();
            Grid = new IslandGrid(cellSize);
            var usableBorders = UsableBorders();
            var waves = WaveTiles(usableBorders);
            Drawing = new IslandDrawing(Grid, TileSetFor(), tilemap, trimHorizontalTilemap, UsableDecor(usableBorders), seed,
                                        usableBorders, UsableTrimVertical(),
                                        waves != null ? waveHorizontalTilemap : null, waves != null ? waveVerticalTilemap : null,
                                        waves, UsableCoastShadow(usableBorders), coastShadowTile);
            Generator = new IslandGenerator(rules, seed);
            var start = Generator.GenerateStart();

            IslandSectionContent content = null;
            if (startBiome != null)
            {
                content = Generator.Populate(start, startBiome.profile, house != null ? house.Footprint : default,
                                             pier != null ? pier.Footprint : default);
                if (content == null)
                    Debug.LogError($"IslandSystem: start biome '{startBiome.name}' does not fit the starting island " +
                                   $"together with the house and the pier (seed {seed}) — starting as bare land. " +
                                   $"Loosen its budgets.", this);
            }
            var section = Generator.Commit(start, content);
            LastSection = section;
            clock.Stop();

            CommitToGrid(section);
            Drawing.RepaintAll();
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
        // generator's: house and pier first, then mountains and river, then objects.
        private void PlaceContent(IslandSection section)
        {
            lastContent.Clear();
            if (section.Content == null) return;
            if (structureSystem == null)
            {
                Debug.LogWarning("IslandSystem: no StructureSystem assigned — zone content is not placed.", this);
                return;
            }

            var biome = section.Content.biome;
            bool missing = false;
            var wrongTerrain = new HashSet<StructureDef>();
            foreach (var (cell, def) in section.Content.Features(house, pier))
            {
                if (def == null) { missing = true; continue; }
                // A def that forbids this biome's ground is a data conflict, not a placement problem:
                // one clear line per def instead of a warning per cell from PlaceInitial.
                if (!Allows(def, biome.terrain)) { wrongTerrain.Add(def); continue; }
                var placed = structureSystem.PlaceInitial(def, cell);
                if (placed != null) lastContent.Add(placed);
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

        // Each terrain draws with its biome's tile set, falling back to the default one. Read once per run
        // (the drawing keeps it): the biome list does not change while one plays. Every cell the grid holds
        // is land — the outline ring around it is worked out by the painter, not stored, so the island never
        // has to be generated oversized to leave room for its own edge.
        private System.Func<TerrainType, IslandTileSet> TileSetFor()
        {
            var sets = new Dictionary<TerrainType, IslandTileSet>();
            foreach (var biome in biomes)
                if (biome != null && biome.tileSet != null && !sets.ContainsKey(biome.profile.terrain))
                    sets[biome.profile.terrain] = biome.tileSet;

            return terrain => sets.TryGetValue(terrain, out var set) ? set : tileSet;
        }

        // The border list as the drawing takes it, checked once per run. A terrain listed twice keeps only
        // its first row (that is the place it ranks at anyway). Each layer's repaint clears its whole
        // tilemap, so a row may not draw on the ground or either trim layer, and a tilemap shared by two
        // rows stays with the first. A row that loses its tilemap still ranks in the order — it just draws
        // nothing.
        private List<IslandBorderLayer> UsableBorders()
        {
            var usable = new List<IslandBorderLayer>();
            var terrains = new HashSet<TerrainType>();
            var tilemaps = new HashSet<Tilemap>();
            foreach (var layer in borders)
            {
                if (layer == null) continue;
                if (!terrains.Add(layer.terrain))
                {
                    Debug.LogError($"IslandSystem: {layer.terrain} is in the border list twice — only its first row is used.", this);
                    continue;
                }

                var layerTilemap = layer.tilemap;
                if (layerTilemap != null &&
                    (layerTilemap == tilemap || layerTilemap == trimHorizontalTilemap || layerTilemap == trimVerticalTilemap))
                {
                    Debug.LogError($"IslandSystem: the {layer.terrain} border is set to draw on the island's own " +
                                   $"'{layerTilemap.name}' tilemap — give it one of its own; this one is not drawn.", this);
                    layerTilemap = null;
                }
                else if (layerTilemap != null && !tilemaps.Add(layerTilemap))
                {
                    Debug.LogError($"IslandSystem: the {layer.terrain} border shares its tilemap '{layerTilemap.name}' with a " +
                                   "row above it — give each terrain its own; this one is not drawn.", this);
                    layerTilemap = null;
                }
                else if (layerTilemap == null && layer.tiles != null)
                {
                    Debug.LogWarning($"IslandSystem: the {layer.terrain} border has art but no tilemap — it is not drawn.", this);
                }
                usable.Add(new IslandBorderLayer(layer.terrain, layer.tiles, layerTilemap));
            }
            return usable;
        }

        // The decor's repaint clears its whole tilemap too, so it may not draw on one another layer uses.
        private Tilemap UsableDecor(IReadOnlyList<IslandBorderLayer> usableBorders)
        {
            if (decorTilemap == null) return null;
            bool shared = decorTilemap == tilemap || decorTilemap == trimHorizontalTilemap ||
                          decorTilemap == trimVerticalTilemap;
            foreach (var layer in usableBorders) shared |= layer.tilemap == decorTilemap;
            if (!shared) return decorTilemap;

            Debug.LogError($"IslandSystem: the decor is set to draw on '{decorTilemap.name}', which another island " +
                           "layer draws on — give it a tilemap of its own; no decor is drawn.", this);
            return null;
        }

        // The waves' tiles for this run's drawing: made the first time a run is built in Play Mode, then kept; null
        // outside Play Mode (they are no asset — painted into the scene they would be saved as missing tiles), and
        // when the set or its tilemaps cannot draw, said once per run. Both tilemaps are asked for: a coast whose
        // columns had no waves would look nearly right and go unnoticed. And each must be the waves' own, since
        // their repaint clears it.
        private CoastWaveTiles WaveTiles(IReadOnlyList<IslandBorderLayer> usableBorders)
        {
            if (!Application.isPlaying || coastWaves == null) return null;

            if (waveHorizontalTilemap == null || waveVerticalTilemap == null)
            {
                Debug.LogWarning("IslandSystem: a Coast Wave Set but not both wave tilemaps — the coast has no waves. " +
                                 "Give Wave Horizontal and Wave Vertical a tilemap each.", this);
                return null;
            }
            var others = new HashSet<Tilemap> { tilemap, trimHorizontalTilemap, trimVerticalTilemap, decorTilemap };
            foreach (var layer in usableBorders)
                if (layer.tilemap != null) others.Add(layer.tilemap);
            if (waveHorizontalTilemap == waveVerticalTilemap || others.Contains(waveHorizontalTilemap) ||
                others.Contains(waveVerticalTilemap))
            {
                Debug.LogError("IslandSystem: the wave tilemaps share a tilemap with each other or with another island " +
                               "layer — give each its own; the coast has no waves.", this);
                return null;
            }

            return waveTiles ??= CoastWaveTiles.Create(coastWaves);
        }

        // The shadow's tilemap for this run's drawing; null without a tile, and when it cannot draw, said once per
        // run. It must be the shadow's own, since its repaint clears it.
        private Tilemap UsableCoastShadow(IReadOnlyList<IslandBorderLayer> usableBorders)
        {
            if (coastShadowTile == null) return null;

            if (coastShadowTilemap == null)
            {
                Debug.LogWarning("IslandSystem: a Coast Shadow Tile but no Coast Shadow Tilemap — the island casts no " +
                                 "shadow on the water.", this);
                return null;
            }
            var others = new HashSet<Tilemap>
            {
                tilemap, trimHorizontalTilemap, trimVerticalTilemap, decorTilemap, waveHorizontalTilemap, waveVerticalTilemap
            };
            foreach (var layer in usableBorders)
                if (layer.tilemap != null) others.Add(layer.tilemap);
            if (!others.Contains(coastShadowTilemap)) return coastShadowTilemap;

            Debug.LogError($"IslandSystem: the shadow is set to draw on '{coastShadowTilemap.name}', which another island " +
                           "layer draws on — give it a tilemap of its own; the island casts no shadow on the water.", this);
            return null;
        }

        // So does the vertical trim's. A border or the decor set to draw on it is turned away above; what is
        // left to check is the ground and the horizontal trim. Without one the columns go on the horizontal
        // trim, which keeps the island's sides but loses the column of every bay's corner — said here, since it
        // looks almost right.
        private Tilemap UsableTrimVertical()
        {
            if (trimVerticalTilemap == null)
            {
                if (trimHorizontalTilemap != null)
                    Debug.LogWarning("IslandSystem: no Trim Vertical Tilemap — the outline's columns go on the horizontal " +
                                     "trim, and bay corners lose theirs. Give them a tilemap of their own.", this);
                return null;
            }
            if (trimVerticalTilemap != tilemap && trimVerticalTilemap != trimHorizontalTilemap) return trimVerticalTilemap;

            Debug.LogError($"IslandSystem: the vertical trim is set to draw on '{trimVerticalTilemap.name}', which another " +
                           "island layer draws on — give it a tilemap of its own; bay corners lose their column.", this);
            return null;
        }
    }
}
