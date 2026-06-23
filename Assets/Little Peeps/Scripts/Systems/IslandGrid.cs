using System.Collections.Generic;
using UnityEngine;

// Pure C# data structure for the tile grid; no MonoBehaviour.
//
// SPARSE grid keyed by SIGNED cell coordinates, anchored to a FIXED point in world space: the
// center of cell c is at world (c + 0.5) * cellSize, independent of which cells exist. A cell
// exists only if it has been added (the IslandGenerator seeds them), so:
//  - the island can be any shape (clean square, ragged edges, detached clusters) — only the
//    cells that exist are land; a missing cell is simply off-island;
//  - growth in any direction / from any side is just "add more cells" — no reallocation, and
//    every existing cell keeps BOTH its coordinate and its world position for the whole game
//    (the key IS the coordinate, nothing ever shifts → no re-indexing of placed structures);
//  - "can't build past the edge" falls out for free (a missing footprint cell fails CanPlace).
//
// Cell c maps 1:1 to a Unity tilemap cell c (same (c+0.5) center convention), so tiles,
// structures and the grid overlay align natively with no editor offset.
//
// Performance note: lookups are O(1) dictionary hits and the grid is never queried in the
// per-frame / per-collision path (units are physics-driven). Heavy generation passes should run
// on a dense local scratch buffer and commit the result here once (see IslandGenerator).
public class IslandGrid
{
    public class Cell
    {
        public TerrainType terrain;
        public StructureInstance occupant;
    }

    private readonly Dictionary<Vector2Int, Cell> cells;
    private readonly Dictionary<Edge, EdgeInstance> edges = new();   // fences on cell boundaries
    private readonly float cellSize;

    // Read-only view for renderers / iteration (e.g. tilemap refresh).
    public IReadOnlyDictionary<Vector2Int, Cell> Cells => cells;
    public IReadOnlyDictionary<Edge, EdgeInstance> Edges => edges;
    public float CellSize => cellSize;

    public IslandGrid(float cellSize, int initialCapacity = 0)
    {
        this.cellSize = cellSize;
        cells = initialCapacity > 0 ? new Dictionary<Vector2Int, Cell>(initialCapacity)
                                    : new Dictionary<Vector2Int, Cell>();
    }

    // Create the cell at coord (or update its terrain, keeping any occupant). Used by the
    // generator to seed the initial island and to add cells on expansion.
    public void SetCell(Vector2Int coord, TerrainType terrain)
    {
        if (cells.TryGetValue(coord, out var cell)) cell.terrain = terrain;
        else cells[coord] = new Cell { terrain = terrain };
    }

    public Cell GetCell(Vector2Int coord)
    {
        return cells.TryGetValue(coord, out var cell) ? cell : null;
    }

    // True if a structure of given size can be placed at origin: every covered cell must exist
    // (be land), be unoccupied, and be an allowed terrain. Empty/null allowedTerrain = any.
    public bool CanPlace(Vector2Int origin, Vector2Int size, TerrainType[] allowedTerrain, int border = 0)
    {
        // A structure occupies its footprint EXPANDED by `border` on every side — the border is just
        // extra claimed territory, not a separate thing (a 2x2 house with border 1 occupies 4x4).
        // The whole expanded area must be on-island and unoccupied; terrain is only checked on the
        // actual footprint (the border is claimed spacing, any land will do).
        for (int x = origin.x - border; x < origin.x + size.x + border; x++)
        {
            for (int y = origin.y - border; y < origin.y + size.y + border; y++)
            {
                var cell = GetCell(new Vector2Int(x, y));
                if (cell == null) return false;            // off-island (footprint or claimed border)
                if (cell.occupant != null) return false;   // occupied (footprint or claimed border)

                bool inFootprint = x >= origin.x && x < origin.x + size.x
                                && y >= origin.y && y < origin.y + size.y;
                if (inFootprint && !IsTerrainAllowed(cell.terrain, allowedTerrain)) return false;
            }
        }
        return true;
    }

    private static bool IsTerrainAllowed(TerrainType terrain, TerrainType[] allowed)
    {
        if (allowed == null || allowed.Length == 0) return true;
        for (int i = 0; i < allowed.Length; i++)
            if (allowed[i] == terrain) return true;
        return false;
    }

    // Mark the structure's whole occupied territory (footprint expanded by its def.border) as occupied.
    public void Place(Vector2Int origin, Vector2Int size, StructureInstance structureInstance)
    {
        int border = structureInstance.Def != null ? structureInstance.Def.border : 0;
        for (int x = origin.x - border; x < origin.x + size.x + border; x++)
            for (int y = origin.y - border; y < origin.y + size.y + border; y++)
            {
                var cell = GetCell(new Vector2Int(x, y));
                if (cell != null) cell.occupant = structureInstance;
            }
    }

    // Clear the structure's whole occupied territory (footprint expanded by its border). The border
    // is read from the occupant at origin, so callers still pass only origin + footprint size.
    public void Remove(Vector2Int origin, Vector2Int size)
    {
        var occupant = GetCell(origin)?.occupant;
        int border = (occupant != null && occupant.Def != null) ? occupant.Def.border : 0;
        for (int x = origin.x - border; x < origin.x + size.x + border; x++)
            for (int y = origin.y - border; y < origin.y + size.y + border; y++)
            {
                var cell = GetCell(new Vector2Int(x, y));
                if (cell != null && cell.occupant == occupant) cell.occupant = null;
            }
    }

    // World position of the CENTER of a grid cell. Anchored at the world origin, so it does not
    // depend on which cells exist — the same cell maps to the same world point for the whole game.
    public Vector2 GridToWorld(Vector2Int cell)
    {
        return new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
    }

    // Cell containing a world position
    public Vector2Int WorldToGrid(Vector2 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / cellSize),
            Mathf.FloorToInt(worldPos.y / cellSize)
        );
    }

    // Origin (bottom-left) cell for a footprint of `size` centered nearest worldCenter.
    // Inverse of OriginToWorldCenter; same centering used for the placement ghost.
    public Vector2Int WorldToOrigin(Vector2 worldCenter, Vector2Int size)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldCenter.x / cellSize - size.x / 2f),
            Mathf.RoundToInt(worldCenter.y / cellSize - size.y / 2f)
        );
    }

    // World position of the center of a footprint of `size` anchored at origin.
    public Vector2 OriginToWorldCenter(Vector2Int origin, Vector2Int size)
    {
        return new Vector2(
            (origin.x + size.x / 2f) * cellSize,
            (origin.y + size.y / 2f) * cellSize
        );
    }

    // --- Edge layer (fences on cell boundaries) -----------------------------------------------
    // Parallel to the cell API above, but keyed by Edge instead of Vector2Int. Cells and edges share
    // the same lattice, so no offset bookkeeping — an Edge just names a line between two cells.

    public EdgeInstance GetEdge(Edge edge) => edges.TryGetValue(edge, out var inst) ? inst : null;

    // The two cells an edge borders (some may be off-island / not present in the grid).
    public (Vector2Int a, Vector2Int b) EdgeCells(Edge edge)
    {
        return edge.horizontal
            ? (new Vector2Int(edge.anchor.x, edge.anchor.y), new Vector2Int(edge.anchor.x, edge.anchor.y - 1))
            : (new Vector2Int(edge.anchor.x, edge.anchor.y), new Vector2Int(edge.anchor.x - 1, edge.anchor.y));
    }

    // A fence fits an edge if the edge is free AND at least one bordering cell is land — this lets
    // the player fence along the shoreline, not just interior boundaries.
    public bool CanPlaceEdge(Edge edge)
    {
        if (edges.ContainsKey(edge)) return false;
        var (a, b) = EdgeCells(edge);
        return GetCell(a) != null || GetCell(b) != null;
    }

    public void PlaceEdge(Edge edge, EdgeInstance instance) => edges[edge] = instance;

    public void RemoveEdge(Edge edge) => edges.Remove(edge);

    // World midpoint of an edge's line segment — where the fence sprite is centered.
    public Vector2 EdgeToWorld(Edge edge)
    {
        return edge.horizontal
            ? new Vector2((edge.anchor.x + 0.5f) * cellSize, edge.anchor.y * cellSize)
            : new Vector2(edge.anchor.x * cellSize, (edge.anchor.y + 0.5f) * cellSize);
    }

    // The grid edge nearest a world position: find the cell under the point, then pick whichever of
    // its four sides the point is closest to (the side with the smallest perpendicular distance).
    public Edge WorldToEdge(Vector2 worldPos)
    {
        float fx = worldPos.x / cellSize;
        float fy = worldPos.y / cellSize;
        int cx = Mathf.FloorToInt(fx);
        int cy = Mathf.FloorToInt(fy);
        float u = fx - cx;   // 0..1 across the cell, left→right
        float v = fy - cy;   // 0..1 across the cell, bottom→top

        float dBottom = v, dTop = 1f - v, dLeft = u, dRight = 1f - u;
        float min = Mathf.Min(Mathf.Min(dBottom, dTop), Mathf.Min(dLeft, dRight));

        if (min == dBottom) return new Edge(new Vector2Int(cx, cy), true);
        if (min == dTop)    return new Edge(new Vector2Int(cx, cy + 1), true);
        if (min == dLeft)   return new Edge(new Vector2Int(cx, cy), false);
        return new Edge(new Vector2Int(cx + 1, cy), false);
    }
}
