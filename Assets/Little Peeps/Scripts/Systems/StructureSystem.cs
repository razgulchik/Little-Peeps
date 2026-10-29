using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Handles placing and removing structures; validates against IslandGrid. Both happen in the running
    // game — building does not stop it — so the player's placements go up as CONSTRUCTION SITES that turn
    // into the building once nobody stands on the spot (ConstructionSite), and a removal also settles
    // whoever the structure was holding (see RemoveStructure). Generated content (a zone's trees, the
    // starting house, the pier) is built at once: it lands before anyone walks there.
    public class StructureSystem : MonoBehaviour
    {
        [SerializeField] private IslandSystem islandSystem;
        [SerializeField] private ResourceSystem resourceSystem;
        [SerializeField] private SpawnSystem spawnSystem;

        [Header("Construction site")]
        [Tooltip("How a site looks until it has art of its own.")]
        [SerializeField] private ConstructionSiteLook siteLook = new();

        private RunContext run;
        private readonly List<Rect> siteArea = new();   // reused per placement: the rects a new site covers

        // Injected by RunManager.StartNewRun so placement targets the current run's structure set.
        public void Initialize(RunContext run)
        {
            this.run = run;
        }

        // Player-driven placement. Validates the cell, the build limit and affordability, charges the
        // price, then puts up a construction site. Returns false (no-op) if the cell is blocked, the limit
        // is reached or the price can't be paid.
        //
        // The site holds everything the building will from this moment: its cells, its slot in the count
        // (so the next one already costs more and the limit already counts it), and a sale refunds it like
        // a finished one. Only the object differs, until CompleteSite swaps it for the real prefab.
        public bool PlaceStructure(StructureDef def, Vector2Int cell)
        {
            if (!islandSystem.Grid.CanPlace(cell, def)) return false;
            if (AtLimit(def) || !CanAfford(def)) return false;
            resourceSystem.Spend(def, run.CountOf(def));

            var grid = islandSystem.Grid;
            var instance = new StructureInstance { Def = def, Cell = cell, UnderConstruction = true };
            ConstructionArea.CellRects(grid, cell, def.Footprint, siteArea);
            instance.RuntimeObject = ConstructionSite.Create(
                def, grid.OriginToWorldAnchor(cell, def.size), siteArea,
                copy => PoseOnCell(copy, cell, def), siteLook, spawnSystem, clearOfAnimals: true,
                () => CompleteSite(instance));
            Register(instance);
            return true;
        }

        // Nobody stands on the site any more: the real building replaces it, on the same instance — the
        // grid, the run and the count already hold it, so only the object changes. Never called for a
        // site that was sold or swept with its run: the site object is gone, and with it the callback.
        private void CompleteSite(StructureInstance instance)
        {
            var site = instance.RuntimeObject;
            instance.RuntimeObject = CreateStructure(instance);
            instance.UnderConstruction = false;
            RefreshBesideWater(instance);
            if (site != null) Destroy(site.gameObject);
        }

        // The run-dependent half of "can I build another": as many stand as the run allows. Placement asks
        // it on every click — the palette locks the card too, but the tool stays in hand between clicks.
        public bool AtLimit(StructureDef def) => def.AtLimit(run);

        // Whether the NEXT one can be paid: its price grows with how many already stand.
        public bool CanAfford(StructureDef def) => resourceSystem.CanAfford(def, run != null ? run.CountOf(def) : 0);

        // The same two questions for several built in a row (a drawn fence): how many of the next `wanted`
        // fit under the build limit, and how many of those the player can pay for, each at its own growing
        // price. The two apart, because only running out of money is reported (BuildDeniedEvent).
        public int RoomUnderLimit(StructureDef def, int wanted)
        {
            if (!def.HasLimit || run == null) return wanted;
            return Mathf.Clamp(def.LimitIn(run) - run.CountOf(def), 0, wanted);
        }

        public int AffordableCount(StructureDef def, int wanted) =>
            resourceSystem.AffordableCount(def, run != null ? run.CountOf(def) : 0, wanted);

        // Free placement for generated structures (a zone's natural content, the pier): no cost, but
        // still validated. Returns the placed instance, or null when the cell is blocked — the generator
        // keeps its own cells clear, so a failure here is a data error (a def's terrain or border rule
        // fighting the biome), so warn and skip rather than corrupt the grid.
        public StructureInstance PlaceInitial(StructureDef def, Vector2Int cell)
        {
            if (!islandSystem.Grid.CanPlace(cell, def))
            {
                Debug.LogWarning($"StructureSystem: cannot place '{def.id}' at {cell} (out of bounds, occupied, wrong terrain, border overlap, a fence across it, or no side along water) — skipped.", this);
                return null;
            }
            return Build(def, cell);
        }

        // Build at once: create the object and register it. Generated content only (PlaceInitial) — the
        // player's placements go through a site. Returns the created instance so callers that need to
        // track it later (e.g. the pier) can hold on to it.
        private StructureInstance Build(StructureDef def, Vector2Int cell)
        {
            var instance = new StructureInstance { Def = def, Cell = cell };
            instance.RuntimeObject = CreateStructure(instance);
            Register(instance);
            return instance;
        }

        // Instantiate the instance's prefab on its spot and wire it up — the object half of a structure,
        // shared by Build and CompleteSite. Grid, run and count are Register's.
        private Structure CreateStructure(StructureInstance instance)
        {
            var grid = islandSystem.Grid;
            var def = instance.Def;
            var cell = instance.Cell;

            // Born at its footprint anchor (shared rule — the placement ghost uses the same point), so
            // anything reading its position in Awake already sees the final spot.
            var worldPos = grid.OriginToWorldAnchor(cell, def.size);
            var go = Instantiate(def.prefab, worldPos, Quaternion.identity);
            var structure = go.GetComponent<Structure>();
            structure.def = def;
            structure.Initialize(resourceSystem);

            // A prefab can't serialize scene-system references, so inject them before the new components'
            // Start runs. The instance is the authoritative record of the structure's grid origin; a spawner
            // holds it to read its Cell when choosing a launch direction. GetComponentsInChildren (not
            // TryGetComponent) so composite prefabs work too: a forest is a root Structure whose child trees
            // each carry a ResourceSource — inject every one, at any depth.
            foreach (var spawner in go.GetComponentsInChildren<Spawner>(true)) spawner.Initialize(spawnSystem, grid, instance);
            foreach (var source in go.GetComponentsInChildren<ResourceSource>(true)) source.Initialize(resourceSystem);
            foreach (var animalSpawner in go.GetComponentsInChildren<AnimalSpawner>(true)) animalSpawner.Initialize(spawnSystem, resourceSystem, grid, instance);
            foreach (var tavern in go.GetComponentsInChildren<Tavern>(true)) tavern.Initialize(resourceSystem, grid, instance);

            // A forest picks its interlocking layout by the row it lands on, a watermill its art by the river,
            // a river cell its piece by the river beside it.
            ApplyPlacementVisual(go, grid, cell, def.Footprint);
            return structure;
        }

        // A site's blueprint is read from a copy of the prefab posed exactly as CreateStructure poses the
        // real one, so what is sketched on the ground is what will stand there.
        private void PoseOnCell(GameObject copy, Vector2Int cell, StructureDef def)
        {
            AnchorOnFootprint(copy.transform, cell, def.size);
            ApplyPlacementVisual(copy, islandSystem.Grid, cell, def.Footprint);
        }

        // Claim the instance's cells and count it — from the site's first moment, not the building's.
        private void Register(StructureInstance instance)
        {
            islandSystem.Grid.Place(instance.Cell, instance.Def.Footprint, instance);
            run.structures[instance.Cell] = instance;
            RefreshBesideWater(instance);
            Recount(instance.Def, +1);
        }

        // The one writer of RunContext.standing. Paired with Register/RegisterEdge and Remove*. Announced so the
        // build palette can show the new price and count.
        private void Recount(StructureDef def, int delta)
        {
            int count = Mathf.Max(0, run.CountOf(def) + delta);
            run.standing[def] = count;
            EventBus<StructureCountChangedEvent>.Publish(new StructureCountChangedEvent { Def = def, Count = count });
        }

        // Put a structure's ROOT at its footprint anchor: the bottom-center of the footprint. Prefab art is
        // pivoted at its base, so the sprite stands on the footprint's bottom edge and rises from there;
        // everything inside the prefab (sprite children, the Physics collider) is authored relative to
        // that base. Any visual offset baked into the prefab (a child's local position) is preserved — we
        // move the root only — so per-prefab art can be nudged by hand without the placement code
        // fighting it. Moving the root (not the sprite child) also keeps the sprite and collider in sync,
        // critical for the physics/bounce gameplay. Shared by Build and the placement ghost
        // (PlacementVisuals) so the preview matches exactly. Grid occupancy is logical (by cell),
        // unaffected by this.
        public void AnchorOnFootprint(Transform root, Vector2Int origin, Vector2Int size)
        {
            Vector2 anchor = islandSystem.Grid.OriginToWorldAnchor(origin, size);
            root.position = new Vector3(anchor.x, anchor.y, root.position.z);
        }

        // The look a structure takes from WHERE it stands, set whenever it lands:
        //   - a forest carries a DualVisual whose two roots interlock by grid row: even rows show the first
        //     layout, odd rows the second, so adjacent forests form a brick-laid pattern. (row & 1) is
        //     correct for negative rows too on the signed grid;
        //   - a watermill carries a WaterSideVisual and shows the art for the side the river runs along —
        //     the south art mirrored where the river stops under its wheel;
        //   - a river cell carries a RiverVisual and shows the piece that joins it to the river cells beside it.
        // No-op for structures with none. Shared by Build and the placement ghost (PlacementVisuals), so
        // the preview shows exactly what lands — and by RefreshBesideWater, since the last two depend on
        // the neighbours.
        public static void ApplyPlacementVisual(GameObject go, IslandGrid grid, Vector2Int origin, Footprint footprint)
        {
            if (go.TryGetComponent<DualVisual>(out var rows)) rows.Show((origin.y & 1) == 0);
            if (go.TryGetComponent<WaterSideVisual>(out var sides))
                sides.Show(WaterSides.Find(grid, origin, footprint), WaterSides.FlipSouth(grid, origin, footprint));
            if (go.TryGetComponent<RiverVisual>(out var river)) river.Show(RiverShapes.At(grid, origin));
        }

        // Water decides the look of what stands beside it — a river cell's piece, a watermill's side — so
        // whenever a water structure lands on the grid or leaves it, everything around it takes its look
        // again. A river laid cell by cell comes out joined whatever the order, and a cell taken out leaves
        // ends behind. Reads the grid as it is now: call after the Place or Remove. No-op for anything
        // that isn't water.
        private void RefreshBesideWater(StructureInstance changed)
        {
            if (!changed.Def.isWater) return;

            var grid = islandSystem.Grid;
            var footprint = changed.Def.Footprint;
            for (int x = 0; x < footprint.Size.x; x++)
                for (int y = 0; y < footprint.Size.y; y++)
                {
                    if (!footprint.Contains(x, y)) continue;
                    var cell = changed.Cell + new Vector2Int(x, y);
                    foreach (var d in IslandShape.Dirs)
                    {
                        var neighbour = grid.GetCell(cell + d)?.occupant;
                        if (neighbour == null || neighbour == changed || neighbour.RuntimeObject == null) continue;
                        ApplyPlacementVisual(neighbour.RuntimeObject.gameObject, grid, neighbour.Cell, neighbour.Def.Footprint);
                    }
                }
        }

        // Sell the structure occupying `cell` (any footprint cell): refund a fraction of its build
        // cost, then remove it. Returns false if nothing is there. Applies to ANY structure
        // (trees, starting buildings, player-built) — there is no playerPlaced distinction.
        public bool SellStructure(Vector2Int cell)
        {
            var instance = islandSystem.Grid.GetCell(cell)?.occupant;
            if (instance == null) return false;
            RefundCost(instance.Def);
            return RemoveStructure(instance.Cell);
        }

        // Remove the structure occupying `cell` (any footprint cell): free its grid cells, drop it
        // from the run, announce it, settle whoever it was holding, and destroy the GameObject. Returns
        // false if nothing is there.
        public bool RemoveStructure(Vector2Int cell)
        {
            var grid = islandSystem.Grid;
            var instance = grid.GetCell(cell)?.occupant;
            if (instance == null) return false;

            grid.Remove(instance.Cell, instance.Def.Footprint);
            run.structures.Remove(instance.Cell);
            RefreshBesideWater(instance);
            Recount(instance.Def, -1);
            ReleaseOccupants(instance.RuntimeObject);
            Destroy(instance.RuntimeObject.gameObject);
            return true;
        }

        // A structure leaving a LIVE run — the units and animals around it keep going, so nothing it held
        // may be left pointing at a building that is gone. (A run teardown is the other way out, see
        // ClearAll: everything goes at once there, so none of this applies.)
        //   - a tavern lets its guests out where it stood (Tavern.EvictAll);
        //   - a den's animals go in a puff — they belong to the den, and only it lets them out;
        //   - every spawner hands its books back NOW rather than in the deferred OnDestroy (resting units
        //     back to the pool, capacity given back, unregistered), so that
        //   - SpawnSystem.TrimSurplus sees the room that is left and takes the residents a sold house
        //     leaves without a home, nearest first, in a puff.
        // Composite prefabs carry these at any depth, like everything Build injects.
        private void ReleaseOccupants(Structure structure)
        {
            if (structure == null) return;

            foreach (var tavern in structure.GetComponentsInChildren<Tavern>(true))
                tavern.EvictAll();

            if (spawnSystem != null)
                foreach (var den in structure.GetComponentsInChildren<AnimalSpawner>(true))
                    for (int i = 0; i < den.Animals.Count; i++)
                        if (den.Animals[i] != null) spawnSystem.Puff(den.Animals[i].transform.position);

            foreach (var spawner in structure.GetComponentsInChildren<IStructureSpawner>(true))
                spawner.Teardown();

            if (spawnSystem != null) spawnSystem.TrimSurplus(structure.transform.position);
        }

        // Refund sellRefundPercent of each cost entry to the player. Free structures (no cost) refund 0.
        // The price refunded is the one the sold structure would cost to build AGAIN — the price with one
        // fewer standing, i.e. what the last one bought cost — so a build → sell cycle always loses
        // (1 - sellRefundPercent) and no stored "price paid" is needed. Called before the removal, while
        // the sold one is still counted.
        private void RefundCost(StructureDef def)
        {
            if (def.cost == null) return;
            int rebuiltAt = Mathf.Max(0, run.CountOf(def) - 1);
            for (int i = 0; i < def.cost.Count; i++)
                resourceSystem.AddResource(def.cost[i].resourceType, def.CostAt(i, rebuiltAt) * def.sellRefundPercent);
        }

        // --- Edge-placed structures (fences) ------------------------------------------------------
        // Parallel to the cell placement path above, but addressed by Edge. Shares the same validation +
        // cost + event flow, and the same site stage; only the grid addressing and the H/V pose differ.

        // Player-driven fence placement. Validates the edge, the build limit and affordability, charges the
        // price, then puts up a construction site: a strip along the edge that the units standing on the
        // line walk out of before the fence goes up. Returns false (no-op) if the edge is blocked, the
        // limit is reached or the price can't be paid.
        public bool PlaceEdgeStructure(StructureDef def, Edge edge)
        {
            if (!islandSystem.Grid.CanPlaceEdge(edge)) return false;
            if (AtLimit(def) || !CanAfford(def)) return false;
            resourceSystem.Spend(def, run.CountOf(def));

            var grid = islandSystem.Grid;
            var instance = new EdgeInstance { Def = def, Edge = edge };
            siteArea.Clear();
            siteArea.Add(ConstructionArea.EdgeRect(grid, edge, siteLook.fenceThickness * grid.CellSize));
            instance.RuntimeObject = ConstructionSite.Create(
                def, grid.EdgeToWorld(edge), siteArea, copy => PoseOnEdge(copy, edge), siteLook, spawnSystem,
                clearOfAnimals: false,   // animals pass fences, finished or not — nothing to wait for
                () => CompleteEdgeSite(instance));
            RegisterEdge(instance);
            return true;
        }

        // Mirror of CompleteSite for a fence.
        private void CompleteEdgeSite(EdgeInstance instance)
        {
            var site = instance.RuntimeObject;
            instance.RuntimeObject = CreateEdgeStructure(instance);
            if (site != null) Destroy(site.gameObject);
        }

        // Instantiate the fence prefab on the edge and give it the pose (horizontal / vertical child)
        // matching the edge it sits on. The object half; mirror of CreateStructure.
        private Structure CreateEdgeStructure(EdgeInstance instance)
        {
            var go = Instantiate(instance.Def.prefab, islandSystem.Grid.EdgeToWorld(instance.Edge), Quaternion.identity);
            var structure = go.GetComponent<Structure>();
            structure.def = instance.Def;
            structure.Initialize(resourceSystem);
            if (go.TryGetComponent<DualVisual>(out var visual)) visual.Show(instance.Edge.horizontal);
            return structure;
        }

        // The fence blueprint's copy, posed like CreateEdgeStructure poses the real one.
        private void PoseOnEdge(GameObject copy, Edge edge)
        {
            copy.transform.position = islandSystem.Grid.EdgeToWorld(edge);
            if (copy.TryGetComponent<DualVisual>(out var visual)) visual.Show(edge.horizontal);
        }

        // Claim the edge and count the fence — from the site's first moment. Mirror of Register.
        private void RegisterEdge(EdgeInstance instance)
        {
            islandSystem.Grid.PlaceEdge(instance.Edge, instance);
            run.fences[instance.Edge] = instance;
            Recount(instance.Def, +1);
        }

        // Sell the fence on `edge`: refund a fraction of its cost, then remove it. False if none there.
        public bool SellEdgeStructure(Edge edge)
        {
            var instance = islandSystem.Grid.GetEdge(edge);
            if (instance == null) return false;
            RefundCost(instance.Def);
            return RemoveEdgeStructure(edge);
        }

        // Remove the fence on `edge`: free the grid edge, drop it from the run, announce it, destroy the
        // GameObject. Returns false if nothing is there.
        public bool RemoveEdgeStructure(Edge edge)
        {
            var grid = islandSystem.Grid;
            var instance = grid.GetEdge(edge);
            if (instance == null) return false;

            grid.RemoveEdge(edge);
            run.fences.Remove(edge);
            Recount(instance.Def, -1);
            Destroy(instance.RuntimeObject.gameObject);
            return true;
        }

        // --- Run teardown ---------------------------------------------------------------------------

        // Remove EVERYTHING this run built: free the grid, tear the spawners down synchronously, destroy
        // the objects, empty the run's registries. Called by RunManager.EndRun; no refunds, no events —
        // the run itself is going away.
        //
        // The explicit Teardown before each Destroy is the whole point. Destroy() defers OnDestroy to the
        // end of the frame, while a prestige ends one run and starts the next inside a single frame, so
        // the old spawners would give their capacity back AFTER the new run had already claimed its own.
        // With unequal totals (the usual case — the player built extra houses) the fresh run's cap ends
        // up clamped at zero and the village never spawns anybody, and the deferred callbacks also push
        // already-pooled units back into UnitPool a second time.
        //
        // Grid cells are freed even though IslandSystem is about to throw the whole grid away: EndRun has
        // to be correct on its own, not only when a StartNewRun happens to follow it.
        //
        // run.structures + run.fences are the complete picture: every structure that exists is in one of
        // them from the moment it is built until it is removed — nothing is ever held off the grid.
        public void ClearAll()
        {
            if (run == null) return;
            var grid = islandSystem != null ? islandSystem.Grid : null;

            foreach (var instance in run.structures.Values)
            {
                if (grid != null) grid.Remove(instance.Cell, instance.Def.Footprint);
                DestroyWithTeardown(instance.RuntimeObject);
            }
            run.structures.Clear();

            foreach (var instance in run.fences.Values)
            {
                if (grid != null) grid.RemoveEdge(instance.Edge);
                DestroyWithTeardown(instance.RuntimeObject);
            }
            run.fences.Clear();

            run.standing.Clear();   // silently, like the rest of the sweep
        }

        // Composite prefabs carry spawners at any depth (a forest is a root Structure over child trees),
        // so this mirrors the injection loop in Build and reaches every one of them.
        private static void DestroyWithTeardown(Structure structure)
        {
            if (structure == null) return;

            foreach (var spawner in structure.GetComponentsInChildren<IStructureSpawner>(true))
                spawner.Teardown();

            Destroy(structure.gameObject);
        }
    }
}
