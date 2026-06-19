using System.Collections.Generic;
using UnityEngine;

// Build-mode grid overlay: one procedural mesh outlining every existing cell of the sparse
// IslandGrid. Lines are drawn as THIN QUADS (triangles), not MeshTopology.Lines — line
// primitives are unreliable under URP 2D (often invisible with MSAA, always 1px). Quads
// rasterize everywhere, give a controllable width, and sort like any sprite. Auto-sizes to
// whatever cells exist (works for ragged/expanded islands), one draw call. Toggled by the
// PlacementController on build-mode enter/exit. Keep this GameObject at world origin (0,0,0)
// with no rotation/scale — vertices are built in world space.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridOverlay : MonoBehaviour
{
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color occupiedColor = new Color(1f, 1f, 1f, 0.12f);   // faint fill over cells a structure occupies (its territory)
    [SerializeField] private float lineWidth = 0.04f;              // world units
    [SerializeField] private string sortingLayerName = "Ground";  // MUST be in front of grass (+ structures); the default matches the grass layer
    [SerializeField] private int sortingOrder = 1000;             // and order within that layer (above the tilemap's 0)

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;

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
        meshFilter.mesh = mesh;
        meshRenderer.enabled = false;
    }

    public void Show()
    {
        if (islandSystem == null) { Debug.LogError("[GridOverlay] islandSystem not assigned", this); return; }

        Rebuild();
        meshRenderer.enabled = true;
    }

    public void Hide()
    {
        meshRenderer.enabled = false;
    }

    // Rebuild while shown — call after occupancy changes (place/sell/move) so the territory fill stays current.
    public void Refresh()
    {
        if (meshRenderer != null && meshRenderer.enabled) Rebuild();
    }

    // Rebuild the line mesh from the grid's current cells. Cheap (a few hundred cells), run only
    // on build-mode enter, so safe to do unconditionally even if the grid grew since last time.
    private void Rebuild()
    {
        var grid = islandSystem.Grid;
        if (grid == null) return;

        float cs = grid.CellSize;
        float half = lineWidth * 0.5f;

        var verts = new List<Vector3>();
        var colors = new List<Color>();
        var tris = new List<int>();

        // Territory fills first (so the grid lines draw on top of them): one full-cell quad per cell
        // a structure occupies — this reveals each structure's claimed area, including the otherwise
        // invisible border cells of a house.
        foreach (var kv in grid.Cells)
        {
            if (kv.Value.occupant == null) continue;
            var c = kv.Key;
            AddQuad(verts, colors, tris, new Vector2(c.x * cs, c.y * cs), new Vector2((c.x + 1) * cs, (c.y + 1) * cs), occupiedColor);
        }

        // Collect unique cell edges as lattice-point pairs (a cell's corner (lx,ly) is at world
        // (lx*cs, ly*cs)). Dedup so shared interior edges aren't drawn — and overdrawn — twice.
        var edges = new HashSet<(Vector2Int, Vector2Int)>();
        foreach (var kv in grid.Cells)
        {
            var c = kv.Key;
            var bl = new Vector2Int(c.x, c.y);
            var br = new Vector2Int(c.x + 1, c.y);
            var tl = new Vector2Int(c.x, c.y + 1);
            var tr = new Vector2Int(c.x + 1, c.y + 1);
            AddEdge(edges, bl, br);
            AddEdge(edges, tl, tr);
            AddEdge(edges, bl, tl);
            AddEdge(edges, br, tr);
        }

        foreach (var (a, b) in edges)
        {
            Vector2 wa = new Vector2(a.x * cs, a.y * cs);
            Vector2 wb = new Vector2(b.x * cs, b.y * cs);
            Vector2 dir = (wb - wa).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * half; // perpendicular half-width

            int baseIdx = verts.Count;
            verts.Add(new Vector3(wa.x - n.x, wa.y - n.y, 0f));
            verts.Add(new Vector3(wa.x + n.x, wa.y + n.y, 0f));
            verts.Add(new Vector3(wb.x + n.x, wb.y + n.y, 0f));
            verts.Add(new Vector3(wb.x - n.x, wb.y - n.y, 0f));
            for (int i = 0; i < 4; i++) colors.Add(lineColor);

            tris.Add(baseIdx + 0); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
        }

        mesh.Clear();
        mesh.indexFormat = verts.Count > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
    }

    private static void AddEdge(HashSet<(Vector2Int, Vector2Int)> edges, Vector2Int p, Vector2Int q)
    {
        // Normalize endpoint order so a shared edge maps to one entry regardless of which cell adds it.
        if (q.x < p.x || (q.x == p.x && q.y < p.y)) (p, q) = (q, p);
        edges.Add((p, q));
    }

    // Append an axis-aligned quad spanning bottom-left `a` to top-right `b`, all one vertex color.
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
