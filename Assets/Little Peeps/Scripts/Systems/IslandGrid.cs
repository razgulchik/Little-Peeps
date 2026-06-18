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
    private readonly float cellSize;

    // Read-only view for renderers / iteration (e.g. tilemap refresh).
    public IReadOnlyDictionary<Vector2Int, Cell> Cells => cells;
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
    public bool CanPlace(Vector2Int origin, Vector2Int size, TerrainType[] allowedTerrain)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                var cell = GetCell(new Vector2Int(x, y));
                if (cell == null) return false;            // off-island (no such cell)
                if (cell.occupant != null) return false;   // occupied
                if (!IsTerrainAllowed(cell.terrain, allowedTerrain)) return false;
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

    // Mark all cells covered by origin+size as occupied by structureInstance
    public void Place(Vector2Int origin, Vector2Int size, StructureInstance structureInstance)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                var cell = GetCell(new Vector2Int(x, y));
                if (cell != null) cell.occupant = structureInstance;
            }
    }

    // Clear occupant on all cells covered by origin+size
    public void Remove(Vector2Int origin, Vector2Int size)
    {
        for (int x = origin.x; x < origin.x + size.x; x++)
            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                var cell = GetCell(new Vector2Int(x, y));
                if (cell != null) cell.occupant = null;
            }
    }

    // Atomically move a structure: CanPlace at destination, Remove source, Place destination
    public bool Move(Vector2Int from, Vector2Int size, Vector2Int to)
    {
        // TODO (Phase 4): read occupant + terrain from `from`; if !CanPlace(to, size, terrain)
        // return false; Remove(from, size); Place(to, size, occupant); return true.
        return false;
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
}
