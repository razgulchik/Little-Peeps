using UnityEngine;

namespace LittlePeeps
{
    // A drop shadow for any sprite: a black, translucent copy of this object's SpriteRenderer, drawn one
    // sorting step behind it and shifted by a few pixels. Nothing is authored by hand — the shadow object
    // is CREATED at Start from the renderer it sits next to, so the model's sprite is the single source of
    // truth: swap the art and the shadow follows. Every frame it re-copies what shapes the silhouette
    // (sprite, flip, sorting), so an animated unit or a y-sorted sprite keeps a matching shadow for free.
    //
    // Created at Start, on purpose: the build-mode ghost is the same prefab with every behaviour disabled
    // (PlacementVisuals), and a disabled behaviour never Starts — so a ghost has no shadow by construction
    // and the placement code never has to strip one. The rule the player sees is "a shadow belongs to
    // what stands on the ground": a structure carried by the Move tool hides its shadow through
    // SetVisible until it lands, and PlacementVisuals leaves the shadow's renderer out of its hover/drag
    // tints, so the building goes green or red and the shadow stays black.
    //
    // Generic — buildings, animals and units alike; it needs nothing but a SpriteRenderer on the same
    // object. Height is not modelled: the shadow is a silhouette offset, not a ground contact point, so
    // a launched unit's shadow flies with it.
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteShadow : MonoBehaviour
    {
        [Tooltip("Off = no shadow object is created. Read once at Start.")]
        [SerializeField] private bool castShadow = true;

        [Tooltip("Where the shadow sits relative to the sprite, in pixels of THIS sprite (+y is up).")]
        [SerializeField] private Vector2Int offsetPixels = new(0, 1);

        [Tooltip("Opacity of the black silhouette.")]
        [SerializeField, Range(0f, 1f)] private float alpha = 35f / 255f;

        private SpriteRenderer source;
        private SpriteRenderer shadow;   // null until Start, and forever when castShadow is off

        // True for a renderer that is somebody's shadow — PlacementVisuals keeps those out of its tints.
        // Answered by the parent's SpriteShadow, so the "shadow is a child of its source" layout stays
        // this class's business.
        public static bool IsShadow(SpriteRenderer renderer)
        {
            var parent = renderer.transform.parent;
            return parent != null && parent.TryGetComponent<SpriteShadow>(out var owner) && owner.shadow == renderer;
        }

        private void Start()
        {
            if (!castShadow) { enabled = false; return; }

            source = GetComponent<SpriteRenderer>();
            var go = new GameObject("Shadow");
            go.transform.SetParent(transform, false);
            shadow = go.AddComponent<SpriteRenderer>();
            shadow.sharedMaterial = source.sharedMaterial;
            shadow.drawMode = source.drawMode;
            if (source.drawMode != SpriteDrawMode.Simple) shadow.size = source.size;

            ApplyLook();
            Sync();
        }

        // Guarded: the component can be switched on from outside after a Start that made no shadow.
        private void LateUpdate()
        {
            if (shadow != null) Sync();
        }

        // Inspector tweaks while playing land on the live shadow; in edit mode there is none yet.
        private void OnValidate()
        {
            if (shadow != null) ApplyLook();
        }

        // Show or hide the shadow without destroying it — the Move tool hides it while the structure is
        // carried and shows it again on drop. No-op when there is no shadow.
        public void SetVisible(bool visible)
        {
            if (shadow != null) shadow.gameObject.SetActive(visible);
        }

        // The shadow's own look: colour and offset. Pixels become local units through the sprite's PPU,
        // so "1 px" is one texel of this art whatever the import settings say.
        private void ApplyLook()
        {
            shadow.color = new Color(0f, 0f, 0f, alpha);
            float ppu = source.sprite != null ? source.sprite.pixelsPerUnit : 1f;
            shadow.transform.localPosition = new Vector3(offsetPixels.x / ppu, offsetPixels.y / ppu, 0f);
        }

        // Copy what shapes the silhouette and where it sorts. The sprite is compared first — assigning
        // it re-uploads the renderer even when nothing changed; the rest are plain field writes.
        private void Sync()
        {
            if (shadow.sprite != source.sprite) shadow.sprite = source.sprite;
            shadow.flipX = source.flipX;
            shadow.flipY = source.flipY;
            shadow.sortingLayerID = source.sortingLayerID;
            shadow.sortingOrder = source.sortingOrder - 1;
        }
    }
}
