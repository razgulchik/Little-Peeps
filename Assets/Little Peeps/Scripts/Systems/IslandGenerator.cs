// Pure C# — procedurally fills and expands IslandGrid terrain per age
public class IslandGenerator
{
    private readonly IslandGrid grid;

    public IslandGenerator(IslandGrid grid)
    {
        this.grid = grid;
    }

    // Seed the grid terrain for the given age (obstacles, terrain distribution)
    public void Generate(int age)
    {
        for (int x = 0; x < grid.GridSize.x; x++)
            for (int y = 0; y < grid.GridSize.y; y++)
                grid.GetCell(new UnityEngine.Vector2Int(x, y)).terrain = TerrainType.Grass;

        // TODO: age > 0 — add varied terrain and obstacles based on age tier
    }

    // Expand grid size and populate newly added cells for the next age
    public void Expand(int age)
    {
        // TODO: compute new size from AgeDef catalogue; call grid.Expand(newSize); fill new boundary cells with terrain
    }
}
