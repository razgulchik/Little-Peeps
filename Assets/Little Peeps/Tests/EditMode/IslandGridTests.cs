using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // IslandGrid is the placement authority: what may be built where, and how cells map to world space.
    // Two things are pinned here.
    //
    // Occupancy: a structure claims its footprint EXPANDED by its border, so CanPlace / Place / Remove
    // must all agree on the same expanded box, and Remove must clear exactly what Place marked.
    //
    // Coordinates: every round-trip is exercised on NEGATIVE coordinates. The island is centred on the
    // world origin, so half of it always sits at negative x/y, and a truncation-instead-of-floor bug
    // would look perfect in the positive quadrant and be wrong on the other half of every map.
    public class IslandGridTests
    {
        private static readonly TerrainType[] GrassOnly = { TerrainType.Grass };
        private static readonly TerrainType[] WaterOnly = { TerrainType.Water };

        // StructureDefs are ScriptableObjects, so they are tracked and destroyed rather than leaked
        // into the editor session.
        private readonly List<StructureDef> defs = new();

        [TearDown]
        public void DestroyCreatedDefs()
        {
            foreach (var def in defs)
                if (def != null) Object.DestroyImmediate(def);
            defs.Clear();
        }

        private StructureInstance Structure(Vector2Int cell, Vector2Int size, int border = 0)
        {
            var def = ScriptableObject.CreateInstance<StructureDef>();
            def.size = size;
            def.border = border;
            defs.Add(def);
            return new StructureInstance { Def = def, Cell = cell };
        }

        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        // --- CanPlace ------------------------------------------------------------------------------

        [Test]
        public void CanPlace_True_WhenTheWholeFootprintIsFreeLand()
        {
            var grid = TestIsland.Square(-2, 2);

            Assert.IsTrue(grid.CanPlace(C(-2, -2), C(2, 2), null));
        }

        [Test]
        public void CanPlace_False_WhenPartOfTheFootprintIsOffIsland()
        {
            var grid = TestIsland.Square(-2, 2);

            // A 2x2 at (2,2) needs column x=3 and row y=3, which were never seeded — no cell means
            // "not land", which is how the "can't build past the edge" rule falls out of the sparse grid.
            Assert.IsFalse(grid.CanPlace(C(2, 2), C(2, 2), null));
        }

        [Test]
        public void CanPlace_False_OnACellAnotherStructureOccupies()
        {
            var grid = TestIsland.Square(-2, 2);
            grid.Place(C(-1, -1), C(1, 1), Structure(C(-1, -1), C(1, 1)));

            Assert.IsFalse(grid.CanPlace(C(-1, -1), C(1, 1), null));
        }

        [Test]
        public void CanPlace_False_InsideAnotherStructuresBorderRing()
        {
            var grid = TestIsland.Square(-3, 3);
            // A 1x1 with border 1 at the origin claims the whole -1..1 box, footprint AND ring.
            grid.Place(C(0, 0), C(1, 1), Structure(C(0, 0), C(1, 1), border: 1));

            Assert.IsFalse(grid.CanPlace(C(1, 1), C(1, 1), null), "(1,1) is claimed border, not free land");
            Assert.IsTrue(grid.CanPlace(C(2, 2), C(1, 1), null), "(2,2) is outside the claimed box");
        }

        [Test]
        public void CanPlace_False_WhenTheOwnBorderRingWouldRunOffTheIsland()
        {
            var grid = TestIsland.Square(-2, 2);

            // The footprint fits on the island's last cell, but its border ring needs x=3 / y=3.
            // This is the mechanism that keeps bordered buildings one clear cell from the map edge.
            Assert.IsFalse(grid.CanPlace(C(2, 2), C(1, 1), null, border: 1));
            Assert.IsTrue(grid.CanPlace(C(2, 2), C(1, 1), null, border: 0));
        }

        [Test]
        public void CanPlace_False_WhenTheFootprintTerrainIsNotAllowed()
        {
            var grid = TestIsland.Square(-2, 2, terrain: TerrainType.Grass);

            Assert.IsFalse(grid.CanPlace(C(0, 0), C(1, 1), WaterOnly));
            Assert.IsTrue(grid.CanPlace(C(0, 0), C(1, 1), GrassOnly));
        }

        [Test]
        public void CanPlace_TreatsAnEmptyAllowedTerrainListAsAnyTerrain()
        {
            var grid = TestIsland.Square(-2, 2, terrain: TerrainType.Water);

            Assert.IsTrue(grid.CanPlace(C(0, 0), C(1, 1), null));
            Assert.IsTrue(grid.CanPlace(C(0, 0), C(1, 1), new TerrainType[0]));
        }

        [Test]
        public void CanPlace_ChecksTerrainOnTheFootprintOnly_NotOnTheBorderRing()
        {
            // One patch of grass in a water island: the border is claimed SPACING, so any land will do
            // there. Only the footprint has to satisfy allowedTerrain.
            var grid = TestIsland.Square(-2, 2, terrain: TerrainType.Water);
            grid.SetCell(C(-1, -1), TerrainType.Grass);

            Assert.IsTrue(grid.CanPlace(C(-1, -1), C(1, 1), GrassOnly, border: 1));
        }

        [Test]
        public void SetCell_ChangesTerrainWithoutEvictingTheOccupant()
        {
            var grid = TestIsland.Square(-1, 1);
            var house = Structure(C(0, 0), C(1, 1));
            grid.Place(C(0, 0), C(1, 1), house);

            grid.SetCell(C(0, 0), TerrainType.Stone);

            Assert.AreEqual(TerrainType.Stone, grid.GetCell(C(0, 0)).terrain);
            Assert.AreSame(house, grid.GetCell(C(0, 0)).occupant, "re-seeding terrain must not clear the cell");
        }

        // --- Place / Remove symmetry ---------------------------------------------------------------

        [Test]
        public void Place_MarksTheFootprintExpandedByTheBorder()
        {
            var grid = TestIsland.Square(-3, 3);
            var house = Structure(C(0, 0), C(2, 2), border: 1);
            grid.Place(C(0, 0), C(2, 2), house);

            // A 2x2 with border 1 owns the whole -1..2 box: 4x4 cells, footprint plus ring.
            for (int x = -1; x <= 2; x++)
                for (int y = -1; y <= 2; y++)
                    Assert.AreSame(house, grid.GetCell(C(x, y)).occupant, $"cell ({x},{y}) is claimed territory");

            Assert.IsNull(grid.GetCell(C(-2, 0)).occupant, "one cell further out is untouched");
            Assert.IsNull(grid.GetCell(C(3, 0)).occupant, "one cell further out is untouched");
        }

        [Test]
        public void Remove_ClearsExactlyWhatPlaceMarked()
        {
            var grid = TestIsland.Square(-3, 3);
            var house = Structure(C(-1, -1), C(2, 2), border: 1);

            grid.Place(C(-1, -1), C(2, 2), house);
            grid.Remove(C(-1, -1), C(2, 2));

            for (int x = -2; x <= 1; x++)
                for (int y = -2; y <= 1; y++)
                    Assert.IsNull(grid.GetCell(C(x, y)).occupant, $"cell ({x},{y}) should be free again");

            Assert.IsTrue(grid.CanPlace(C(-1, -1), C(2, 2), null, border: 1),
                          "the same structure must be placeable again after Remove");
        }

        [Test]
        public void Remove_LeavesANeighbouringStructureAlone()
        {
            var grid = TestIsland.Square(-3, 3);
            var a = Structure(C(-2, 0), C(2, 1));
            var b = Structure(C(0, 0), C(1, 1));
            grid.Place(C(-2, 0), C(2, 1), a);
            grid.Place(C(0, 0), C(1, 1), b);

            grid.Remove(C(-2, 0), C(2, 1));

            Assert.IsNull(grid.GetCell(C(-2, 0)).occupant);
            Assert.IsNull(grid.GetCell(C(-1, 0)).occupant);
            Assert.AreSame(b, grid.GetCell(C(0, 0)).occupant, "the neighbour keeps its cell");
        }

        [Test]
        public void Remove_OnACellWithNoOccupant_CannotEvictAnyoneElse()
        {
            var grid = TestIsland.Square(-3, 3);
            var b = Structure(C(1, 0), C(1, 1));
            grid.Place(C(1, 0), C(1, 1), b);

            // Origin (0,0) is empty, so the occupant Remove matches against is null — the sweep must
            // then clear nothing, even though its 2x1 footprint runs straight over b's cell.
            Assert.DoesNotThrow(() => grid.Remove(C(0, 0), C(2, 1)));
            Assert.AreSame(b, grid.GetCell(C(1, 0)).occupant);
        }

        [Test]
        public void PlaceAndRemove_IgnoreTerritoryCellsThatAreOffIsland()
        {
            var grid = TestIsland.Square(-2, 2);
            var house = Structure(C(2, 2), C(1, 1), border: 1);

            // Its border ring reaches x=3 / y=3, which do not exist. Nothing may be created there and
            // nothing may throw — Place/Remove skip missing cells.
            Assert.DoesNotThrow(() => grid.Place(C(2, 2), C(1, 1), house));
            Assert.IsNull(grid.GetCell(C(3, 3)), "a missing cell must not be conjured into existence");
            Assert.DoesNotThrow(() => grid.Remove(C(2, 2), C(1, 1)));
            Assert.IsNull(grid.GetCell(C(2, 2)).occupant);
        }

        // --- coordinate round-trips ----------------------------------------------------------------

        [Test]
        public void GridToWorld_ReturnsTheCellCentre_OnNegativeCoordinates()
        {
            var grid = new IslandGrid(1f);

            // Cell c is centred at (c + 0.5) * cellSize, so cell (-1,-1) sits at (-0.5,-0.5) — NOT at
            // (-1.5,-1.5), which is what a "round away from zero" mistake would produce.
            Assert.AreEqual(new Vector2(-0.5f, -0.5f), grid.GridToWorld(C(-1, -1)));
            Assert.AreEqual(new Vector2(0.5f, 0.5f), grid.GridToWorld(C(0, 0)));
        }

        [Test]
        public void WorldToGrid_FloorsTowardsNegativeInfinity()
        {
            var grid = new IslandGrid(1f);

            // The classic sign bug: a cast to int truncates toward zero and would answer (0,0) here,
            // silently putting everything just left of / below the origin into the wrong cell.
            Assert.AreEqual(C(-1, -1), grid.WorldToGrid(new Vector2(-0.1f, -0.1f)));
            Assert.AreEqual(C(0, 0), grid.WorldToGrid(new Vector2(0.1f, 0.1f)));
            Assert.AreEqual(C(-1, 0), grid.WorldToGrid(new Vector2(-0.5f, 0.5f)));
        }

        [TestCase(1f)]
        [TestCase(0.32f)]
        public void GridToWorld_RoundTripsThroughWorldToGrid_AcrossTheOrigin(float cellSize)
        {
            var grid = new IslandGrid(cellSize);

            for (int x = -4; x <= 4; x++)
                for (int y = -4; y <= 4; y++)
                {
                    var cell = C(x, y);
                    Assert.AreEqual(cell, grid.WorldToGrid(grid.GridToWorld(cell)), $"cell {cell}");
                }
        }

        [TestCase(1f)]
        [TestCase(0.32f)]
        public void OriginToWorldCenter_RoundTripsThroughWorldToOrigin_AcrossTheOrigin(float cellSize)
        {
            var grid = new IslandGrid(cellSize);
            // Odd and even footprints round differently (size/2f lands on a cell centre vs a cell
            // corner), so both are covered on negative origins.
            Vector2Int[] sizes = { C(1, 1), C(2, 2), C(3, 3), C(2, 3) };

            foreach (var size in sizes)
                for (int x = -4; x <= 4; x++)
                    for (int y = -4; y <= 4; y++)
                    {
                        var origin = C(x, y);
                        Assert.AreEqual(origin, grid.WorldToOrigin(grid.OriginToWorldCenter(origin, size), size),
                                        $"origin {origin}, size {size}");
                    }
        }

        [Test]
        public void OriginToWorldCenter_CentresTheFootprint_OnNegativeOrigins()
        {
            var grid = new IslandGrid(1f);

            // 2x2 at (-2,-2) covers x,y in -2..-1, whose middle is the lattice point (-1,-1).
            Assert.AreEqual(new Vector2(-1f, -1f), grid.OriginToWorldCenter(C(-2, -2), C(2, 2)));
            // 1x1 at (-1,-1) is one cell, so its centre is that cell's centre.
            Assert.AreEqual(new Vector2(-0.5f, -0.5f), grid.OriginToWorldCenter(C(-1, -1), C(1, 1)));
        }

        [Test]
        public void OriginToWorldCenter_AgreesWithGridToWorld_ForASingleCell()
        {
            var grid = new IslandGrid(0.32f);

            // The two anchoring conventions must not drift apart: a 1x1 footprint IS its cell.
            Assert.AreEqual(grid.GridToWorld(C(-3, 2)), grid.OriginToWorldCenter(C(-3, 2), C(1, 1)));
        }
    }
}
