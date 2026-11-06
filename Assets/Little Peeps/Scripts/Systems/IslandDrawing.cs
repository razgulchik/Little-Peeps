using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LittlePeeps
{
    // The island as it is DRAWN: the ground, decor, trim (horizontal and vertical), border, coast wave and coast
    // shadow tilemaps painted from a grid, less whatever land is held back. The grid stays the truth — placement, content and units see
    // every cell on it — and this only decides which cells the tilemaps show. That is what lets an age's zone
    // be on the grid from the moment it is committed and still come up out of the sea one tile at a time
    // (IslandRise).
    //
    // A plain class rather than part of IslandSystem so the island rise can be played on an island other
    // than the game's: the tuning window draws a sample island of its own with one of these, and so runs
    // the rise through exactly the code the game does.
    public sealed class IslandDrawing
    {
        public IslandGrid Grid { get; }
        public Tilemap GroundTilemap { get; }
        public Tilemap TrimHorizontalTilemap { get; }   // the outline's bands, under land to the north
        public Tilemap TrimVerticalTilemap { get; }     // its columns, beside land to the east or west, over the bands
        public Tilemap DecorTilemap { get; }

        // The coast's waves, under the island: null without waves to draw.
        public Tilemap WaveHorizontalTilemap { get; }   // under the bands
        public Tilemap WaveVerticalTilemap { get; }     // beside the columns, over the horizontal waves

        // The island's shadow on the water, under the waves: null without a shadow to draw.
        public Tilemap CoastShadowTilemap { get; }

        // The soft borders between terrains, top first (IslandBorderPainter): never null, maybe empty.
        public IReadOnlyList<IslandBorderLayer> BorderLayers { get; }

        private readonly Func<TerrainType, IslandTileSet> tileSetFor;
        private readonly int decorSeed;
        private readonly IslandBorderPainter.Cascade cascade;
        private readonly HashSet<Vector2Int> hidden = new();
        private readonly HashSet<Vector2Int> rising = new();   // the zone held back, for as long as it is coming up
        private readonly IslandTilePainter.Land land;   // the grid minus `hidden`

        private readonly CoastWaveTiles waveTiles;      // null without waves; not this drawing's to destroy
        private readonly IslandTilePainter.Waves waves;
        private int waveFrame;                          // the frame the waves are painted in

        private readonly IslandTilePainter.CoastShadow coastShadow;

        // What lies on the water draws on the sorting layer and in the material of the trim, in the orders right
        // under the island's lowest layer. Top down, how many orders under it: the waves, the structures'
        // projections (no tilemaps, see ProjectionLook), the island's shadow.
        private const int WaveVerticalDepth = 1;
        private const int WaveHorizontalDepth = 2;
        private const int ProjectionLightDepth = 3;
        private const int ProjectionDarkDepth = 4;
        private const int CoastShadowDepth = 5;

        private int islandLowestOrder;
        private Material underIslandMaterial;

        // The sorting layer of what lies on the water, and the lowest order any of it takes.
        public int UnderIslandSortingLayer { get; private set; }
        public int LowestUnderIslandOrder => islandLowestOrder - CoastShadowDepth;

        // tileSetFor answers with a set for every terrain (a default for unmapped ones); the two trim tilemaps
        // and decorTilemap may be null — the ground still draws, just without its outline or decor (with no
        // vertical trim the columns go on the horizontal one, minus a bay's corners). decorSeed picks the
        // decor: the run's, so a seed always comes back with the same island. borderLayers is the cascade,
        // top first; null or empty draws no borders. Their tilemaps are sorted by it here, and the vertical
        // trim is made to draw like the horizontal, right over it.
        //
        // The waves are drawn when waveTiles is given, on whichever of their two tilemaps is (with only one, the
        // columns' waves go on it the way the trim's columns do); the caller keeps waveTiles alive and destroys
        // it. Their tilemaps are sorted under the island and drawn in the trim's material here. So is the coast
        // shadow's, under them, drawn when both it and its tile are given.
        public IslandDrawing(IslandGrid grid, Func<TerrainType, IslandTileSet> tileSetFor, Tilemap groundTilemap,
                             Tilemap trimHorizontalTilemap, Tilemap decorTilemap = null, int decorSeed = 0,
                             IReadOnlyList<IslandBorderLayer> borderLayers = null, Tilemap trimVerticalTilemap = null,
                             Tilemap waveHorizontalTilemap = null, Tilemap waveVerticalTilemap = null,
                             CoastWaveTiles waveTiles = null, Tilemap coastShadowTilemap = null,
                             TileBase coastShadowTile = null)
        {
            Grid = grid;
            this.tileSetFor = tileSetFor;
            GroundTilemap = groundTilemap;
            TrimHorizontalTilemap = trimHorizontalTilemap;
            TrimVerticalTilemap = trimVerticalTilemap;
            DecorTilemap = decorTilemap;
            this.decorSeed = decorSeed;
            BorderLayers = borderLayers ?? Array.Empty<IslandBorderLayer>();
            cascade = new IslandBorderPainter.Cascade(TerrainsOf(BorderLayers));
            land = new IslandTilePainter.Land(grid, hidden.Contains, rising.Contains);
            if (waveTiles != null && (waveHorizontalTilemap != null || waveVerticalTilemap != null))
            {
                this.waveTiles = waveTiles;
                WaveHorizontalTilemap = waveHorizontalTilemap;
                WaveVerticalTilemap = waveVerticalTilemap;
                waves = new IslandTilePainter.Waves(waveHorizontalTilemap, waveVerticalTilemap,
                                                    piece => this.waveTiles.Get(waveFrame, piece),
                                                    (horizontal, vertical) => this.waveTiles.Corner(waveFrame, horizontal, vertical));
            }
            if (coastShadowTilemap != null && coastShadowTile != null)
            {
                CoastShadowTilemap = coastShadowTilemap;
                coastShadow = new IslandTilePainter.CoastShadow(coastShadowTilemap, coastShadowTile);
            }
            MatchTrimVertical();
            MatchUnderIsland();
            StackBorderLayers();
        }

        // The columns are the same outline as the bands, so the vertical trim draws as the horizontal does: its
        // material (which is what bends it with the land while a zone rises), its sorting layer, one order up —
        // a bay's corner has the wall's column over the band. Copied rather than trusted to be set alike: a
        // column on another material would stay put while the coast bounces.
        private void MatchTrimVertical()
        {
            var horizontal = TrimHorizontalTilemap != null ? TrimHorizontalTilemap.GetComponent<TilemapRenderer>() : null;
            var vertical = TrimVerticalTilemap != null ? TrimVerticalTilemap.GetComponent<TilemapRenderer>() : null;
            if (horizontal == null || vertical == null) return;

            vertical.sharedMaterial = horizontal.sharedMaterial;
            vertical.sortingLayerID = horizontal.sortingLayerID;
            vertical.sortingOrder = horizontal.sortingOrder + 1;
            TrimVerticalTilemap.tileAnchor = TrimHorizontalTilemap.tileAnchor;
        }

        // The waves and the shadow are the outline's too, so they draw as the trim does — its material (the land's
        // bend: a zone rising lifts its foam and its shadow's edge with it), its sorting layer, its tile anchor — but
        // under the whole island, columns over bands as the trim stacks them, and the shadow below them all. That
        // puts them over everything on the water's own sorting layer (the sea, the gradient over it). Copied rather
        // than trusted to be set alike, as the vertical trim's are.
        private void MatchUnderIsland()
        {
            var likeTilemap = TrimHorizontalTilemap != null ? TrimHorizontalTilemap : GroundTilemap;
            var like = likeTilemap != null ? likeTilemap.GetComponent<TilemapRenderer>() : null;
            if (like == null) return;

            int lowest = like.sortingOrder;
            foreach (var tilemap in new[] { GroundTilemap, TrimHorizontalTilemap, TrimVerticalTilemap })
            {
                var renderer = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;
                if (renderer != null && renderer.sortingLayerID == like.sortingLayerID)
                    lowest = Mathf.Min(lowest, renderer.sortingOrder);
            }

            islandLowestOrder = lowest;
            underIslandMaterial = like.sharedMaterial;
            UnderIslandSortingLayer = like.sortingLayerID;

            Match(WaveVerticalTilemap, lowest - WaveVerticalDepth);
            Match(WaveHorizontalTilemap, lowest - WaveHorizontalDepth);
            Match(CoastShadowTilemap, lowest - CoastShadowDepth);

            void Match(Tilemap underTilemap, int order)
            {
                var renderer = underTilemap != null ? underTilemap.GetComponent<TilemapRenderer>() : null;
                if (renderer == null) return;
                renderer.sharedMaterial = like.sharedMaterial;
                renderer.sortingLayerID = like.sortingLayerID;
                renderer.sortingOrder = order;
                underTilemap.tileAnchor = likeTilemap.tileAnchor;
            }
        }

        // How a structure's projection on the water draws: over the island's shadow, under the waves. Unbent: a
        // projection rides with its structure, which moves with the land already — and a bending renderer would
        // have the rise take the whole structure for part of the land (IslandRise.DrawnAsLand).
        public WaterProjectionLook ProjectionLook(Sprite dark, Sprite light) =>
            new(dark, light, IslandBend.Unbent(underIslandMaterial), UnderIslandSortingLayer,
                islandLowestOrder - ProjectionDarkDepth, islandLowestOrder - ProjectionLightDepth);

        // How the border layers stack is the cascade's to say, so they are sorted by it rather than trusted
        // to agree with it: on the ground's sorting layer, over the ground, its decor and both trim layers,
        // the lowest covering terrain first and each one listed above it on top of it. The orders typed into
        // the tilemaps are overwritten — the list is the one place the order lives.
        private void StackBorderLayers()
        {
            var ground = GroundTilemap != null ? GroundTilemap.GetComponent<TilemapRenderer>() : null;
            if (ground == null) return;

            int order = ground.sortingOrder;
            foreach (var tilemap in new[] { DecorTilemap, TrimHorizontalTilemap, TrimVerticalTilemap })
            {
                var renderer = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;
                if (renderer != null && renderer.sortingLayerID == ground.sortingLayerID)
                    order = Mathf.Max(order, renderer.sortingOrder);
            }

            for (int i = BorderLayers.Count - 1; i >= 0; i--)
            {
                var tilemap = BorderLayers[i]?.tilemap;
                var renderer = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;
                if (renderer == null) continue;
                renderer.sortingLayerID = ground.sortingLayerID;
                renderer.sortingOrder = ++order;
            }
        }

        private static IEnumerable<TerrainType> TerrainsOf(IReadOnlyList<IslandBorderLayer> layers)
        {
            foreach (var layer in layers)
                if (layer != null) yield return layer.terrain;
        }

        public bool IsHidden(Vector2Int cell) => hidden.Contains(cell);

        // Whether the cell is land on screen: on the grid and not held back.
        public bool ShowsLand(Vector2Int cell) => land.Contains(cell);

        // Every layer from scratch.
        public void RepaintAll()
        {
            IslandTilePainter.Repaint(land, tileSetFor, GroundTilemap, TrimHorizontalTilemap, TrimVerticalTilemap,
                                      DecorTilemap, decorSeed, waves, coastShadow);
            IslandBorderPainter.Repaint(land, BorderLayers, cascade);
        }

        // Take land off the screen without taking it off the grid: these cells' tiles go, and the coast
        // redraws around the gap, until they are revealed again. They are a zone coming up from here until
        // RevealAllLand: each shows as the piece it will be in the finished zone (IslandTilePainter).
        public void HideLand(IEnumerable<Vector2Int> cells)
        {
            hidden.UnionWith(cells);
            rising.UnionWith(cells);
            RepaintAll();
        }

        // Exactly these cells hidden, every other one shown — one full repaint, however many changed. For
        // moving through a zone's rise, so what counts as rising stays as it is.
        public void HideOnly(IEnumerable<Vector2Int> cells)
        {
            hidden.Clear();
            hidden.UnionWith(cells);
            RepaintAll();
        }

        // One hidden cell comes up: only the 3×3 around it is redrawn, which is all its arrival can change.
        public void RevealLand(Vector2Int cell)
        {
            if (!hidden.Remove(cell)) return;
            IslandTilePainter.RepaintAround(land, cell, tileSetFor, GroundTilemap, TrimHorizontalTilemap,
                                            TrimVerticalTilemap, DecorTilemap, decorSeed, waves, coastShadow);
            IslandBorderPainter.RepaintAround(land, cell, BorderLayers, cascade);
        }

        // The zone is up: its cells are ordinary land from now on. With nothing left hidden they already draw
        // what ordinary land draws, so only a zone cut short needs painting.
        public void RevealAllLand()
        {
            rising.Clear();
            if (hidden.Count == 0) return;
            hidden.Clear();
            RepaintAll();
        }

        // The ground tile `cell` draws with all the grid's land shown: what a tile of a rising zone looks like
        // from the moment it lands. Null for a cell that is not on the grid.
        public TileBase FinalGroundTileOf(Vector2Int cell) =>
            IslandTilePainter.GroundTileOf(new IslandTilePainter.Land(Grid), cell, tileSetFor);

        // Colour one drawn cell of land, its decor and border pieces with it (a wet cell is wet all over);
        // white = as painted. Each tile keeps the colour until it is next painted.
        public void TintLand(Vector2Int cell, Color tint)
        {
            if (GroundTilemap == null) return;
            var at = new Vector3Int(cell.x, cell.y, 0);
            Tint(GroundTilemap, at, tint);
            if (DecorTilemap != null && DecorTilemap.HasTile(at)) Tint(DecorTilemap, at, tint);
            foreach (var layer in BorderLayers)
                if (layer != null && layer.tilemap != null && layer.tilemap.HasTile(at)) Tint(layer.tilemap, at, tint);
        }

        private static void Tint(Tilemap tilemap, Vector3Int at, Color tint)
        {
            tilemap.SetTileFlags(at, TileFlags.None);   // tile assets lock their colour by default
            tilemap.SetColor(at, tint);
        }

        // The waves as they stand `seconds` into their loop, in real time — the caller's clock: they keep breaking
        // through every pause, the island rise's included. Only a change of frame does anything: every wave tile
        // is swapped for its piece's tile in the new frame, and nothing else is painted. True when the frame
        // changed, for a caller that has to ask for the view to be drawn again.
        public bool ShowWavesAt(double seconds)
        {
            if (waveTiles == null) return false;
            // The steps were checked when the tiles were made; the set can still be edited in Play Mode.
            int frame = Mathf.Clamp(waveTiles.Set.FrameAt(seconds), 0, waveTiles.FrameCount - 1);
            if (frame == waveFrame) return false;

            // Every wave tile on both layers: with one layer missing, the other holds the columns' waves too.
            if (WaveHorizontalTilemap != null) waveTiles.Swap(WaveHorizontalTilemap, waveFrame, frame);
            if (WaveVerticalTilemap != null) waveTiles.Swap(WaveVerticalTilemap, waveFrame, frame);
            waveFrame = frame;
            return true;
        }
    }
}
