using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // Smoke test for the test harness itself: proves the EditMode assembly runs under the Test
    // Runner and can see LittlePeeps.Runtime types. IslandGrid is used because it is plain C# —
    // no scene, no MonoBehaviour, so a failure here means the wiring is broken, not the game.
    // Real coverage of the grid, RunStats, EventBus and StateMachine is a separate step.
    public class GridSmokeTests
    {
        [Test]
        public void GridToWorld_RoundTripsThroughWorldToGrid()
        {
            var grid = new IslandGrid(1f);
            // Negative coordinates are the normal case, not an edge case: the island is centred
            // on the world origin, so roughly half of every island lives at negative x/y.
            var cell = new Vector2Int(-3, 2);

            Assert.AreEqual(cell, grid.WorldToGrid(grid.GridToWorld(cell)));
        }

        [Test]
        public void GetCell_ReturnsNullForACellThatWasNeverAdded()
        {
            var grid = new IslandGrid(1f);
            grid.SetCell(Vector2Int.zero, TerrainType.Grass);

            Assert.IsNotNull(grid.GetCell(Vector2Int.zero), "seeded cell should exist");
            Assert.IsNull(grid.GetCell(new Vector2Int(1, 0)), "unseeded cell is off-island");
        }
    }
}
