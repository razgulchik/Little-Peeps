using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The build grid as a SPOT around the cursor, shown while a build card or the sell tool is in hand:
    // the lines of the island's cells within spotRadius of the point, fading out toward the rim, and the
    // faint fill over occupied cells (each structure's claimed territory, the otherwise invisible border
    // of a house included) in the same fade. The rest of the island stays clean — building happens in the
    // running game, with the panel always up, and a grid over everything would sit on top of the play.
    //
    // Rebuilt every frame it is shown: a few dozen cells, so it always matches the grid without being
    // told about placements, sales or a site turning into its building. Lines are THIN QUADS (triangles),
    // not MeshTopology.Lines — line primitives are unreliable under URP 2D (often invisible with MSAA,
    // always 1px); quads rasterize everywhere, give a controllable width, and sort like any sprite. Each
    // cell side is cut into a few segments so the fade reads as a circle, not as cell-sized steps. One
    // draw call. Driven by PlacementController (Track / Hide). Keep this GameObject at world origin
    // (0,0,0) with no rotation/scale — vertices are built in world space.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GridOverlay : MonoBehaviour
    {
        [SerializeField] private IslandSystem islandSystem;
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color occupiedColor = new Color(1f, 1f, 1f, 0.12f);   // faint fill over cells a structure occupies (its territory)
        [SerializeField] private float lineWidth = 0.04f;              // world units
        [SerializeField] private string sortingLayerName = "Ground";  // MUST be in front of the island's tiles (the Island layer, below Ground) and what lies on the land
        [SerializeField] private int sortingOrder = 1000;             // and order within that layer (above the fields and stumps lying on it)

        [Header("Spot")]
        [Tooltip("Radius of the spot around the cursor, in cells.")]
        [SerializeField, Min(0.5f)] private float spotRadius = 3f;

        [Tooltip("Share of the radius drawn at full strength; the rest fades out to the rim.")]
        [SerializeField, Range(0f, 1f)] private float spotSolid = 0.35f;

        [Tooltip("Segments per cell side. More = a rounder fade, at a few more vertices.")]
        [SerializeField, Range(1, 8)] private int segmentsPerSide = 4;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;

        // Reused every frame the spot is shown — rebuilding it allocates nothing.
        private readonly List<Vector3> verts = new();
        private readonly List<Color> colors = new();
        private readonly List<int> tris = new();
        private readonly HashSet<(Vector2Int, Vector2Int)> edges = new();

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            // Vertex-colored, unlit, alpha-blended shader — renders correctly in URP 2D. Created in
            // code on purpose: a serialized material is easy to mis-assign (a Standard-shader material
            // shows up as magenta / invisible in URP), and the lines only need vertex color + lineColor.
            meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;

            mesh = new Mesh { name = "GridOverlay" };
            mesh.MarkDynamic();
            meshFilter.mesh = mesh;
            meshRenderer.enabled = false;
        }

        // Show the spot around `center` (world), rebuilt for where it is now. Called every frame the tool
        // is in hand.
        public void Track(Vector2 center)
        {
            if (islandSystem == null) { Debug.LogError("[GridOverlay] islandSystem not assigned", this); return; }

            Rebuild(center);
            meshRenderer.enabled = true;
        }

        public void Hide()
        {
            if (meshRenderer != null) meshRenderer.enabled = false;
        }

        private void Rebuild(Vector2 center)
        {
            var grid = islandSystem.Grid;
            if (grid == null) return;

            float cs = grid.CellSize;
            float outer = spotRadius * cs;
            float inner = outer * spotSolid;
            int reach = Mathf.CeilToInt(spotRadius) + 1;
            Vector2Int middle = grid.WorldToGrid(center);

            verts.Clear();
            colors.Clear();
            tris.Clear();
            edges.Clear();

            // Territory fills first (so the grid lines draw on top of them), then the sides of every cell in
            // reach — collected as lattice-point pairs (a cell's corner (lx,ly) is at world (lx*cs, ly*cs))
            // and deduplicated, so a side two cells share is not drawn twice.
            for (int dx = -reach; dx <= reach; dx++)
                for (int dy = -reach; dy <= reach; dy++)
                {
                    var c = new Vector2Int(middle.x + dx, middle.y + dy);
                    var cell = grid.GetCell(c);
                    if (cell == null) continue;

                    Vector2 min = new Vector2(c.x * cs, c.y * cs);
                    Vector2 max = min + new Vector2(cs, cs);
                    if (DistanceToBox(center, min, max) >= outer) continue;   // wholly outside the spot

                    if (cell.occupant != null) AddFill(min, max, center, inner, outer);

                    var bl = new Vector2Int(c.x, c.y);
                    var br = new Vector2Int(c.x + 1, c.y);
                    var tl = new Vector2Int(c.x, c.y + 1);
                    var tr = new Vector2Int(c.x + 1, c.y + 1);
                    AddEdge(bl, br);
                    AddEdge(tl, tr);
                    AddEdge(bl, tl);
                    AddEdge(br, tr);
                }

            float half = lineWidth * 0.5f;
            foreach (var (a, b) in edges)
            {
                Vector2 wa = new Vector2(a.x * cs, a.y * cs);
                Vector2 wb = new Vector2(b.x * cs, b.y * cs);
                Vector2 dir = (wb - wa).normalized;
                Vector2 n = new Vector2(-dir.y, dir.x) * half;   // perpendicular half-width

                for (int s = 0; s < segmentsPerSide; s++)
                {
                    Vector2 p = Vector2.Lerp(wa, wb, (float)s / segmentsPerSide);
                    Vector2 q = Vector2.Lerp(wa, wb, (float)(s + 1) / segmentsPerSide);
                    float fp = Fade(p, center, inner, outer);
                    float fq = Fade(q, center, inner, outer);
                    if (fp <= 0f && fq <= 0f) continue;   // this piece is past the rim

                    int i = verts.Count;
                    verts.Add(new Vector3(p.x - n.x, p.y - n.y, 0f));
                    verts.Add(new Vector3(p.x + n.x, p.y + n.y, 0f));
                    verts.Add(new Vector3(q.x + n.x, q.y + n.y, 0f));
                    verts.Add(new Vector3(q.x - n.x, q.y - n.y, 0f));
                    colors.Add(Faded(lineColor, fp));
                    colors.Add(Faded(lineColor, fp));
                    colors.Add(Faded(lineColor, fq));
                    colors.Add(Faded(lineColor, fq));
                    tris.Add(i + 0); tris.Add(i + 1); tris.Add(i + 2);
                    tris.Add(i + 0); tris.Add(i + 2); tris.Add(i + 3);
                }
            }

            mesh.Clear();
            mesh.indexFormat = verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
        }

        // 1 inside `inner`, 0 from `outer` out, a smooth fall between.
        private static float Fade(Vector2 p, Vector2 center, float inner, float outer)
        {
            float d = Vector2.Distance(p, center);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, d));
        }

        private static Color Faded(Color color, float fade) => new Color(color.r, color.g, color.b, color.a * fade);

        private static float DistanceToBox(Vector2 p, Vector2 min, Vector2 max)
        {
            float dx = Mathf.Max(min.x - p.x, 0f, p.x - max.x);
            float dy = Mathf.Max(min.y - p.y, 0f, p.y - max.y);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private void AddEdge(Vector2Int p, Vector2Int q)
        {
            // Normalize endpoint order so a shared side maps to one entry regardless of which cell adds it.
            if (q.x < p.x || (q.x == p.x && q.y < p.y)) (p, q) = (q, p);
            edges.Add((p, q));
        }

        // An occupied cell's fill, each corner faded by its own distance from the centre.
        private void AddFill(Vector2 a, Vector2 b, Vector2 center, float inner, float outer)
        {
            int i = verts.Count;
            Vector2 c0 = new Vector2(a.x, a.y), c1 = new Vector2(a.x, b.y), c2 = new Vector2(b.x, b.y), c3 = new Vector2(b.x, a.y);
            verts.Add(c0); verts.Add(c1); verts.Add(c2); verts.Add(c3);
            colors.Add(Faded(occupiedColor, Fade(c0, center, inner, outer)));
            colors.Add(Faded(occupiedColor, Fade(c1, center, inner, outer)));
            colors.Add(Faded(occupiedColor, Fade(c2, center, inner, outer)));
            colors.Add(Faded(occupiedColor, Fade(c3, center, inner, outer)));
            tris.Add(i + 0); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i + 0); tris.Add(i + 2); tris.Add(i + 3);
        }
    }
}
