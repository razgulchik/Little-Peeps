using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LittlePeeps
{
    // Pure C# autotiler: turns the sparse IslandGrid into three tilemap layers.
    //  - ground: one tile per land cell — checkerboard shade from cell parity, rounded variant at the
    //            island's outer corners;
    //  - decor:  specks scattered over the ground, on a random share of the land cells (see HasDecor);
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
    // What counts as land is a Land, not the grid itself: the grid minus whatever the caller holds back.
    // That is the island rise — a new zone is on the grid from the moment it is committed, but its tiles
    // come out of the water one at a time, and the coast has to be right for what is on screen at every
    // step. So there are two ways to paint: Repaint redraws everything (run start, a whole zone at once),
    // RepaintAround redraws only what one cell turning land or water can change. Both decide each cell
    // with the same PaintOf, which is also what makes the two provably agree (IslandTilePainterTests).
    public static class IslandTilePainter
    {
        // The land the painter draws: the grid's cells, minus any the caller holds back. Reads through to
        // the grid and the predicate on every call, so it never goes stale as either changes.
        public sealed class Land
        {
            private readonly IslandGrid grid;
            private readonly Func<Vector2Int, bool> hidden;

            public Land(IslandGrid grid, Func<Vector2Int, bool> hidden = null)
            {
                this.grid = grid;
                this.hidden = hidden;
            }

            public bool Contains(Vector2Int coord) =>
                grid != null && grid.GetCell(coord) != null && (hidden == null || !hidden(coord));

            public TerrainType TerrainOf(Vector2Int coord) => grid.GetCell(coord).terrain;

            public IEnumerable<Vector2Int> Cells()
            {
                if (grid == null) yield break;
                foreach (var coord in grid.Cells.Keys)
                    if (hidden == null || !hidden(coord)) yield return coord;
            }
        }

        // What one cell draws: a land cell its ground piece, a water cell its outline piece (None = bare
        // water). Tiles are not chosen here — they depend on the tile set, which is the terrain's.
        public readonly struct CellPaint
        {
            public readonly bool land;
            public readonly IslandGroundPiece ground;   // land cells
            public readonly IslandTrimPiece trim;       // water cells
            public readonly Vector2Int trimSource;      // the land cell whose terrain colours the outline

            private CellPaint(bool land, IslandGroundPiece ground, IslandTrimPiece trim, Vector2Int trimSource)
            {
                this.land = land;
                this.ground = ground;
                this.trim = trim;
                this.trimSource = trimSource;
            }

            public static CellPaint Ground(IslandGroundPiece piece) => new(true, piece, IslandTrimPiece.None, default);
            public static CellPaint Trim(IslandTrimPiece piece, Vector2Int source) => new(false, default, piece, source);

            public bool DrawsSomething => land || trim != IslandTrimPiece.None;

            public override string ToString() => land ? $"ground {ground}" : $"trim {trim} from {trimSource}";
        }

        public static CellPaint PaintOf(Land land, Vector2Int coord)
        {
            if (land.Contains(coord)) return CellPaint.Ground(PickGround(land, coord));
            var piece = PickTrim(land, coord, out Vector2Int source);
            return CellPaint.Trim(piece, source);
        }

        // Every cell that draws something, and what: the land cells, and the outline ring around them.
        // Ring candidates are collected from the land cells' own neighbours rather than by scanning the
        // island's bounding box, so a hollow or scattered island costs nothing extra.
        public static Dictionary<Vector2Int, CellPaint> Plan(Land land)
        {
            var plan = new Dictionary<Vector2Int, CellPaint>();
            var ring = new HashSet<Vector2Int>();
            foreach (var coord in land.Cells())
            {
                plan[coord] = PaintOf(land, coord);
                foreach (var neighbour in Around(coord))
                    if (!land.Contains(neighbour)) ring.Add(neighbour);
            }

            foreach (var coord in ring)
            {
                var paint = PaintOf(land, coord);
                if (paint.DrawsSomething) plan[coord] = paint;
            }
            return plan;
        }

        // The cells whose paint can change when `cell` turns land or water: the cell and its 8 neighbours.
        // A land cell's ground piece reads its 4 side neighbours; a water cell's outline reads its north,
        // east, west, north-east and north-west ones. Neither looks further than one step, so nothing
        // outside this 3×3 can notice.
        public static IEnumerable<Vector2Int> Around(Vector2Int cell)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    yield return new Vector2Int(cell.x + dx, cell.y + dy);
        }

        // Repaint every layer from scratch. tileSetFor answers with a set for every terrain (a default
        // for unmapped ones); trimTilemap and decorTilemap may be null — the ground still draws, just
        // bare. decorSeed is the run's: the same seed draws the same decor.
        public static void Repaint(Land land, Func<TerrainType, IslandTileSet> tileSetFor, Tilemap groundTilemap, Tilemap trimTilemap,
                                   Tilemap decorTilemap, int decorSeed)
        {
            if (!CanPaint(tileSetFor, groundTilemap)) return;

            groundTilemap.ClearAllTiles();
            if (trimTilemap != null) trimTilemap.ClearAllTiles();
            if (decorTilemap != null) decorTilemap.ClearAllTiles();
            if (land == null) return;

            bool warnedMissing = false;
            foreach (var kv in Plan(land))
            {
                var coord = kv.Key;
                var paint = kv.Value;
                if (paint.land)
                {
                    var tileSet = tileSetFor(land.TerrainOf(coord));
                    if (tileSet == null && !warnedMissing)
                    {
                        Debug.LogWarning($"IslandTilePainter: no tile set for terrain {land.TerrainOf(coord)} — those cells stay undrawn.");
                        warnedMissing = true;
                    }
                    groundTilemap.SetTile(ToTilemapCell(coord), GroundTile(land, coord, paint, tileSetFor));
                    var decor = decorTilemap != null ? DecorTile(land, coord, paint, tileSetFor, decorSeed) : null;
                    if (decor != null) decorTilemap.SetTile(ToTilemapCell(coord), decor);
                }
                else if (trimTilemap != null)
                {
                    trimTilemap.SetTile(ToTilemapCell(coord), TrimTile(land, paint, tileSetFor));
                }
            }
        }

        // Redraw the 3×3 around `cell` after it turned land or water. Every cell in it is set on every
        // layer, cleared where it now draws nothing, so the result is exactly what Repaint would draw.
        // A missing tile set stays silent here: the full repaint that drew the island already said so.
        public static void RepaintAround(Land land, Vector2Int cell, Func<TerrainType, IslandTileSet> tileSetFor,
                                         Tilemap groundTilemap, Tilemap trimTilemap, Tilemap decorTilemap, int decorSeed)
        {
            if (!CanPaint(tileSetFor, groundTilemap) || land == null) return;

            foreach (var coord in Around(cell))
            {
                var paint = PaintOf(land, coord);
                var tilemapCell = ToTilemapCell(coord);
                groundTilemap.SetTile(tilemapCell, paint.land ? GroundTile(land, coord, paint, tileSetFor) : null);
                if (trimTilemap != null)
                    trimTilemap.SetTile(tilemapCell, paint.land ? null : TrimTile(land, paint, tileSetFor));
                if (decorTilemap != null)
                    decorTilemap.SetTile(tilemapCell, DecorTile(land, coord, paint, tileSetFor, decorSeed));
            }
        }

        // Silence here would just look like an island that failed to generate, so say which reference on
        // IslandSystem is still empty.
        private static bool CanPaint(Func<TerrainType, IslandTileSet> tileSetFor, Tilemap groundTilemap)
        {
            if (groundTilemap != null && tileSetFor != null) return true;
            Debug.LogWarning($"IslandTilePainter: nothing drawn — {(groundTilemap == null ? "ground tilemap" : "tile set lookup")} is not assigned.");
            return false;
        }

        // The ground tile a cell draws on this land; null for a cell that is not land on it.
        public static TileBase GroundTileOf(Land land, Vector2Int coord, Func<TerrainType, IslandTileSet> tileSetFor)
        {
            if (land == null || tileSetFor == null) return null;
            var paint = PaintOf(land, coord);
            return paint.land ? GroundTile(land, coord, paint, tileSetFor) : null;
        }

        private static TileBase GroundTile(Land land, Vector2Int coord, CellPaint paint, Func<TerrainType, IslandTileSet> tileSetFor)
        {
            var tileSet = tileSetFor(land.TerrainOf(coord));
            return tileSet != null ? tileSet.GetGround(IsLight(coord, tileSet.invertCheckerboard), paint.ground) : null;
        }

        // Decor may go on any land cell, the island's rounded corners too — so decor art must leave bare the
        // pixels a corner tile cuts away for the rounding, or a speck there hangs over the water.
        public static bool DrawsDecor(CellPaint paint) => paint.land;

        // Whether a cell gets decor, and which of `count` tiles: the cell's own rolls under the run's seed,
        // not draws from a sequence. A zone rising redraws the cells around each landing tile over and over,
        // in whatever order the tiles land, and a cell must come back with the decor it had. The two are
        // separate rolls — read off one, the cells that pass a low chance would all pick the first tiles.
        public static bool HasDecor(Vector2Int coord, int seed, float chance) =>
            IslandRng.CellHash(seed, coord.x, coord.y, DecorChanceSalt) * (1.0 / 4294967296.0) < chance;

        public static int DecorSlot(Vector2Int coord, int seed, int count) =>
            (int)(((ulong)IslandRng.CellHash(seed, coord.x, coord.y) * (ulong)count) >> 32);

        private const uint DecorChanceSalt = 1;

        private static TileBase DecorTile(Land land, Vector2Int coord, CellPaint paint, Func<TerrainType, IslandTileSet> tileSetFor, int seed)
        {
            if (!DrawsDecor(paint)) return null;
            var tileSet = tileSetFor(land.TerrainOf(coord));
            if (tileSet == null || !HasDecor(coord, seed, tileSet.decorChance)) return null;
            var options = tileSet.GetDecor(IsLight(coord, tileSet.invertCheckerboard));
            return options.Length > 0 ? options[DecorSlot(coord, seed, options.Length)] : null;
        }

        private static TileBase TrimTile(Land land, CellPaint paint, Func<TerrainType, IslandTileSet> tileSetFor)
        {
            if (paint.trim == IslandTrimPiece.None) return null;
            var tileSet = tileSetFor(land.TerrainOf(paint.trimSource));
            return tileSet != null ? tileSet.GetTrim(paint.trim) : null;
        }

        // IslandGrid cell c maps 1:1 to tilemap cell c — both put the centre of c at (c + 0.5) * cellSize,
        // so no round trip through world space is needed (see the header comment in IslandGrid).
        private static Vector3Int ToTilemapCell(Vector2Int coord) => new Vector3Int(coord.x, coord.y, 0);

        // Checkerboard parity. Masking with 1 rather than % 2 because grid coordinates are signed and
        // C#'s % would hand back -1 for odd negative sums.
        private static bool IsLight(Vector2Int coord, bool invert) => (((coord.x + coord.y) & 1) == 0) != invert;

        private static bool IsLand(Land land, int x, int y) => land.Contains(new Vector2Int(x, y));

        // A land cell takes a rounded variant where two outlines meet, i.e. where both orthogonal
        // neighbours of that corner are water. A cell narrow enough to qualify on two corners at once
        // would need art that does not exist, so the first match wins and the other corner stays square.
        private static IslandGroundPiece PickGround(Land land, Vector2Int c)
        {
            bool n = IsLand(land, c.x, c.y + 1);
            bool s = IsLand(land, c.x, c.y - 1);
            bool e = IsLand(land, c.x + 1, c.y);
            bool w = IsLand(land, c.x - 1, c.y);

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
        private static IslandTrimPiece PickTrim(Land land, Vector2Int c, out Vector2Int source)
        {
            var north = new Vector2Int(c.x, c.y + 1);
            var east  = new Vector2Int(c.x + 1, c.y);
            var west  = new Vector2Int(c.x - 1, c.y);
            var northEast = new Vector2Int(c.x + 1, c.y + 1);
            var northWest = new Vector2Int(c.x - 1, c.y + 1);
            bool n  = land.Contains(north);
            bool e  = land.Contains(east);
            bool w  = land.Contains(west);
            bool ne = land.Contains(northEast);
            bool nw = land.Contains(northWest);

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
