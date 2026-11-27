using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // A bridge (StructureDef.onWater) goes up over a river cell that stays where it is. Pinned here: where
    // one may stand (a bare river cell, nothing else), what the grid remembers under it and gives back, that
    // the river is still water to what stands around it — its own neighbours' pieces, a watermill's wheel —
    // and who may walk over it: a house's launch and a carried unit may, once it stands, not while it is a site.
    //
    // The river runs along y = -3 from x = -5 to -1, on the negative side like half of every island.
    public class BridgeTests
    {
        private static readonly Vector2Int Middle = new Vector2Int(-3, -3);

        private readonly List<StructureDef> defs = new();
        private StructureDef riverDef, bridgeDef;
        private IslandGrid grid;
        private Dictionary<Vector2Int, StructureInstance> river;

        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

        [SetUp]
        public void SetUp()
        {
            riverDef = Def(Vector2Int.one);
            riverDef.isWater = true;
            riverDef.animalsAvoid = true;
            bridgeDef = Def(Vector2Int.one);
            bridgeDef.onWater = true;
            bridgeDef.passable = true;

            grid = TestIsland.Square(-8, 4);
            river = new Dictionary<Vector2Int, StructureInstance>();
            for (int x = -5; x <= -1; x++) river[C(x, -3)] = Put(riverDef, C(x, -3));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var def in defs)
                if (def != null) Object.DestroyImmediate(def);
            defs.Clear();
        }

        private StructureDef Def(Vector2Int size)
        {
            var def = ScriptableObject.CreateInstance<StructureDef>();
            def.size = size;
            defs.Add(def);
            return def;
        }

        private StructureInstance Put(StructureDef def, Vector2Int cell)
        {
            var instance = new StructureInstance { Def = def, Cell = cell };
            grid.Place(cell, def.Footprint, instance);
            return instance;
        }

        private void Remove(StructureInstance instance) => grid.Remove(instance.Cell, instance.Def.Footprint);

        // --- where it may stand ---------------------------------------------------------------------

        [Test]
        public void CanPlace_OnTheRiver() => Assert.IsTrue(grid.CanPlace(Middle, bridgeDef));

        [Test]
        public void CanPlace_False_OnBareLand() => Assert.IsFalse(grid.CanPlace(C(0, 0), bridgeDef));

        [Test]
        public void CanPlace_False_OnTheSea() => Assert.IsFalse(grid.CanPlace(C(40, 40), bridgeDef));

        [Test]
        public void CanPlace_False_OnAnotherStructure()
        {
            Put(Def(Vector2Int.one), C(0, 0));

            Assert.IsFalse(grid.CanPlace(C(0, 0), bridgeDef));
        }

        [Test]
        public void CanPlace_False_OverABridge()
        {
            Put(bridgeDef, Middle);

            Assert.IsFalse(grid.CanPlace(Middle, bridgeDef));
        }

        [Test]
        public void CanPlace_NextToABridge()
        {
            Put(bridgeDef, Middle);

            Assert.IsTrue(grid.CanPlace(Middle + Vector2Int.right, bridgeDef));
        }

        [Test]
        public void CanPlace_AnOrdinaryBuilding_NeitherOnTheRiverNorOnTheBridge()
        {
            var house = Def(Vector2Int.one);
            Put(bridgeDef, Middle);

            Assert.IsFalse(grid.CanPlace(Middle, house), "on the bridge");
            Assert.IsFalse(grid.CanPlace(Middle + Vector2Int.right, house), "on the river");
        }

        // --- what stays underneath ------------------------------------------------------------------

        [Test]
        public void Place_TakesTheCell_AndKeepsTheRiverBeneath()
        {
            var bridge = Put(bridgeDef, Middle);

            Assert.AreSame(bridge, grid.GetCell(Middle).occupant);
            Assert.AreSame(river[Middle], grid.GetCell(Middle).beneath);
        }

        [Test]
        public void Remove_GivesTheCellBackToTheRiver()
        {
            Remove(Put(bridgeDef, Middle));

            Assert.AreSame(river[Middle], grid.GetCell(Middle).occupant);
            Assert.IsNull(grid.GetCell(Middle).beneath);
            Assert.IsTrue(grid.CanPlace(Middle, bridgeDef), "and it may be bridged again");
        }

        [Test]
        public void Remove_LeavesTheBridgeBesideIt()
        {
            var gone = Put(bridgeDef, Middle);
            var kept = Put(bridgeDef, Middle + Vector2Int.right);

            Remove(gone);

            Assert.AreSame(kept, grid.GetCell(kept.Cell).occupant);
            Assert.AreSame(river[kept.Cell], grid.GetCell(kept.Cell).beneath);
        }

        [Test]
        public void Remove_TheBridgeAndThenTheRiver_LeavesBareLand()
        {
            Remove(Put(bridgeDef, Middle));   // the order a run is swept in
            Remove(river[Middle]);

            Assert.IsNull(grid.GetCell(Middle).occupant);
            Assert.IsNull(grid.GetCell(Middle).beneath);
        }

        [Test]
        public void TheSellTool_AimsAtTheBridge_ThenAtTheRiverAgain()
        {
            var bridge = Put(bridgeDef, Middle);
            Assert.AreSame(bridge, PlacementTarget.Resolve(grid, grid.GridToWorld(Middle)).Instance);

            Remove(bridge);
            Assert.AreSame(river[Middle], PlacementTarget.Resolve(grid, grid.GridToWorld(Middle)).Instance);
        }

        // --- still a river --------------------------------------------------------------------------

        [Test]
        public void TheRiver_KeepsItsShape_UnderAndBesideTheBridge()
        {
            var before = new List<RiverShape>();
            foreach (var cell in river.Keys) before.Add(RiverShapes.At(grid, cell));

            Put(bridgeDef, Middle);

            int i = 0;
            foreach (var cell in river.Keys) Assert.AreEqual(before[i++], RiverShapes.At(grid, cell), $"river cell {cell}");
        }

        [Test]
        public void AWatermill_KeepsItsSide_AndMayStillBePlaced()
        {
            var mill = Def(C(2, 1));
            mill.needsWaterSide = true;
            var origin = C(-4, -2);   // its right cell stands right above the middle

            Put(bridgeDef, Middle);

            Assert.AreEqual(WaterSide.SouthRight, WaterSides.Find(grid, origin, mill.Footprint));
            Assert.IsTrue(grid.CanPlace(origin, mill));
        }

        // --- the way across -------------------------------------------------------------------------

        private bool HouseOpensOntoTheMiddle() =>
            StructureExits.IsOpenExit(grid, Middle + Vector2Int.up, Footprint.Rect(1, 1), 0, Middle);

        [Test]
        public void AHouseBesideIt_LaunchesOntoTheBridge_NotOntoTheRiver()
        {
            Assert.IsFalse(HouseOpensOntoTheMiddle(), "river");

            var bridge = Put(bridgeDef, Middle);
            Assert.IsTrue(HouseOpensOntoTheMiddle(), "bridge");

            Remove(bridge);
            Assert.IsFalse(HouseOpensOntoTheMiddle(), "river again");
        }

        [Test]
        public void WhileItIsASite_NobodyIsLetOntoIt()
        {
            Put(bridgeDef, Middle).UnderConstruction = true;

            Assert.IsFalse(HouseOpensOntoTheMiddle(), "a house's launch");
            Assert.IsFalse(CarryTrail.IsFree(grid, Middle), "a carried unit");
        }

        [Test]
        public void ACarriedUnit_MayComeDownOnTheBridge_NotOnTheRiver()
        {
            Put(bridgeDef, Middle);

            Assert.IsTrue(CarryTrail.IsFree(grid, Middle));
            Assert.IsFalse(CarryTrail.IsFree(grid, Middle + Vector2Int.right));
        }

        [Test]
        public void Animals_CrossTheBridge_NotTheRiver()
        {
            Put(bridgeDef, Middle);
            Vector2 bank = grid.GridToWorld(Middle + Vector2Int.up);

            Assert.IsFalse(AnimalSpawner.IsBlocked(grid, bank, grid.GridToWorld(Middle)), "bridge");
            Assert.IsTrue(AnimalSpawner.IsBlocked(grid, bank, grid.GridToWorld(Middle + Vector2Int.right)), "river");
        }

        [Test]
        public void AFence_StandsAlongTheBridge_OnAnySideOfIt()
        {
            Put(bridgeDef, Middle);

            Assert.IsTrue(grid.CanPlaceEdge(new Edge(Middle, true)), "towards the bank");
            Assert.IsTrue(grid.CanPlaceEdge(new Edge(Middle, false)), "towards the river");
        }
    }
}
