using UnityEngine;

namespace LittlePeeps
{
    // The sea around the island: one quad in the "Little Peeps/Water" shader — its colour and the flow strokes,
    // each stroke living on its own by a map of the tile made here (WaterFlowStrokes). What lies on the sea along
    // the coast (waves, the island's shadow, the structures' projections) is the island's: IslandDrawing.
    //
    // Wire: waterMaterial → M_Water.
    public class WaterSystem : MonoBehaviour
    {
        [Tooltip("M_Water (the \"Little Peeps/Water\" shader): the sea's colour and its flow strokes.")]
        [SerializeField] private Material waterMaterial;

        // The art's pixels per unit (the Pixel Perfect Camera's Assets PPU).
        private const float ArtPixelsPerUnit = 16f;

        private static readonly int ArtPixelsPerUnitId = Shader.PropertyToID("_ArtPixelsPerUnit");
        private static readonly int FlowTexId = Shader.PropertyToID("_FlowTex");
        private static readonly int FlowStrokesId = Shader.PropertyToID("_FlowStrokes");
        private static readonly int FlowStrokesSizeId = Shader.PropertyToID("_FlowStrokesSize");

        // Our sea sorts at the bottom of this layer: under the gradient canvas (Water, order 1) and the island.
        private const string WaterSortingLayer = "Water";
        // Edge of the cube our sea's mesh claims to fill, in world units. Its vertices are the screen's corners,
        // not places in the world, so its bounds only have to hold every view the camera can take — far past
        // any island — or the quad is culled as soon as the camera leaves its spot.
        private const float SeaBoundsSize = 100000f;

        private Mesh seaMesh;                // null without a water material
        private Texture2D flowStrokes;       // its strokes' map; null without a readable tile

        private void Awake() => CreateSea();

        // Our sea. The shader puts the quad's corners on the screen's corners, so it covers the whole frame
        // wherever the camera is: nothing follows the camera, and nothing is a frame behind a pan.
        private void CreateSea()
        {
            if (waterMaterial == null)
            {
                Debug.LogWarning("WaterSystem: no water material assigned — the island stands on the camera's background.", this);
                return;
            }

            seaMesh = new Mesh { name = "Sea" };
            seaMesh.SetVertices(new[]
            {
                new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f)
            });
            seaMesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            seaMesh.bounds = new Bounds(Vector3.zero, Vector3.one * SeaBoundsSize);

            var sea = new GameObject("Sea");
            sea.transform.SetParent(transform, false);
            sea.AddComponent<MeshFilter>().sharedMesh = seaMesh;
            var seaRenderer = sea.AddComponent<MeshRenderer>();
            seaRenderer.sharedMaterial = waterMaterial;
            seaRenderer.sortingLayerID = SortingLayer.NameToID(WaterSortingLayer);
            seaRenderer.sortingOrder = 0;

            // The art's grid from here, not from a second copy in the material.
            var block = new MaterialPropertyBlock();
            block.SetFloat(ArtPixelsPerUnitId, ArtPixelsPerUnit);
            SetFlowStrokes(block);
            seaRenderer.SetPropertyBlock(block);
        }

        // The map that lets each stroke of the material's tile live on its own (WaterFlowStrokes), read from the
        // tile once, here: a tile put on the material later in Play Mode is not seen until the next run. Without
        // the map the shader draws no strokes, so a tile it cannot read says so.
        private void SetFlowStrokes(MaterialPropertyBlock block)
        {
            if (waterMaterial.GetTexture(FlowTexId) is not Texture2D tile) return;
            if (!tile.isReadable)
            {
                Debug.LogWarning($"WaterSystem: the flow strokes '{tile.name}' cannot be read — turn on Read/Write in " +
                                 "its import settings. The sea has no strokes.", tile);
                return;
            }

            flowStrokes = WaterFlowStrokes.CreateTexture(tile);
            block.SetTexture(FlowStrokesId, flowStrokes);
            block.SetVector(FlowStrokesSizeId, new Vector4(flowStrokes.width, flowStrokes.height, 0f, 0f));
        }

        private void OnDestroy()
        {
            if (seaMesh != null) Destroy(seaMesh);
            if (flowStrokes != null) Destroy(flowStrokes);
        }
    }
}
