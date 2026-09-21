using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LittlePeeps
{
    // Pure C# autotiler: turns the sparse IslandGrid into two tilemap layers.
    //  - ground: one tile per land cell — checkerboard shade from cell parity, rounded variant at the
    //            island's outer corners;
    //  - trim:   the coastline outline, painted on the NON-land cells that touch land.
    //
    // Every choice is made from the 8 neighbours ("a cell exists in the grid" == land), never from an
    // index inside a rectangle. That is what makes it survive the shapes IslandGenerator produces:
    // chamfered masses, staircase coasts, broad bays. Nothing here writes to the grid — the outline ring is
    // decoration derived from it, so the island's logical size stays exactly its playable cells and the
    // ground collider never grows past the coast.
    //
    // Which tile SET draws a cell is the terrain's (one per biome): the ground takes its own cell's set,
    // a trim piece takes the set of the land cell that gives it its shape — so a coast is drawn in the
    // colours of the zone it belongs to.
    //
    // The north side has no outline: the art is drawn without a waterline on top, by design.
    //
    // Cost is one pass over the land cells plus one over the ring around them, and it only runs when the
    // island changes (run start, age expansion), so a full repaint is cheaper than tracking dirty cells.
    public static class IslandTilePainter
    {
        // Repaint both layers from scratch. tileSetFor answers with a set for every terrain (a default
        // for unmapped ones); trimTilemap may be null — the ground still draws, just bare.
        public static void Repaint(IslandGrid grid, Func<TerrainType, IslandTileSet> tileSetFor, Tilemap groundTilemap, Tilemap trimTilemap)
        {
            if (groundTilemap == null || tileSetFor == null)
            {
                // Silence here would just look like an island that failed to generate, so say which
                // reference on IslandSystem is still empty.
                Debug.LogWarning($"IslandTilePainter: nothing drawn — {(groundTilemap == null ? "ground tilemap" : "tile set lookup")} is not assigned.");
                return;
            }

            groundTilemap.ClearAllTiles();
            if (trimTilemap != null) trimTilemap.ClearAllTiles();
            if (grid == null) return;

            // Ring candidates are collected from the land cells' own neighbours rather than by scanning the
            // island's bounding box, so a hollow or scattered island costs nothing extra.
            var ring = new HashSet<Vector2Int>();
            bool warnedMissing = false;

            foreach (var kv in grid.Cells)
            {
                Vector2Int coord = kv.Key;
                var tileSet = tileSetFor(kv.Value.terrain);
                if (tileSet == null)
                {
                    if (!warnedMissing) Debug.LogWarning($"IslandTilePainter: no tile set for terrain {kv.Value.terrain} — those cells stay undrawn.");
                    warnedMissing = true;
                }
                else
                {
                    groundTilemap.SetTile(ToTilemapCell(coord),
                                          tileSet.GetGround(IsLight(coord, tileSet.invertCheckerboard), PickGround(grid, coord)));
                }

                if (trimTilemap == null) continue;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var neighbour = new Vector2Int(coord.x + dx, coord.y + dy);
                        if (!IsLand(grid, neighbour)) ring.Add(neighbour);
                    }
            }

            if (trimTilemap == null) return;
            foreach (Vector2Int coord in ring)
            {
                var piece = PickTrim(grid, coord, out Vector2Int source);
                if (piece == IslandTrimPiece.None) continue;
                var tileSet = tileSetFor(grid.GetCell(source).terrain);
                TileBase tile = tileSet != null ? tileSet.GetTrim(piece) : null;
                if (tile != null) trimTilemap.SetTile(ToTilemapCell(coord), tile);
            }
        }

        // IslandGrid cell c maps 1:1 to tilemap cell c — both put the centre of c at (c + 0.5) * cellSize,
        // so no round trip through world space is needed (see the header comment in IslandGrid).
        private static Vector3Int ToTilemapCell(Vector2Int coord) => new Vector3Int(coord.x, coord.y, 0);

        // Checkerboard parity. Masking with 1 rather than % 2 because grid coordinates are signed and
        // C#'s % would hand back -1 for odd negative sums.
        private static bool IsLight(Vector2Int coord, bool invert) => (((coord.x + coord.y) & 1) == 0) != invert;

        private static bool IsLand(IslandGrid grid, Vector2Int coord) => grid.GetCell(coord) != null;

        private static bool IsLand(IslandGrid grid, int x, int y) => grid.GetCell(new Vector2Int(x, y)) != null;

        // A land cell takes a rounded variant where two outlines meet, i.e. where both orthogonal
        // neighbours of that corner are water. A cell narrow enough to qualify on two corners at once
        // would need art that does not exist, so the first match wins and the other corner stays square.
        private static IslandGroundPiece PickGround(IslandGrid grid, Vector2Int c)
        {
            bool n = IsLand(grid, c.x, c.y + 1);
            bool s = IsLand(grid, c.x, c.y - 1);
            bool e = IsLand(grid, c.x + 1, c.y);
            bool w = IsLand(grid, c.x - 1, c.y);

            if (!n && !w) return IslandGroundPiece.CornerTopLeft;
            if (!n && !e) return IslandGroundPiece.CornerTopRight;
            if (!s && !w) return IslandGroundPiece.CornerBottomLeft;
            if (!s && !e) return IslandGroundPiece.CornerBottomRight;
            return IslandGroundPiece.Plain;
        }

        // Outline piece for a water cell, from the land around it, and which land cell it belongs to
        // (whose terrain picks the tile set). Only the north, east and west sides of this cell can carry
        // outline — a cell whose only land neighbour is to the south draws nothing, because the island
        // has no outline along its top.
        //
        // Order is priority: the earlier a case sits, the more of the cell it covers. Cases that would need
        // two pieces at once (land on both sides, or a band rounded at both ends) only arise on geometry
        // one cell wide, which the island shapes never produce; they degrade to the nearest piece here
        // rather than leaving a gap.
        private static IslandTrimPiece PickTrim(IslandGrid grid, Vector2Int c, out Vector2Int source)
        {
            var north = new Vector2Int(c.x, c.y + 1);
            var east  = new Vector2Int(c.x + 1, c.y);
            var west  = new Vector2Int(c.x - 1, c.y);
            var northEast = new Vector2Int(c.x + 1, c.y + 1);
            var northWest = new Vector2Int(c.x - 1, c.y + 1);
            bool n  = IsLand(grid, north);
            bool e  = IsLand(grid, east);
            bool w  = IsLand(grid, west);
            bool ne = IsLand(grid, northEast);
            bool nw = IsLand(grid, northWest);

            // Inside a bay: the band along its top plus the column down one of its walls.
            source = north;
            if (n && w) return IslandTrimPiece.InnerCornerLeft;
            if (n && e) return IslandTrimPiece.InnerCornerRight;

            // Along the island's bottom: the band's ends round off wherever the coast turns.
            if (n)
            {
                if (!nw) return IslandTrimPiece.BottomEdgeLeft;
                if (!ne) return IslandTrimPiece.BottomEdgeRight;
                return IslandTrimPiece.BottomEdgeMid;
            }

            // Down the island's sides: the column is cut short at the top, where the rounding is already
            // baked into the corner tile of the land itself.
            if (e) { source = east; return ne ? IslandTrimPiece.LeftEdgeMid : IslandTrimPiece.LeftEdgeTop; }
            if (w) { source = west; return nw ? IslandTrimPiece.RightEdgeMid : IslandTrimPiece.RightEdgeTop; }

            // One cell below the last cell of a side: the stub that closes the column off.
            if (ne) { source = northEast; return IslandTrimPiece.LeftEdgeBottom; }
            if (nw) { source = northWest; return IslandTrimPiece.RightEdgeBottom; }

            return IslandTrimPiece.None;
        }
    }
}
