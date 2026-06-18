using UnityEngine;

// Pure C# — decides which cells exist (the island SHAPE) and their terrain, then commits them
// into the sparse IslandGrid. Shape lives here, not in the grid: the grid just stores whatever
// cells are added. For now Generate seeds a clean centered square of all-Grass; noise / ragged
// edges / detached clusters come later.
//
// Perf note for future generation: run heavy neighbour passes (noise, cellular-automata
// smoothing, flood-fill) on a dense local scratch buffer, then commit the result with one
// grid.SetCell per cell — keep dictionary writes one-shot, off any neighbour-lookup loop.
public class IslandGenerator
{
    private readonly IslandGrid grid;
    private readonly Vector2Int initialSize;

    public IslandGenerator(IslandGrid grid, Vector2Int initialSize)
    {
        this.grid = grid;
        this.initialSize = initialSize;
    }

    // Seed the starting island for the given age. Centered on the world origin so coordinates are
    // signed and symmetric (even sizes are offset by one cell — irrelevant).
    public void Generate(int age)
    {
        var min = new Vector2Int(-(initialSize.x / 2), -(initialSize.y / 2));
        for (int x = min.x; x < min.x + initialSize.x; x++)
            for (int y = min.y; y < min.y + initialSize.y; y++)
                grid.SetCell(new Vector2Int(x, y), TerrainType.Grass);

        // TODO: age > 0 — varied terrain / obstacles based on age tier.
    }

    // Add the cells exposed by the next age. Growth = adding new cells via grid.SetCell (any
    // direction / clusters); existing cells and their occupants are untouched.
    public void Expand(int age)
    {
        // TODO: compute new cells from AgeDef catalogue; grid.SetCell(coord, terrain) for each.
    }
}
