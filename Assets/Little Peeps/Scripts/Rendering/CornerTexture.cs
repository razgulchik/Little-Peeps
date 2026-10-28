using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittlePeeps
{
    // Values at every corner of the island's grid, handed to the shaders as a small texture they look up by world
    // position (IslandBend.hlsl): one texel per corner, filtered between them, so a vertex that is not on a corner
    // still gets the value of the land under it. The layers that move the land are built on it — the island's
    // bend (IslandBend) and the coast's bounce (CoastBounce) — each with a texture and a rect of its own.
    //
    // The texture spans the given cells with a few flat corners to spare all round; it clamps, so everything
    // beyond reads flat too.
    public sealed class CornerTexture
    {
        private const int Margin = 2;   // flat corners beyond the cells on every side

        private readonly string name;
        private readonly int channels;
        private readonly int textureId, rectId;

        private Texture2D texture;
        private float[] values;     // `channels` per corner
        private ushort[] halves;
        private Vector2Int first;   // the grid corner of texel (0, 0)
        private int width, height;
        private Vector2 origin;     // world position of that corner
        private Vector2 cell;       // world size of one cell
        private bool flat;          // every value is 0
        private bool dirty;         // values changed since the texture last took them

        // One channel = a height (RHalf), two = a push across the screen (RGHalf).
        public CornerTexture(string name, int channels, int textureId, int rectId)
        {
            if (channels < 1 || channels > 2) throw new ArgumentOutOfRangeException(nameof(channels));
            this.name = name;
            this.channels = channels;
            this.textureId = textureId;
            this.rectId = rectId;
        }

        public bool IsReady => values != null;

        public bool IsFlat => flat;

        // Room for every corner of the cells from minCell to maxCell, all flat — corner (x, y) being the
        // bottom-left corner of cell (x, y) — and the shaders pointed at it.
        public void Begin(GridLayout grid, Vector2Int minCell, Vector2Int maxCell)
        {
            End();

            first = minCell - new Vector2Int(Margin, Margin);
            width = maxCell.x - minCell.x + 2 + 2 * Margin;
            height = maxCell.y - minCell.y + 2 + 2 * Margin;
            var at = grid.CellToWorld(new Vector3Int(first.x, first.y, 0));
            var next = grid.CellToWorld(new Vector3Int(first.x + 1, first.y + 1, 0));
            origin = new Vector2(at.x, at.y);
            cell = new Vector2(next.x - at.x, next.y - at.y);

            values = new float[width * height * channels];
            halves = new ushort[values.Length];
            texture = new Texture2D(width, height, channels == 1 ? TextureFormat.RHalf : TextureFormat.RGHalf,
                                    false, true)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            flat = true;
            dirty = true;
            Apply();

            // Measured from the middle of the first texel, which is where its corner is.
            Shader.SetGlobalTexture(textureId, texture);
            Shader.SetGlobalVector(rectId, new Vector4(origin.x - 0.5f * cell.x, origin.y - 0.5f * cell.y,
                                                       1f / (cell.x * width), 1f / (cell.y * height)));
        }

        public void End()
        {
            if (texture != null)
            {
                if (Application.isPlaying) Object.Destroy(texture);
                else Object.DestroyImmediate(texture);
            }
            texture = null;
            values = null;
            halves = null;
        }

        // Every corner flat.
        public void Clear()
        {
            if (values == null || flat) return;
            Array.Clear(values, 0, values.Length);
            flat = true;
            dirty = true;
        }

        // A corner's value in world units; 0 outside the texture.
        public float Get(Vector2Int corner, int channel) =>
            TryIndex(corner, out int i) ? values[i * channels + channel] : 0f;

        public void Set(Vector2Int corner, int channel, float value)
        {
            if (!TryIndex(corner, out int i) || values[i * channels + channel] == value) return;
            values[i * channels + channel] = value;
            dirty = true;
            if (value != 0f) flat = false;
        }

        // Hands the values to the shaders; nothing reaches them before.
        public void Apply()
        {
            if (!dirty || texture == null) return;
            for (int i = 0; i < values.Length; i++) halves[i] = Mathf.FloatToHalf(values[i]);
            texture.SetPixelData(halves, 0);
            texture.Apply(false, false);
            dirty = false;
        }

        // A world position in corners: whole numbers are corners, as Get and Set take them.
        public Vector2 CornerOf(Vector2 world) =>
            new(first.x + (world.x - origin.x) / cell.x, first.y + (world.y - origin.y) / cell.y);

        // The value under a world position, filtered between corners exactly as the shaders do; 0 before Begin.
        public float Sample(Vector2 world, int channel)
        {
            if (values == null) return 0f;
            float x = (world.x - origin.x) / cell.x, y = (world.y - origin.y) / cell.y;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            return Mathf.Lerp(Mathf.Lerp(Texel(x0, y0, channel), Texel(x0 + 1, y0, channel), tx),
                              Mathf.Lerp(Texel(x0, y0 + 1, channel), Texel(x0 + 1, y0 + 1, channel), tx), ty);
        }

        private bool TryIndex(Vector2Int corner, out int index)
        {
            int x = corner.x - first.x, y = corner.y - first.y;
            index = y * width + x;
            return values != null && x >= 0 && x < width && y >= 0 && y < height;
        }

        // Clamped, as the texture is.
        private float Texel(int x, int y, int channel) =>
            values[(Mathf.Clamp(y, 0, height - 1) * width + Mathf.Clamp(x, 0, width - 1)) * channels + channel];
    }
}
