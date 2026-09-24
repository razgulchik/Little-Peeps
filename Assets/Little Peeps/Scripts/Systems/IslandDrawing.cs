using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LittlePeeps
{
    // The island as it is DRAWN: the ground and trim tilemaps painted from a grid, less whatever land is
    // held back. The grid stays the truth — placement, content and units see every cell on it — and this
    // only decides which cells the tilemaps show. That is what lets an age's zone be on the grid from the
    // moment it is committed and still come up out of the sea one tile at a time (IslandRise).
    //
    // A plain class rather than part of IslandSystem so the island rise can be played on an island other
    // than the game's: the tuning window draws a sample island of its own with one of these, and so runs
    // the rise through exactly the code the game does.
    public sealed class IslandDrawing
    {
        public IslandGrid Grid { get; }
        public Tilemap GroundTilemap { get; }
        public Tilemap TrimTilemap { get; }

        private readonly Func<TerrainType, IslandTileSet> tileSetFor;
        private readonly HashSet<Vector2Int> hidden = new();
        private readonly IslandTilePainter.Land land;   // the grid minus `hidden`

        // Set by every paint, taken by whoever mirrors the drawn island (IslandSystem → IslandRepaintedEvent).
        private bool repainted;

        // tileSetFor answers with a set for every terrain (a default for unmapped ones); trimTilemap may be
        // null — the ground still draws, just without its outline.
        public IslandDrawing(IslandGrid grid, Func<TerrainType, IslandTileSet> tileSetFor, Tilemap groundTilemap, Tilemap trimTilemap)
        {
            Grid = grid;
            this.tileSetFor = tileSetFor;
            GroundTilemap = groundTilemap;
            TrimTilemap = trimTilemap;
            land = new IslandTilePainter.Land(grid, hidden.Contains);
        }

        public bool IsHidden(Vector2Int cell) => hidden.Contains(cell);

        // Whether the cell is land on screen: on the grid and not held back.
        public bool ShowsLand(Vector2Int cell) => land.Contains(cell);

        // Both layers from scratch.
        public void RepaintAll()
        {
            IslandTilePainter.Repaint(land, tileSetFor, GroundTilemap, TrimTilemap);
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
            IslandTilePainter.RepaintAround(land, cell, tileSetFor, GroundTilemap, TrimTilemap);
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

        // Colour one drawn ground tile; white = as painted. It keeps the colour until it is next painted.
        public void TintLand(Vector2Int cell, Color tint)
        {
            if (GroundTilemap == null) return;
            var at = new Vector3Int(cell.x, cell.y, 0);
            GroundTilemap.SetTileFlags(at, TileFlags.None);   // tile assets lock their colour by default
            GroundTilemap.SetColor(at, tint);
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
