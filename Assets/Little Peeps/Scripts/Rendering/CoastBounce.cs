using UnityEngine;

namespace LittlePeeps
{
    // The coast giving under a hit: a push across the screen for every corner of the grid, a second layer on the
    // island's sheet over its bend (IslandBend). It goes to the same shaders as a small texture of its own
    // (CornerTexture), and each vertex of what bends with the land moves by the push under it — the land, its
    // trims, the side foam and the coast the water sees, so the waterline bulges with the land. Kept apart from
    // the bend so neither has to know who began the other: the island rises while the game is paused, and the
    // coast is hit while it plays.
    //
    // Nothing moves while every push is 0: Apply switches the layer off then (a global the shaders multiply by,
    // which reads 0 in a scene that never set it).
    //
    // One at a time: the shader globals belong to the whole project. CoastBump owns it.
    public sealed class CoastBounce
    {
        private static readonly int TextureId = Shader.PropertyToID("_CoastBounceTex");
        private static readonly int RectId = Shader.PropertyToID("_CoastBounceRect");
        private static readonly int OnId = Shader.PropertyToID("_CoastBounceOn");

        private readonly CornerTexture pushes = new("Coast bounce", 2, TextureId, RectId);

        public bool IsReady => pushes.IsReady;

        // Room for every corner of the cells from minCell to maxCell, all still.
        public void Begin(GridLayout grid, Vector2Int minCell, Vector2Int maxCell)
        {
            End();
            pushes.Begin(grid, minCell, maxCell);
        }

        public void End()
        {
            Shader.SetGlobalFloat(OnId, 0f);
            pushes.End();
        }

        // Every corner still.
        public void Clear() => pushes.Clear();

        // A corner's push in world units.
        public void Set(Vector2Int corner, Vector2 push)
        {
            pushes.Set(corner, 0, push.x);
            pushes.Set(corner, 1, push.y);
        }

        // Hands the pushes to the shaders, and switches the layer off when there are none.
        public void Apply()
        {
            pushes.Apply();
            Shader.SetGlobalFloat(OnId, pushes.IsFlat ? 0f : 1f);
        }

        // A world position in corners: whole numbers are corners, as Set takes them.
        public Vector2 CornerOf(Vector2 world) => pushes.CornerOf(world);
    }
}
