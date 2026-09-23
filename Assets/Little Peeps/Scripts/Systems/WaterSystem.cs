using UnityEngine;
using UnityEngine.Tilemaps;
using Water2D;

namespace LittlePeeps
{
    // The sea around the island: Modern 2D Water spawned from a prefab at run time, plus the island's
    // outline in the water's obstruction layer so ripples break on the coast instead of running under it.
    //
    // Why a runtime prefab and not a scene object: the package runs in edit mode ([ExecuteAlways]) — a
    // water placed in the scene embeds a fresh Material into SampleScene on every enable and parks hidden
    // manager cameras there, so every scene save would churn against the art branch. Spawned from here,
    // none of that reaches the scene file; the prefab holds the whole look.
    //
    // Coast: ObstructorTilemap copies a tilemap's tiles into a hidden twin on the Obstructors layer — but
    // only once, and it only ever adds. Our island is painted at run time and repainted on every
    // expansion, so the components are added here (not in the scene, same churn reason) and each twin is
    // rebuilt from scratch after every repaint.
    //
    // Coast foam: the package's coast outline (its obstruction colour — a band under the island's south
    // edges) made to breathe. The package sizes that band as a share of the obstruction texture, i.e. of
    // the SCREEN, so a fixed width grows and shrinks in the world with every zoom. Here it is set every
    // frame from a thickness in art pixels instead: the band stays the same in the world at any zoom and
    // pulses between two thicknesses. Colour and alpha stay in the prefab; its width field is overridden.
    //
    // Wire: waterPrefab → the water prefab; coastTilemaps → Ground and GroundTrim (the trim draws the
    // coastline on water cells, so it is part of the coast the ripples meet).
    public class WaterSystem : MonoBehaviour
    {
        [SerializeField] private ModernWater2D waterPrefab;
        [SerializeField] private Tilemap[] coastTilemaps;

        [Header("Coast foam")]
        [Tooltip("Thickness of the foam under the south coast at the low point of its pulse, in art pixels. " +
                 "Replaces the width field of the water prefab's obstruction settings.")]
        [SerializeField] private float foamMinPixels = 1f;
        [Tooltip("Thickness at the high point of the pulse, in art pixels. Equal to the minimum = still foam.")]
        [SerializeField] private float foamMaxPixels = 3f;
        [Tooltip("Seconds for one full pulse, out and back. Real time: the foam keeps pulsing on pauses.")]
        [SerializeField] private float foamPeriod = 4f;
        [Tooltip("On: the foam steps a whole art pixel at a time. Off: it slides by screen pixels.")]
        [SerializeField] private bool foamWholePixels = true;

        // The art's pixels per unit (the Pixel Perfect Camera's Assets PPU).
        private const float ArtPixelsPerUnit = 16f;

        private static readonly int ObstructionWidthId = Shader.PropertyToID("_obstruction_width");
        private static readonly int ObstructionTransformId = Shader.PropertyToID("_obs_transform");

        private ModernWater2D water;
        private MaterialPropertyBlock foamBlock;
        private ObstructorTilemap[] coast;   // null until Start

        private void OnEnable()  => EventBus<IslandRepaintedEvent>.Subscribe(OnIslandRepainted);
        private void OnDisable() => EventBus<IslandRepaintedEvent>.Unsubscribe(OnIslandRepainted);

        // The water in Awake: its reflection system must exist before any content Starts. The first
        // island's trees and houses are instantiated in GameBootstrap.Awake, and a Reflector reaches for
        // that system in its own Start — without it, a NullReferenceException per reflecting object.
        // Every Awake still runs before any Start, whichever of the two Awakes goes first.
        private void Awake()
        {
            if (waterPrefab == null)
            {
                Debug.LogWarning("WaterSystem: no water prefab assigned — the island stands on the camera's background.", this);
                return;
            }
            water = Instantiate(waterPrefab);
            foamBlock = new MaterialPropertyBlock();

            // Ripples off outside the island rise, which turns them on for its own duration. The package
            // follows a camera PAN inside its ripple field but never rescales it on a ZOOM, so every zoom
            // step shifts the whole coast inside the field and it rings with waves — and in normal play
            // nothing moves in the water to need them. Off here, not in the prefab: the package only
            // builds the ripple field if it is on when the water enables, and could not turn it on later.
            water.enableSimulation.value = false;
        }

        // The coast in Start: the first island is painted in GameBootstrap.Awake, which is only certain
        // to be over here — the ObstructorTilemaps copy it as they enable.
        private void Start()
        {
            if (waterPrefab == null) return;

            coast = new ObstructorTilemap[coastTilemaps.Length];
            for (int i = 0; i < coastTilemaps.Length; i++)
                if (coastTilemaps[i] != null)
                    coast[i] = coastTilemaps[i].gameObject.AddComponent<ObstructorTilemap>();
        }

        // A repaint before Start (the first island) needs nothing: Start's copies are taken after it.
        private void OnIslandRepainted(IslandRepaintedEvent _)
        {
            if (coast == null) return;

            foreach (var twin in coast)
            {
                if (twin == null) continue;
                if (twin.obstructor != null)
                    twin.obstructor.GetComponent<Tilemap>().ClearAllTiles();   // CreateData only ever adds
                twin.CreateData();
            }
        }

        // Through a property block on the water's renderer, not the package's width setting: every change
        // of a setting re-uploads the whole material and searches the scene for waters — once a frame is
        // too much. The package puts no property block on its own renderer, so nothing overwrites this one.
        private void LateUpdate()
        {
            if (water == null) return;
            var waterRenderer = water.ActiveRenderer;
            var camera = Camera.main;
            if (waterRenderer == null || camera == null || !camera.orthographic) return;

            float pulse = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * Time.unscaledTime / Mathf.Max(0.01f, foamPeriod));
            float pixels = Mathf.Lerp(foamMinPixels, foamMaxPixels, pulse);
            if (foamWholePixels) pixels = Mathf.Round(pixels);

            // The shader shifts its sample by width / 50 of the obstruction texture's height. That texture
            // spans the view plus the package's margins; _obs_transform.y is the view's share of it.
            var material = water.ActiveSharedMaterial;
            float viewShare = material != null ? material.GetVector(ObstructionTransformId).y : 1f;
            float width = 50f * (pixels / ArtPixelsPerUnit) * viewShare / (2f * camera.orthographicSize);

            waterRenderer.GetPropertyBlock(foamBlock);
            foamBlock.SetFloat(ObstructionWidthId, width);
            waterRenderer.SetPropertyBlock(foamBlock);
        }
    }
}
