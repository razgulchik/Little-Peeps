using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LittlePeeps
{
    // One row of IslandSystem's border list: a terrain, its border art, and the tilemap the art goes on.
    //
    // The list is the cascade, top first: a terrain covers every terrain listed after it and every terrain
    // not listed at all. A row without art still holds its terrain's place in the order — the meadow has to
    // be listed above the forest even before it has a border of its own, or the forest would take it for
    // lower and spill onto it.
    [Serializable]
    public class IslandBorderLayer
    {
        public TerrainType terrain;

        [Tooltip("Its border art. Empty: the terrain keeps its place in the order but draws no border yet.")]
        public TerrainBorderSet tiles;

        [Tooltip("The tilemap its border is drawn on: no collider, the ground's material (so it bends with " +
                 "the land). Its sorting is set from this list when the island is drawn — on the ground's " +
                 "layer, above the ground, and below the border of every terrain listed above this one — so " +
                 "the order typed into its renderer does not matter.")]
        public Tilemap tilemap;

        public IslandBorderLayer() { }

        public IslandBorderLayer(TerrainType terrain, TerrainBorderSet tiles, Tilemap tilemap)
        {
            this.terrain = terrain;
            this.tiles = tiles;
            this.tilemap = tilemap;
        }
    }

    // Pure C# autotiler for the soft borders between terrains; the coast is IslandTilePainter's.
    //
    // Where two terrains meet, the one higher in the cascade draws the border, with its own art and on a
    // tilemap of its own: its fringe on the lower cell (Over…) and its edge on its own cell (Edge…). So a
    // covering terrain needs one set of art whatever lies under it, and a cell where three terrains meet
    // just carries a piece on each of two layers, the higher terrain's sorted on top. A set may also edge
    // its terrain all round (toward the terrains above it and the sea too) and spill its fringe out over
    // the sea, which lies under every terrain (TerrainBorderSet). Its fringe never goes onto a terrain
    // above it: that border is the upper terrain's, and that is what keeps the cascade.
    //
    // Like the coast, every choice comes from the cell's 4 side neighbours on the Land — the grid minus
    // what a rising zone still holds back — and nothing is stored. A hidden cell is neither covering nor
    // covered (to the sea options it is the sea it looks like on screen), and a cell turning land or
    // water changes nothing outside the 3×3 around it, so redrawing that 3×3 (RepaintAround) leaves
    // exactly what a full Repaint would.
    public static class IslandBorderPainter
    {
        // The order terrains cover each other in, top first. A terrain not in it lies under every one that
        // is; two terrains that are both missing from it draw no border between them.
        public sealed class Cascade
        {
            private readonly List<TerrainType> order = new();

            public Cascade(IEnumerable<TerrainType> topFirst)
            {
                if (topFirst == null) return;
                foreach (var terrain in topFirst)
                    if (!order.Contains(terrain)) order.Add(terrain);   // a repeat changes nothing: its first place counts
            }

            public bool Covers(TerrainType upper, TerrainType lower) => Rank(upper) < Rank(lower);

            private int Rank(TerrainType terrain)
            {
                int i = order.IndexOf(terrain);
                return i < 0 ? int.MaxValue : i;
            }
        }

        // The piece `coord` draws on the border layer of `covering`; None for a cell off that border.
        // edgeAllRound: the edge faces every other terrain and the sea, not only the terrains `covering`
        // lies over. overSea: the fringe spills onto the sea's cells along the shore too.
        //
        // Corners come before sides, so a cell with the other terrain on two adjacent sides takes the piece
        // made for both. Two OPPOSITE sides would need art that does not exist, and the island never makes
        // one: every zone is at least two cells thick along every row and column (IslandGenerator rejects
        // NarrowLand(added, 2)) and the island three (NarrowLand(combined, minWidth)), so no land cell has
        // another terrain, or the sea, on both sides of it; and water narrower than minBay between two
        // shores is filled in with land, so no sea cell has land on both sides either. Only a zone still
        // rising can show one for a moment — land held back counts as sea. The cell then degrades to one of
        // the two sides, as the coast's outline does.
        public static IslandBorderPiece PieceOf(IslandTilePainter.Land land, Vector2Int coord, TerrainType covering, Cascade cascade,
                                                bool edgeAllRound = false, bool overSea = false)
        {
            if (land == null || cascade == null) return IslandBorderPiece.None;

            // The sea (or land still held back): only a fringe made to reach out over it does.
            if (!land.Contains(coord)) return overSea ? Over(land, coord, covering) : IslandBorderPiece.None;
            var own = land.TerrainOf(coord);

            // Its own cell: the edge faces wherever a terrain it covers lies — or wherever it ends at all.
            if (own == covering)
            {
                bool n = EdgeFaces(land, coord.x, coord.y + 1, covering, cascade, edgeAllRound);
                bool s = EdgeFaces(land, coord.x, coord.y - 1, covering, cascade, edgeAllRound);
                bool w = EdgeFaces(land, coord.x - 1, coord.y, covering, cascade, edgeAllRound);
                bool e = EdgeFaces(land, coord.x + 1, coord.y, covering, cascade, edgeAllRound);

                if (n && w) return IslandBorderPiece.EdgeNorthWest;
                if (n && e) return IslandBorderPiece.EdgeNorthEast;
                if (s && w) return IslandBorderPiece.EdgeSouthWest;
                if (s && e) return IslandBorderPiece.EdgeSouthEast;
                if (n) return IslandBorderPiece.EdgeNorth;
                if (s) return IslandBorderPiece.EdgeSouth;
                if (w) return IslandBorderPiece.EdgeWest;
                if (e) return IslandBorderPiece.EdgeEast;
                return IslandBorderPiece.None;
            }

            // A cell it covers: the fringe spills over from wherever it lies.
            return cascade.Covers(covering, own) ? Over(land, coord, covering) : IslandBorderPiece.None;
        }

        // The fringe of `covering` on this cell, from whichever sides it lies on.
        private static IslandBorderPiece Over(IslandTilePainter.Land land, Vector2Int coord, TerrainType covering)
        {
            bool n = Is(land, coord.x, coord.y + 1, covering);
            bool s = Is(land, coord.x, coord.y - 1, covering);
            bool w = Is(land, coord.x - 1, coord.y, covering);
            bool e = Is(land, coord.x + 1, coord.y, covering);

            if (n && w) return IslandBorderPiece.OverNorthWest;
            if (n && e) return IslandBorderPiece.OverNorthEast;
            if (s && w) return IslandBorderPiece.OverSouthWest;
            if (s && e) return IslandBorderPiece.OverSouthEast;
            if (n) return IslandBorderPiece.OverNorth;
            if (s) return IslandBorderPiece.OverSouth;
            if (w) return IslandBorderPiece.OverWest;
            if (e) return IslandBorderPiece.OverEast;
            return IslandBorderPiece.None;
        }

        private static readonly Vector2Int[] Sides = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // Every cell the border of `covering` draws on, and with what: land cells, and with overSea the sea's
        // cells along its shore — found from its own cells' sides, so the sea costs no scan.
        public static Dictionary<Vector2Int, IslandBorderPiece> Plan(IslandTilePainter.Land land, TerrainType covering, Cascade cascade,
                                                                     bool edgeAllRound = false, bool overSea = false)
        {
            var plan = new Dictionary<Vector2Int, IslandBorderPiece>();
            if (land == null) return plan;
            foreach (var coord in land.Cells())
            {
                Add(coord);
                if (overSea && land.TerrainOf(coord) == covering)
                    foreach (var side in Sides)
                        if (!land.Contains(coord + side)) Add(coord + side);
            }
            return plan;

            void Add(Vector2Int coord)
            {
                var piece = PieceOf(land, coord, covering, cascade, edgeAllRound, overSea);
                if (piece != IslandBorderPiece.None) plan[coord] = piece;
            }
        }

        // Every border layer from scratch. A layer with no tilemap is skipped (its terrain still ranks in
        // the cascade); one with no art is cleared and stays empty.
        public static void Repaint(IslandTilePainter.Land land, IReadOnlyList<IslandBorderLayer> layers, Cascade cascade)
        {
            if (layers == null) return;
            foreach (var layer in layers)
            {
                if (layer == null || layer.tilemap == null) continue;
                layer.tilemap.ClearAllTiles();
                if (land == null || layer.tiles == null) continue;
                foreach (var kv in Plan(land, layer.terrain, cascade, layer.tiles.edgeAllRound, layer.tiles.overSea))
                    layer.tilemap.SetTile(ToTilemapCell(kv.Key), layer.tiles.Get(kv.Value));
            }
        }

        // Redraw the 3×3 around `cell` on every border layer after it turned land or water; a cell that now
        // draws nothing is cleared, so the result is exactly what Repaint would draw.
        public static void RepaintAround(IslandTilePainter.Land land, Vector2Int cell, IReadOnlyList<IslandBorderLayer> layers, Cascade cascade)
        {
            if (layers == null || land == null) return;
            foreach (var layer in layers)
            {
                if (layer == null || layer.tilemap == null) continue;
                foreach (var coord in IslandTilePainter.Around(cell))
                {
                    var piece = layer.tiles != null
                        ? PieceOf(land, coord, layer.terrain, cascade, layer.tiles.edgeAllRound, layer.tiles.overSea)
                        : IslandBorderPiece.None;
                    layer.tilemap.SetTile(ToTilemapCell(coord), piece != IslandBorderPiece.None ? layer.tiles.Get(piece) : null);
                }
            }
        }

        private static bool Is(IslandTilePainter.Land land, int x, int y, TerrainType terrain)
        {
            var coord = new Vector2Int(x, y);
            return land.Contains(coord) && land.TerrainOf(coord) == terrain;
        }

        // Whether the edge of `covering` faces this neighbour: a terrain it covers — or, all round, anything
        // that is not more of itself, the sea (and land still held back) included.
        private static bool EdgeFaces(IslandTilePainter.Land land, int x, int y, TerrainType covering, Cascade cascade, bool allRound)
        {
            var coord = new Vector2Int(x, y);
            if (!land.Contains(coord)) return allRound;
            var terrain = land.TerrainOf(coord);
            return allRound ? terrain != covering : cascade.Covers(covering, terrain);
        }

        // Grid cell c is tilemap cell c, as for the ground (see IslandTilePainter).
        private static Vector3Int ToTilemapCell(Vector2Int coord) => new Vector3Int(coord.x, coord.y, 0);
    }
}
