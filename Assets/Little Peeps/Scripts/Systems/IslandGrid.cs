using UnityEngine;

// Pure C# data structure for the tile grid; no MonoBehaviour
public class IslandGrid
{
    public class Cell
    {
        public TerrainType terrain;
        public StructureInstance occupant;
    }

    private Cell[,] cells;
    private Vector2Int gridSize;
    private Vector2 worldOrigin;
    private float cellSize;

    public Vector2Int GridSize => gridSize;

    public IslandGrid(Vector2Int size, Vector2 origin, float cellSize)
    {
        gridSize = size;
        worldOrigin = origin;
        this.cellSize = cellSize;
        cells = new Cell[size.x, size.y];
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                cells[x, y] = new Cell { terrain = TerrainType.Grass };
    }

    // True if a structure of given size can be placed at origin with matching terrain
    public bool CanPlace(Vector2Int origin, Vector2Int size, TerrainType required)
    {
        // TODO: for each cell in [origin, origin+size): check InBounds, terrain == required, occupant == null
        return false;
    }

    // Mark all cells covered by origin+size as occupied by structureInstance
    public void Place(Vector2Int origin, Vector2Int size, StructureInstance structureInstance)
    {
        // TODO: iterate cells, set occupant = structureInstance
    }

    // Clear occupant on all cells covered by origin+size
    public void Remove(Vector2Int origin, Vector2Int size)
    {
        // TODO: iterate cells, set occupant = null
    }

    // Atomically move a structure: CanPlace at destination, Remove source, Place destination
    public bool Move(Vector2Int from, Vector2Int size, Vector2Int to)
    {
        // TODO: if !CanPlace(to, size, terrain), return false; Remove(from, size); Place(to, size, occupant)
        return false;
    }

    // World position of the center of a grid cell
    public Vector2 GridToWorld(Vector2Int cell)
    {
        return worldOrigin + new Vector2((cell.x + 0.5f) * cellSize, (cell.y + 0.5f) * cellSize);
    }

    // Nearest grid cell for a world position
    public Vector2Int WorldToGrid(Vector2 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt((worldPos.x - worldOrigin.x) / cellSize),
            Mathf.FloorToInt((worldPos.y - worldOrigin.y) / cellSize)
        );
    }

    // Grow grid to newSize, preserving existing cells and filling new ones with default terrain
    public void Expand(Vector2Int newSize)
    {
        // TODO: allocate new Cell[newSize.x, newSize.y], copy cells, init new cells, update gridSize
    }

    private bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < gridSize.x && cell.y < gridSize.y;
    }

    public Cell GetCell(Vector2Int pos)
    {
        return InBounds(pos) ? cells[pos.x, pos.y] : null;
    }
}
