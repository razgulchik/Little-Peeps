using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Draws the zones on offer onto the world while the player chooses: every candidate as a tinted
    // block of cells in its biome's colour with an outline, the one under the cursor brighter and on
    // top. One procedural mesh of quads, the way GridOverlay draws the build grid and for the same
    // reasons (line primitives are unreliable under URP 2D; quads sort like sprites). Shown and driven
    // by ZoneSelectionUI, which is the only thing that knows when offers are up.
    //
    // The candidates are not island yet — they sit on water — so nothing here reads the grid beyond
    // its cell size. Keep this GameObject at the world origin with no rotation or scale: vertices are
    // built in world space.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ZonePreviewOverlay : MonoBehaviour
    {
        [SerializeField] private IslandSystem islandSystem;

        [Header("Look")]
        [Tooltip("Alpha of a zone's fill. Its colour is the biome's previewColor.")]
        [Range(0f, 1f)] [SerializeField] private float fillAlpha = 0.25f;
        [Range(0f, 1f)] [SerializeField] private float outlineAlpha = 0.7f;
        [Tooltip("The zone whose card the cursor is on.")]
        [Range(0f, 1f)] [SerializeField] private float highlightedFillAlpha = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float highlightedOutlineAlpha = 1f;
        [Tooltip("Width of the outline, in world units, drawn inside the zone's edge cells.")]
        [Min(0f)] [SerializeField] private float outlineWidth = 0.08f;

        [Header("Sorting")]
        [SerializeField] private string sortingLayerName = "Ground";
        [SerializeField] private int sortingOrder = 1001;   // above the tilemap and the build grid

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;

        private IReadOnlyList<ZoneOffer> offers;
        private ZoneOffer highlighted;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            // Vertex-coloured, unlit, alpha-blended — see GridOverlay for why the material is made here.
            meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;

            mesh = new Mesh { name = "ZonePreviewOverlay" };
            meshFilter.mesh = mesh;
            meshRenderer.enabled = false;
        }

        public void Show(IReadOnlyList<ZoneOffer> offers)
        {
            this.offers = offers;
            highlighted = null;
            Rebuild();
            meshRenderer.enabled = true;
        }

        // Bring one offer forward (null = none). Cheap enough to rebuild the whole mesh: a few hundred
        // cells, on hover only.
        public void Highlight(ZoneOffer offer)
        {
            if (highlighted == offer) return;
            highlighted = offer;
            if (meshRenderer.enabled) Rebuild();
        }

        public void Hide()
        {
            meshRenderer.enabled = false;
            offers = null;
            highlighted = null;
        }

        // World point at the middle of a zone's cells — where the camera looks when its card is hovered.
        public Vector2 WorldCentre(ZoneOffer offer)
        {
            float cs = CellSize();
            var sum = Vector2.zero;
            foreach (var c in offer.Candidate.Cells) sum += new Vector2(c.x + 0.5f, c.y + 0.5f);
            return sum / Mathf.Max(1, offer.Candidate.Cells.Count) * cs;
        }

        // World box around every cell of every offer, for widening the camera clamp. A zero box for none.
        public Bounds WorldBounds(IReadOnlyList<ZoneOffer> offers)
        {
            float cs = CellSize();
            bool any = false;
            var b = new Bounds();
            foreach (var offer in offers)
                foreach (var c in offer.Candidate.Cells)
                {
                    var min = new Vector3(c.x * cs, c.y * cs, 0f);
                    var max = new Vector3((c.x + 1) * cs, (c.y + 1) * cs, 0f);
                    if (!any) { b.SetMinMax(min, max); any = true; }
                    else { b.Encapsulate(min); b.Encapsulate(max); }
                }
            return b;
        }

        private float CellSize()
        {
            var grid = islandSystem != null ? islandSystem.Grid : null;
            return grid != null ? grid.CellSize : 1f;
        }

        // Every offer's fill, then every offer's outline, the highlighted one last in each pass so it
        // draws over the others where two offers overlap (they may — each is valid alone).
        private void Rebuild()
        {
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();

            if (offers != null)
            {
                float cs = CellSize();
                foreach (var offer in DrawOrder())
                    AddFill(offer, cs, verts, colors, tris);
                foreach (var offer in DrawOrder())
                    AddOutline(offer, cs, verts, colors, tris);
            }

            mesh.Clear();
            mesh.indexFormat = verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
        }

        private IEnumerable<ZoneOffer> DrawOrder()
        {
            foreach (var offer in offers) if (offer != highlighted) yield return offer;
            if (highlighted != null) yield return highlighted;
        }

        private Color Tint(ZoneOffer offer, float alpha)
        {
            var c = offer.Biome != null ? offer.Biome.previewColor : Color.white;
            c.a = alpha;
            return c;
        }

        private void AddFill(ZoneOffer offer, float cs, List<Vector3> verts, List<Color> colors, List<int> tris)
        {
            var color = Tint(offer, offer == highlighted ? highlightedFillAlpha : fillAlpha);
            foreach (var c in offer.Candidate.Cells)
                AddQuad(verts, colors, tris, new Vector2(c.x * cs, c.y * cs), new Vector2((c.x + 1) * cs, (c.y + 1) * cs), color);
        }

        // A strip just inside every cell edge that has no zone cell across it. Inside, so the outline
        // never spills onto the island or a neighbouring offer.
        private void AddOutline(ZoneOffer offer, float cs, List<Vector3> verts, List<Color> colors, List<int> tris)
        {
            if (outlineWidth <= 0f) return;
            var color = Tint(offer, offer == highlighted ? highlightedOutlineAlpha : outlineAlpha);
            float w = Mathf.Min(outlineWidth, cs);
            var candidate = offer.Candidate;

            foreach (var c in candidate.Cells)
            {
                float x0 = c.x * cs, y0 = c.y * cs, x1 = (c.x + 1) * cs, y1 = (c.y + 1) * cs;
                if (!candidate.Contains(c + Vector2Int.left))  AddQuad(verts, colors, tris, new Vector2(x0, y0), new Vector2(x0 + w, y1), color);
                if (!candidate.Contains(c + Vector2Int.right)) AddQuad(verts, colors, tris, new Vector2(x1 - w, y0), new Vector2(x1, y1), color);
                if (!candidate.Contains(c + Vector2Int.down))  AddQuad(verts, colors, tris, new Vector2(x0, y0), new Vector2(x1, y0 + w), color);
                if (!candidate.Contains(c + Vector2Int.up))    AddQuad(verts, colors, tris, new Vector2(x0, y1 - w), new Vector2(x1, y1), color);
            }
        }

        // Append an axis-aligned quad spanning bottom-left `a` to top-right `b`, all one vertex colour.
        private static void AddQuad(List<Vector3> verts, List<Color> colors, List<int> tris, Vector2 a, Vector2 b, Color color)
        {
            int idx = verts.Count;
            verts.Add(new Vector3(a.x, a.y, 0f));
            verts.Add(new Vector3(a.x, b.y, 0f));
            verts.Add(new Vector3(b.x, b.y, 0f));
            verts.Add(new Vector3(b.x, a.y, 0f));
            for (int i = 0; i < 4; i++) colors.Add(color);
            tris.Add(idx + 0); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx + 0); tris.Add(idx + 2); tris.Add(idx + 3);
        }
    }
}
