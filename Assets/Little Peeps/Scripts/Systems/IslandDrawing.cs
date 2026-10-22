using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LittlePeeps
{
    // The island as it is DRAWN: the ground, decor, trim (horizontal and vertical) and border tilemaps painted
    // from a grid, less whatever land is held back. The grid stays the truth — placement, content and units see
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

        // The soft borders between terrains, top first (IslandBorderPainter): never null, maybe empty.
        public IReadOnlyList<IslandBorderLayer> BorderLayers { get; }

        private readonly Func<TerrainType, IslandTileSet> tileSetFor;
        private readonly int decorSeed;
        private readonly IslandBorderPainter.Cascade cascade;
        private readonly HashSet<Vector2Int> hidden = new();
        private readonly IslandTilePainter.Land land;   // the grid minus `hidden`

        // Set by every paint, taken by whoever mirrors the drawn island (IslandSystem → IslandRepaintedEvent).
        private bool repainted;

        // tileSetFor answers with a set for every terrain (a default for unmapped ones); the two trim tilemaps
        // and decorTilemap may be null — the ground still draws, just without its outline or decor (with no
        // vertical trim the columns go on the horizontal one, minus a bay's corners). decorSeed picks the
        // decor: the run's, so a seed always comes back with the same island. borderLayers is the cascade,
        // top first; null or empty draws no borders. Their tilemaps are sorted by it here, and the vertical
        // trim is made to draw like the horizontal, right over it.
        public IslandDrawing(IslandGrid grid, Func<TerrainType, IslandTileSet> tileSetFor, Tilemap groundTilemap,
                             Tilemap trimHorizontalTilemap, Tilemap decorTilemap = null, int decorSeed = 0,
                             IReadOnlyList<IslandBorderLayer> borderLayers = null, Tilemap trimVerticalTilemap = null)
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
            land = new IslandTilePainter.Land(grid, hidden.Contains);
            MatchTrimVertical();
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
                                      DecorTilemap, decorSeed);
            IslandBorderPainter.Repaint(land, BorderLayers, cascade);
            repainted = true;
        }

        // Take land off the screen without taking it off the grid: these cells' tiles go, and the coast
        // redraws around the gap, until they are revealed again.
        public void HideLand(IEnumerable<Vector2Int> cells)
        {
            hidden.UnionWith(cells);
            RepaintAll();
        }

        // Exactly these cells hidden, every other one shown — one full repaint, however many changed.
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
                                            TrimVerticalTilemap, DecorTilemap, decorSeed);
            IslandBorderPainter.RepaintAround(land, cell, BorderLayers, cascade);
            repainted = true;
        }

        public void RevealAllLand()
        {
            if (hidden.Count == 0) return;
            hidden.Clear();
            RepaintAll();
        }

        // The ground tile `cell` draws when the land shown is the grid minus `hiddenThen`: what a tile of a
        // rising zone will look like at the moment it lands. Null for a cell that is not land then.
        public TileBase GroundTileAt(Vector2Int cell, Func<Vector2Int, bool> hiddenThen) =>
            IslandTilePainter.GroundTileOf(new IslandTilePainter.Land(Grid, hiddenThen), cell, tileSetFor);

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

        // Whether anything was painted since the last call.
        public bool TakeRepainted()
        {
            bool was = repainted;
            repainted = false;
            return was;
        }
    }
}
