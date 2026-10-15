using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // Animals are kinematic: walls do not stop them, the grid does — AnimalSpawner.IsBlocked is the whole
    // of it. Pinned here: a refused cell stops an animal stepping IN and never one already standing on
    // refused ground (a house built around a grazing animal once left it turning on the spot for good),
    // a construction site refuses animals whatever it will become, and the water's edge holds always.
    // StructureDefs are ScriptableObjects, so this runs in Unity's Test Runner only.
    public class AnimalBlockingTests
    {
        private readonly List<StructureDef> defs = new();
        private IslandGrid grid;

        [SetUp]
        public void SetUp() => grid = TestIsland.Rect(new Vector2Int(-3, -3), new Vector2Int(3, 3));

        [TearDown]
        public void TearDown()
        {
            foreach (var def in defs)
                if (def != null) Object.DestroyImmediate(def);
            defs.Clear();
        }

        private StructureInstance Put(Vector2Int cell, bool animalsAvoid, bool underConstruction = false)
        {
            var def = ScriptableObject.CreateInstance<StructureDef>();
            def.animalsAvoid = animalsAvoid;
            defs.Add(def);
            var instance = new StructureInstance { Def = def, Cell = cell, UnderConstruction = underConstruction };
            grid.Place(cell, def.Footprint, instance);
            return instance;
        }

        // Cell (x, y) is the unit square north-east of (x, y); these are their centres.
        private static Vector2 Centre(int x, int y) => new Vector2(x + 0.5f, y + 0.5f);

        [Test]
        public void SteppingIntoAnAvoidedBuilding_IsBlocked()
        {
            Put(Vector2Int.zero, animalsAvoid: true);

            Assert.IsTrue(AnimalSpawner.IsBlocked(grid, Centre(-1, 0), Centre(0, 0)));
        }

        [Test]
        public void StandingInsideABuilding_EveryStepOnIsFree()
        {
            Put(Vector2Int.zero, animalsAvoid: true);
            Put(new Vector2Int(1, 0), animalsAvoid: true);   // a second one right next door

            Assert.IsFalse(AnimalSpawner.IsBlocked(grid, Centre(0, 0), Centre(-1, 0)), "out onto free ground");
            Assert.IsFalse(AnimalSpawner.IsBlocked(grid, Centre(0, 0), Centre(1, 0)), "through the next one, on its way out");
        }

        [Test]
        public void ASite_RefusesAnimals_EvenForAWalkThroughStructure()
        {
            var field = Put(Vector2Int.zero, animalsAvoid: false, underConstruction: true);

            Assert.IsTrue(AnimalSpawner.IsBlocked(grid, Centre(-1, 0), Centre(0, 0)), "the site waits for its spot to clear");

            field.UnderConstruction = false;   // built
            Assert.IsFalse(AnimalSpawner.IsBlocked(grid, Centre(-1, 0), Centre(0, 0)), "the finished field is walk-through again");
        }

        [Test]
        public void TheWatersEdge_HoldsEvenFromInsideABuilding()
        {
            Put(new Vector2Int(3, 0), animalsAvoid: true);   // the island's east edge

            Assert.IsTrue(AnimalSpawner.IsBlocked(grid, Centre(3, 0), Centre(4, 0)));
        }
    }
}
