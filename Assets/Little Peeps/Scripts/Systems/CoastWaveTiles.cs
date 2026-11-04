using System;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace LittlePeeps
{
    // A CoastWaveSet as tiles the painter can put down: one per frame and outline piece, and per frame and inner
    // corner the set has art for, made here from the set's sprites. A right-edge piece or corner is the left
    // one's sprite in a tile that mirrors it: its transform turns the sprite over about the cell's centre (the
    // sprites are sliced with a centred pivot, the island's tilemaps anchor tiles at the centre), so the art
    // needs no mirrored copy. Every tile is flagged LockTransform — without it a tilemap draws a cell with the
    // cell's own matrix and ignores the tile's — so the mirror holds wherever the tile is put down or swapped in.
    //
    // Made at run time, so the tiles are no asset: they live as long as whoever made them (the island in Play
    // Mode, the rise window's preview) and are never saved into a scene — which is why the island drawn in Edit
    // Mode has no waves, as it has no sea.
    public sealed class CoastWaveTiles : IDisposable
    {
        public CoastWaveSet Set { get; }
        public int FrameCount => tiles.GetLength(0);

        private static readonly int PieceCount = Enum.GetValues(typeof(IslandTrimPiece)).Length;
        private static readonly int CornerCount = Enum.GetValues(typeof(CoastWaveCorner)).Length;
        private static readonly int SlotCount = PieceCount + 2 * CornerCount;

        // [frame, slot]: the pieces at (int)piece, then each corner as drawn and mirrored (CornerSlot). None's
        // slots, and a corner's without art, stay empty.
        private readonly Tile[,] tiles;

        private static int CornerSlot(CoastWaveCorner corner, bool mirrored) => PieceCount + 2 * (int)corner + (mirrored ? 1 : 0);

        private CoastWaveTiles(CoastWaveSet set, Tile[,] tiles)
        {
            Set = set;
            this.tiles = tiles;
        }

        // Null — and said why — when the set cannot draw the coast: no frames, a piece's slot without art (that
        // piece would drop out of the coast on that frame and, frames being swapped tile for tile, never come
        // back), or a step showing a frame the set does not have. An inner corner with art on some frames only is
        // left out, and said: its cells show the band and the column stacked, as without corner art at all.
        public static CoastWaveTiles Create(CoastWaveSet set)
        {
            if (set == null) return null;
            string problem = ProblemOf(set);
            if (problem != null)
            {
                Debug.LogWarning($"CoastWaveTiles: '{set.name}' {problem} — the coast has no waves.", set);
                return null;
            }

            var tiles = new Tile[set.frames.Length, SlotCount];
            for (int frame = 0; frame < set.frames.Length; frame++)
                foreach (IslandTrimPiece piece in Enum.GetValues(typeof(IslandTrimPiece)))
                {
                    if (piece == IslandTrimPiece.None) continue;
                    var sprite = set.frames[frame].Of(piece, out bool mirrored);
                    tiles[frame, (int)piece] = NewTile($"{set.name} {frame + 1} {piece}", sprite, mirrored);
                }

            foreach (CoastWaveCorner corner in Enum.GetValues(typeof(CoastWaveCorner)))
            {
                if (corner == CoastWaveCorner.None) continue;
                int drawn = 0;
                foreach (var frame in set.frames) if (frame.Of(corner) != null) drawn++;
                if (drawn == 0) continue;
                if (drawn < set.frames.Length)
                {
                    Debug.LogWarning($"CoastWaveTiles: '{set.name}' has the {corner} corner on {drawn} of its " +
                                     $"{set.frames.Length} frames — it is left out; give it every frame or none.", set);
                    continue;
                }
                for (int frame = 0; frame < set.frames.Length; frame++)
                    foreach (bool mirrored in new[] { false, true })
                        tiles[frame, CornerSlot(corner, mirrored)] =
                            NewTile($"{set.name} {frame + 1} {corner}{(mirrored ? " mirrored" : "")}",
                                    set.frames[frame].Of(corner), mirrored);
            }
            return new CoastWaveTiles(set, tiles);
        }

        private static Tile NewTile(string name, Sprite sprite, bool mirrored)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = name;
            tile.hideFlags = HideFlags.HideAndDontSave;
            tile.sprite = sprite;
            tile.transform = mirrored ? Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)) : Matrix4x4.identity;
            tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        private static string ProblemOf(CoastWaveSet set)
        {
            if (set.frames == null || set.frames.Length == 0) return "has no frames";

            var missing = new StringBuilder();
            for (int i = 0; i < set.frames.Length; i++)
            {
                var frame = set.frames[i];
                if (frame == null) { missing.Append($" frame {i}: all;"); continue; }
                foreach (var (sprite, slot) in new[]
                         {
                             (frame.leftEdgeTop, "Left Edge Top"), (frame.leftEdgeMid, "Left Edge Mid"),
                             (frame.leftEdgeBottom, "Left Edge Bottom"), (frame.bottomEdgeLeft, "Bottom Edge Left"),
                             (frame.bottomEdgeMid, "Bottom Edge Mid"),
                         })
                    if (sprite == null) missing.Append($" frame {i}: {slot};");
            }
            if (missing.Length > 0) return $"has empty slots ({missing.ToString().Trim().TrimEnd(';')})";

            if (set.steps != null)
                foreach (var step in set.steps)
                    if (step.frame < 0 || step.frame >= set.frames.Length)
                        return $"has a step showing frame {step.frame}, but only frames 0–{set.frames.Length - 1}";
            return null;
        }

        // The tile of `piece` in `frame`; null for None.
        public TileBase Get(int frame, IslandTrimPiece piece) =>
            piece == IslandTrimPiece.None ? null : tiles[frame, (int)piece];

        // The inner corner's tile in `frame` for a water cell with these two outline pieces; null where they
        // make no corner, or the set has no art for it.
        public TileBase Corner(int frame, IslandTrimPiece horizontal, IslandTrimPiece vertical)
        {
            var corner = CoastWaveSet.CornerOf(horizontal, vertical, out bool mirrored);
            return corner == CoastWaveCorner.None ? null : tiles[frame, CornerSlot(corner, mirrored)];
        }

        // Every wave tile of frame `from` on the tilemap for its own in frame `to`, the coast left as it is.
        public void Swap(Tilemap tilemap, int from, int to)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                var was = tiles[from, slot];
                var now = tiles[to, slot];
                if (was != null && now != null) tilemap.SwapTile(was, now);
            }
        }

        public void Dispose()
        {
            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                if (Application.isPlaying) Object.Destroy(tile);
                else Object.DestroyImmediate(tile);
            }
        }
    }
}
