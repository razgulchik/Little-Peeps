using UnityEngine;

namespace LittlePeeps.Tests
{
    // Shared island builder for the grid and spawner geometry tests.
    //
    // Deliberately tiny: the tests it serves are ABOUT coordinates, so a fixture that computed or
    // centred anything would hide the very thing under test. It only fills a rectangle of cells —
    // every coordinate a test cares about is still written out in the test itself.
    internal static class TestIsland
    {
        // Seed a rectangular island covering min..max INCLUSIVE on both axes.
        //
        // Tests pass negative bounds as the normal case: the real island is centred on the world
        // origin, so roughly half of every island lives at negative x/y and a sign error there is a
        // shipped bug, not an edge case.
        public static IslandGrid Rect(Vector2Int min, Vector2Int max, float cellSize = 1f,
                                      TerrainType terrain = TerrainType.Grass)
        {
            var grid = new IslandGrid(cellSize);
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    grid.SetCell(new Vector2Int(x, y), terrain);
            return grid;
        }

        // Square island spanning min..max inclusive on both axes, e.g. Square(-5, 5) = 11x11 centred
        // on the origin.
        public static IslandGrid Square(int min, int max, float cellSize = 1f,
                                        TerrainType terrain = TerrainType.Grass)
            => Rect(new Vector2Int(min, min), new Vector2Int(max, max), cellSize, terrain);
    }
}
