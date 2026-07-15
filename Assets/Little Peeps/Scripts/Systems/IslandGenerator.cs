using System.Collections.Generic;
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

    // Add the cells of each block to the island as Grass. Blocks are absolute grid rectangles; only
    // MISSING cells are created, so existing cells (their terrain and occupants) are left untouched —
    // growth is purely additive and can form any shape (squares, L-corners, detached clusters).
    public void Expand(IReadOnlyList<RectInt> blocks)
    {
        if (blocks == null) return;

        for (int i = 0; i < blocks.Count; i++)
        {
            RectInt b = blocks[i];
            for (int x = b.xMin; x < b.xMax; x++)
                for (int y = b.yMin; y < b.yMax; y++)
                {
                    var coord = new Vector2Int(x, y);
                    if (grid.GetCell(coord) == null) grid.SetCell(coord, TerrainType.Grass);
                }
        }
    }
}
