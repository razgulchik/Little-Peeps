using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LittlePeeps.Tests
{
    // What IslandDrawing does to the scene's tilemaps beyond painting them. Needs real Tilemap components,
    // so it runs in Unity's Test Runner only.
    public class IslandDrawingTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        private TilemapRenderer NewTilemap(string name, string sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }

        [Test]
        public void BorderLayers_AreSortedByTheCascade_OverTheGroundDecorAndTrim()
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var ground = NewTilemap("Ground", "Default", 0);
            var trim = NewTilemap("TrimHorizontal", "Default", 1);
            var decor = NewTilemap("Decor", "Default", 4);
            // Typed in upside down, and the meadow on another sorting layer altogether.
            var meadow = NewTilemap("BordersMeadow", "Water", 0);
            var forest = NewTilemap("BordersForest", "Default", 9);

            var borders = new List<IslandBorderLayer>
            {
                new(TerrainType.Grass, null, meadow.GetComponent<Tilemap>()),
                new(TerrainType.Forest, null, forest.GetComponent<Tilemap>()),
            };
            new IslandDrawing(new IslandGrid(1f), _ => null, ground.GetComponent<Tilemap>(), trim.GetComponent<Tilemap>(),
                              decor.GetComponent<Tilemap>(), 0, borders);

            Assert.That(forest.sortingLayerID, Is.EqualTo(ground.sortingLayerID));
            Assert.That(meadow.sortingLayerID, Is.EqualTo(ground.sortingLayerID));
            Assert.That(forest.sortingOrder, Is.EqualTo(5), "just over the highest land layer below it (decor, 4)");
            Assert.That(meadow.sortingOrder, Is.EqualTo(6), "listed above the forest, so drawn over it");
        }

        [Test]
        public void ARowWithoutATilemap_KeepsItsPlace_ButMovesNothing()
        {
            root = new GameObject("Island");
            root.AddComponent<Grid>();
            var ground = NewTilemap("Ground", "Default", 0);
            var forest = NewTilemap("BordersForest", "Default", 7);

            var borders = new List<IslandBorderLayer>
            {
                new(TerrainType.Grass, null, null),
                new(TerrainType.Forest, null, forest.GetComponent<Tilemap>()),
            };
            new IslandDrawing(new IslandGrid(1f), _ => null, ground.GetComponent<Tilemap>(), null, null, 0, borders);

            Assert.That(forest.sortingOrder, Is.EqualTo(1));
            Assert.That(ground.sortingOrder, Is.EqualTo(0), "the ground itself is never re-sorted");
        }
    }
}
