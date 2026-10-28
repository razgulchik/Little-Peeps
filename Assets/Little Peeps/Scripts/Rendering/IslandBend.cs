using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The island's land bent like one sheet. A height for every corner of the grid goes to the shaders that
    // draw the land ("Little Peeps/Sprite Lit Bend"; "Little Peeps/Sprite Flat Color" for the side foam) as a
    // small texture (CornerTexture), and each vertex moves up by the height under it. Neighbouring tiles share
    // their corners, so whatever the heights, the land never comes apart: a tile tilts and bends between its
    // four corners, and a wave crosses the coast as a slope rather than a staircase. A tilemap tile's own matrix
    // cannot do that — it shifts, stretches or shears a tile as a whole, never its four corners each their own way.
    //
    // The shaders look the height up by world position, filtered between corners, so a vertex that is not on
    // a corner still gets the height of the land under it. The texture spans the island with a few flat
    // corners to spare all round; it clamps, so everything beyond reads flat too. Nothing bends outside
    // Begin…End: the shaders multiply by a global switch that is 0 then — also in a scene that never began a
    // bend, since a shader global nobody set reads 0.
    //
    // A material bends when it has the shaders' weight property above 0. Whatever is drawn with one is part
    // of the sheet; something standing on the land is not, and is moved up by hand (HeightAt). The coast's
    // bounce (CoastBounce) is a second layer on the same sheet, moving the same materials by the same weight.
    //
    // One bend at a time: the shader globals belong to the whole project. Whoever begins one ends it.
    public sealed class IslandBend
    {
        private static readonly int TextureId = Shader.PropertyToID("_IslandBendTex");
        private static readonly int RectId = Shader.PropertyToID("_IslandBendRect");
        private static readonly int OnId = Shader.PropertyToID("_IslandBendOn");
        private static readonly int WeightId = Shader.PropertyToID("_IslandBendWeight");

        private static readonly Dictionary<Material, Material> UnbentCopies = new();

        private readonly CornerTexture heights = new("Island bend", 1, TextureId, RectId);

        // Room for every corner of the cells from minCell to maxCell, all flat — corner (x, y) being the
        // bottom-left corner of cell (x, y).
        public void Begin(GridLayout grid, Vector2Int minCell, Vector2Int maxCell)
        {
            End();
            heights.Begin(grid, minCell, maxCell);
            Shader.SetGlobalFloat(OnId, 1f);
        }

        // Flat again, and nothing bends any more.
        public void End()
        {
            Shader.SetGlobalFloat(OnId, 0f);
            heights.End();
        }

        // Every corner flat.
        public void Clear() => heights.Clear();

        // A corner's height in world units; 0 outside the bend.
        public float Get(Vector2Int corner) => heights.Get(corner, 0);

        public void Set(Vector2Int corner, float value) => heights.Set(corner, 0, value);

        // Hands the heights to the shaders; nothing reaches them before.
        public void Apply() => heights.Apply();

        // How high the land under a world position is, filtered between corners exactly as the shaders do:
        // what a thing standing there must be moved up by to stay on it.
        public float HeightAt(Vector3 world) => heights.Sample(world, 0);

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
    }
}
