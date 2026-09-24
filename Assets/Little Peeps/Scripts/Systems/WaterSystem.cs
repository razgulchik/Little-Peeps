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
    // Side foam: that band cannot reach the east and west edges — the package's shader only looks upward
    // for the coast. So the sides get their own: two flat-coloured twins of every coast tilemap, one shifted
    // a few art pixels left and one right, drawn just under the island. The island hides them everywhere
    // except a strip along its vertical edges. Same colour and alpha as the band, read from the prefab.
    //
    // Wire: waterPrefab → the water prefab; coastTilemaps → Ground and GroundTrim (the trim draws the
    // coastline on water cells, so it is part of the coast the ripples meet); sideFoamShader → the
    // "Little Peeps/Sprite Flat Color" shader.
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

        [Header("Side foam")]
        [Tooltip("The \"Little Peeps/Sprite Flat Color\" shader. Empty = no foam along the sides.")]
        [SerializeField] private Shader sideFoamShader;
        [Tooltip("Width of the foam along the island's east and west edges, in art pixels. 0 = none.")]
        [Min(0)] [SerializeField] private int sideFoamPixels = 1;

        // The art's pixels per unit (the Pixel Perfect Camera's Assets PPU).
        private const float ArtPixelsPerUnit = 16f;

        private static readonly int ObstructionWidthId = Shader.PropertyToID("_obstruction_width");
        private static readonly int ObstructionTransformId = Shader.PropertyToID("_obs_transform");

        private ModernWater2D water;
        private MaterialPropertyBlock foamBlock;
        private ObstructorTilemap[] coast;   // null until Start

        // Two twins per coast tilemap: [2i] shifted left and [2i + 1] shifted right of coastTilemaps[i].
        private Tilemap[] sideFoam;          // null until Start, and without a shader
        private Material sideFoamMaterial;
        private int sideFoamPlaced = -1;     // the width the twins are shifted by right now

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

            // Ripples off. The package follows a camera PAN inside its ripple field but never rescales it on
            // a ZOOM, so every zoom step shifts the whole coast inside the field and it rings with waves —
            // and nothing in the game moves in the water to need them. (The island rise was to switch them
            // on for itself; it plays at timeScale 0, where the simulation, stepped in FixedUpdate, cannot
            // run.) Off here, not in the prefab: the package only builds the ripple field if it is on when
            // the water enables, so a prefab with it on keeps the way back open.
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

            CreateSideFoam();
            CopySideFoamTiles();
        }

        private void OnDestroy()
        {
            if (sideFoamMaterial != null) Destroy(sideFoamMaterial);
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

            CopySideFoamTiles();
        }

        private void LateUpdate()
        {
            if (water == null) return;
            PulseCoastFoam();
            UpdateSideFoam();
        }

        // Whether there is a sea at all — without one, nothing needs to push on it.
        public bool HasWater => water != null;

        // Through a property block on the water's renderer, not the package's width setting: every change
        // of a setting re-uploads the whole material and searches the scene for waters — once a frame is
        // too much. The package puts no property block on its own renderer, so nothing overwrites this one.
        private void PulseCoastFoam()
        {
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

        // The twins sit beside their tilemaps under the same Grid, so they share its cells, and sort one step
        // below the lowest coast layer: under the whole island, above the water.
        private void CreateSideFoam()
        {
            if (sideFoamShader == null) return;

            TilemapRenderer lowest = null;
            foreach (var tilemap in coastTilemaps)
            {
                var tilemapRenderer = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;
                if (tilemapRenderer != null && (lowest == null || SortsBelow(tilemapRenderer, lowest))) lowest = tilemapRenderer;
            }
            if (lowest == null) return;

            sideFoamMaterial = new Material(sideFoamShader);
            sideFoam = new Tilemap[coastTilemaps.Length * 2];
            for (int i = 0; i < coastTilemaps.Length; i++)
            {
                var source = coastTilemaps[i];
                if (source == null) continue;
                for (int side = 0; side < 2; side++)
                {
                    var twin = new GameObject($"{source.name} side foam {(side == 0 ? "left" : "right")}");
                    twin.transform.SetParent(source.transform.parent, false);
                    var tilemap = twin.AddComponent<Tilemap>();
                    tilemap.tileAnchor = source.tileAnchor;
                    var tilemapRenderer = twin.AddComponent<TilemapRenderer>();
                    tilemapRenderer.sharedMaterial = sideFoamMaterial;
                    tilemapRenderer.sortingLayerID = lowest.sortingLayerID;
                    tilemapRenderer.sortingOrder = lowest.sortingOrder - 1;
                    sideFoam[i * 2 + side] = tilemap;
                }
            }
        }

        private static bool SortsBelow(Renderer a, Renderer b)
        {
            int layerA = SortingLayer.GetLayerValueFromID(a.sortingLayerID);
            int layerB = SortingLayer.GetLayerValueFromID(b.sortingLayerID);
            return layerA != layerB ? layerA < layerB : a.sortingOrder < b.sortingOrder;
        }

        private void CopySideFoamTiles()
        {
            if (sideFoam == null) return;
            for (int i = 0; i < coastTilemaps.Length; i++)
            {
                var source = coastTilemaps[i];
                if (source == null) continue;
                var bounds = source.cellBounds;
                var tiles = source.GetTilesBlock(bounds);
                for (int side = 0; side < 2; side++)
                {
                    var twin = sideFoam[i * 2 + side];
                    twin.ClearAllTiles();
                    twin.SetTilesBlock(bounds, tiles);
                }
            }
        }

        // Colour every frame (cheap: our own material) so tuning the prefab's obstruction colour in Play Mode
        // shows on the sides too; the shift only when the width changes.
        private void UpdateSideFoam()
        {
            if (sideFoam == null) return;

            var settings = water.settings._waterSettings;
            Color colour = settings.obstructionColor.value;
            colour.a *= settings.obstructionAlpha.value;
            sideFoamMaterial.color = colour;

            if (sideFoamPixels == sideFoamPlaced) return;
            sideFoamPlaced = sideFoamPixels;
            for (int i = 0; i < sideFoam.Length; i++)
            {
                var twin = sideFoam[i];
                if (twin == null) continue;
                float shift = (i % 2 == 0 ? -1f : 1f) * sideFoamPixels / ArtPixelsPerUnit;
                twin.transform.localPosition = coastTilemaps[i / 2].transform.localPosition + new Vector3(shift, 0f, 0f);
                twin.gameObject.SetActive(sideFoamPixels > 0);
            }
        }
    }
}
