using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittlePeeps
{
    // The island's land bent like one sheet. A height for every corner of the grid goes to the shaders that
    // draw the land ("Little Peeps/Sprite Lit Bend"; "Little Peeps/Sprite Flat Color" for the side foam) as a
    // small texture, and each vertex moves up by the height under it. Neighbouring tiles share their corners,
    // so whatever the heights, the land never comes apart: a tile tilts and bends between its four corners,
    // and a wave crosses the coast as a slope rather than a staircase. A tilemap tile's own matrix cannot do
    // that — it shifts, stretches or shears a tile as a whole, never its four corners each their own way.
    //
    // The shaders look the height up by world position, filtered between corners, so a vertex that is not on
    // a corner still gets the height of the land under it. The texture spans the island with a few flat
    // corners to spare all round; it clamps, so everything beyond reads flat too. Nothing bends outside
    // Begin…End: the shaders multiply by a global switch that is 0 then — also in a scene that never began a
    // bend, since a shader global nobody set reads 0.
    //
    // A material bends when it has the shaders' weight property above 0. Whatever is drawn with one is part
    // of the sheet; something standing on the land is not, and is moved up by hand (HeightAt).
    //
    // One bend at a time: the shader globals belong to the whole project. Whoever begins one ends it.
    public sealed class IslandBend
    {
        private const int Margin = 2;   // flat corners beyond the island on every side

        private static readonly int TextureId = Shader.PropertyToID("_IslandBendTex");
        private static readonly int RectId = Shader.PropertyToID("_IslandBendRect");
        private static readonly int OnId = Shader.PropertyToID("_IslandBendOn");
        private static readonly int WeightId = Shader.PropertyToID("_IslandBendWeight");

        private static readonly Dictionary<Material, Material> UnbentCopies = new();

        private Texture2D texture;
        private float[] heights;
        private ushort[] halves;
        private Vector2Int first;   // the grid corner of texel (0, 0)
        private int width, height;
        private Vector2 origin;     // world position of that corner
        private Vector2 cell;       // world size of one cell
        private bool flat;          // every height is 0
        private bool dirty;         // heights changed since the texture last took them

        // Room for every corner of the cells from minCell to maxCell, all flat — corner (x, y) being the
        // bottom-left corner of cell (x, y).
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

            heights = new float[width * height];
            halves = new ushort[width * height];
            texture = new Texture2D(width, height, TextureFormat.RHalf, false, true)
            {
                name = "Island bend",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            flat = true;
            dirty = true;
            Apply();

            // Measured from the middle of the first texel, which is where its corner is.
            Shader.SetGlobalTexture(TextureId, texture);
            Shader.SetGlobalVector(RectId, new Vector4(origin.x - 0.5f * cell.x, origin.y - 0.5f * cell.y,
                                                       1f / (cell.x * width), 1f / (cell.y * height)));
            Shader.SetGlobalFloat(OnId, 1f);
        }

        // Flat again, and nothing bends any more.
        public void End()
        {
            Shader.SetGlobalFloat(OnId, 0f);
            if (texture != null)
            {
                if (Application.isPlaying) Object.Destroy(texture);
                else Object.DestroyImmediate(texture);
            }
            texture = null;
            heights = null;
            halves = null;
        }

        // Every corner flat.
        public void Clear()
        {
            if (heights == null || flat) return;
            Array.Clear(heights, 0, heights.Length);
            flat = true;
            dirty = true;
        }

        // A corner's height in world units; 0 outside the bend.
        public float Get(Vector2Int corner) => TryIndex(corner, out int i) ? heights[i] : 0f;

        public void Set(Vector2Int corner, float value)
        {
            if (!TryIndex(corner, out int i) || heights[i] == value) return;
            heights[i] = value;
            dirty = true;
            if (value != 0f) flat = false;
        }

        // Hands the heights to the shaders; nothing reaches them before.
        public void Apply()
        {
            if (!dirty || texture == null) return;
            for (int i = 0; i < heights.Length; i++) halves[i] = Mathf.FloatToHalf(heights[i]);
            texture.SetPixelData(halves, 0);
            texture.Apply(false, false);
            dirty = false;
        }

        // How high the land under a world position is, filtered between corners exactly as the shaders do:
        // what a thing standing there must be moved up by to stay on it.
        public float HeightAt(Vector3 world)
        {
            if (heights == null) return 0f;
            float x = (world.x - origin.x) / cell.x, y = (world.y - origin.y) / cell.y;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            return Mathf.Lerp(Mathf.Lerp(Texel(x0, y0), Texel(x0 + 1, y0), tx),
                              Mathf.Lerp(Texel(x0, y0 + 1), Texel(x0 + 1, y0 + 1), tx), ty);
        }

        // Whether what is drawn with this material bends with the land.
        public static bool Bends(Material material) =>
            material != null && material.HasProperty(WeightId) && material.GetFloat(WeightId) > 0f;

        // How much what is drawn with this material bends: 1 = with the land, 0 = not at all.
        public static void SetWeight(Material material, float weight)
        {
            if (material != null && material.HasProperty(WeightId)) material.SetFloat(WeightId, weight);
        }

        // This material, or — if it bends — a copy that does not: for what is drawn like the land without
        // being part of it (a tile still in flight). One copy per material, kept for every later bend.
        public static Material Unbent(Material material)
        {
            if (!Bends(material)) return material;
            if (UnbentCopies.TryGetValue(material, out var copy) && copy != null) return copy;
            copy = new Material(material) { name = $"{material.name} (unbent)", hideFlags = HideFlags.HideAndDontSave };
            SetWeight(copy, 0f);
            UnbentCopies[material] = copy;
            return copy;
        }

        private bool TryIndex(Vector2Int corner, out int index)
        {
            int x = corner.x - first.x, y = corner.y - first.y;
            index = y * width + x;
            return heights != null && x >= 0 && x < width && y >= 0 && y < height;
        }

        // Clamped, as the texture is.
        private float Texel(int x, int y) => heights[Mathf.Clamp(y, 0, height - 1) * width + Mathf.Clamp(x, 0, width - 1)];
    }
}
