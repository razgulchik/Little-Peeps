using UnityEngine;

namespace LittlePeeps
{
    // How the structures' projections draw: the same for all of them, the island's to say
    // (IslandDrawing.ProjectionLook). default = no projections.
    public readonly struct WaterProjectionLook
    {
        public readonly Sprite dark;
        public readonly Sprite light;
        public readonly Material material;
        public readonly int sortingLayerId;
        public readonly int darkOrder;
        public readonly int lightOrder;

        public WaterProjectionLook(Sprite dark, Sprite light, Material material, int sortingLayerId, int darkOrder, int lightOrder)
        {
            this.dark = dark;
            this.light = light;
            this.material = material;
            this.sortingLayerId = sortingLayerId;
            this.darkOrder = darkOrder;
            this.lightOrder = lightOrder;
        }
    }

    // What a structure throws on the water in front of it: a dark block as wide as its footprint's box (a
    // market's empty middle included), a light one over it, a little narrower at each side, both fading out to
    // the south. Not a mirror image: the art is a plain gradient stretched across, the same for every structure.
    //
    // The blocks hang from the root, which stands on the footprint's bottom edge (StructureSystem.AnchorOnFootprint);
    // the art's top-centre pivot is what hangs them. They sort under the island's layers, so land to the south
    // simply covers them: away from the coast nothing shows, and a zone coming up in front of a house covers its
    // projection tile by tile with no code here. Children of the root, not of Visual: a bump's squash must not
    // stretch them, and a structure's ride on the land during a rise carries them along.
    //
    // Only StructureSystem calls Show, on a structure it really places. So the placement ghost and a site's
    // blueprint, which are copies of the prefab, never have one.
    [DisallowMultipleComponent]
    public class WaterProjection : MonoBehaviour
    {
        [Tooltip("How many art pixels narrower the light block is than the dark one at EACH side. Read once, when " +
                 "the structure is placed.")]
        [SerializeField, Min(0)] private int lightInsetPixels = 2;

        private SpriteRenderer darkBlock;
        private SpriteRenderer lightBlock;

        public static float LightWidth(float width, int insetPixels, float pixelsPerUnit) =>
            Mathf.Max(0f, width - 2f * insetPixels / pixelsPerUnit);

        // True for a renderer that is somebody's projection — PlacementVisuals keeps those out of its tints.
        public static bool IsProjection(SpriteRenderer renderer)
        {
            var parent = renderer.transform.parent;
            return parent != null && parent.TryGetComponent<WaterProjection>(out var owner) &&
                   (owner.darkBlock == renderer || owner.lightBlock == renderer);
        }

        // Put the blocks up, `width` world units across. Without the look's art, none.
        public void Show(in WaterProjectionLook look, float width)
        {
            darkBlock = Block("Water Projection Dark", look.dark, look, look.darkOrder, width);
            float lightWidth = look.light != null ? LightWidth(width, lightInsetPixels, look.light.pixelsPerUnit) : 0f;
            lightBlock = Block("Water Projection Light", look.light, look, look.lightOrder, lightWidth);
        }

        private SpriteRenderer Block(string blockName, Sprite sprite, in WaterProjectionLook look, int order, float width)
        {
            if (sprite == null) return null;

            var go = new GameObject(blockName);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(width / sprite.bounds.size.x, 1f, 1f);
            var block = go.AddComponent<SpriteRenderer>();
            block.sprite = sprite;
            if (look.material != null) block.sharedMaterial = look.material;
            block.sortingLayerID = look.sortingLayerId;
            block.sortingOrder = order;
            return block;
        }
    }
}
