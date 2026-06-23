using UnityEngine;

// Handles placing, removing, and moving structures; validates against IslandGrid
public class StructureSystem : MonoBehaviour
{
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private ResourceSystem resourceSystem;
    [SerializeField] private SpawnSystem spawnSystem;

    private RunContext run;

    // Injected by RunManager.StartNewRun so placement targets the current run's structure set.
    public void Initialize(RunContext run)
    {
        this.run = run;
    }

    // Player-driven placement. Validates the cell and affordability, charges the cost, then
    // builds. Returns false (no-op) if the cell is blocked or the cost can't be paid.
    public bool PlaceStructure(StructureDef def, Vector2Int cell)
    {
        if (!islandSystem.Grid.CanPlace(cell, def.size, def.allowedTerrain, def.border)) return false;
        if (!resourceSystem.CanAfford(def.cost)) return false;
        resourceSystem.Spend(def.cost);
        Build(def, cell);
        return true;
    }

    // Free placement for run-start structures (StartingLayoutDef): no cost, but still validated.
    // A failing entry is a data error in the layout — warn and skip rather than corrupt the grid.
    public void PlaceInitial(StructureDef def, Vector2Int cell)
    {
        if (!islandSystem.Grid.CanPlace(cell, def.size, def.allowedTerrain, def.border))
        {
            Debug.LogWarning($"StartingLayout: cannot place '{def.id}' at {cell} (out of bounds, occupied, wrong terrain, or border overlap) — skipped.", this);
            return;
        }
        Build(def, cell);
    }

    // Instantiate the prefab, register it in the grid + run, and announce it. Shared by every
    // placement path (starting layout now; interactive placement in 2.3).
    private Structure Build(StructureDef def, Vector2Int cell)
    {
        var grid = islandSystem.Grid;
        var worldPos = grid.OriginToWorldCenter(cell, def.size);

        var go = Instantiate(def.prefab, worldPos, Quaternion.identity);
        var structure = go.GetComponent<Structure>();
        structure.def = def;

        // The instance is the authoritative record of this structure's grid origin; build it now so a
        // spawner can hold it and read its (move-updated) Cell when choosing a launch direction.
        var instance = new StructureInstance { Def = def, RuntimeObject = structure, Cell = cell };

        // A prefab can't serialize scene-system references, so inject them before the new
        // components' Start runs (Build runs in GameBootstrap.Awake → injection happens first).
        if (go.TryGetComponent<Spawner>(out var spawner)) spawner.Initialize(spawnSystem, grid, instance);
        if (go.TryGetComponent<ResourceSource>(out var source)) source.Initialize(resourceSystem);

        // Center the sprite on its footprint (shared rule — the placement ghost uses the same call).
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) CenterSpriteOnFootprint(go.transform, sr, cell, def.size);

        grid.Place(cell, def.size, instance);
        run.structures[cell] = instance;

        EventBus<StructurePlacedEvent>.Publish(new StructurePlacedEvent { Structure = structure, Cell = cell });
        return structure;
    }

    // Position a structure so its sprite's geometric middle (bounds.center — pivot-independent,
    // accounts for pixel size / PPU / scale) sits at the footprint center. Moves the ROOT (not the
    // sprite child) so the sprite and the collider stay in sync — critical for the physics/bounce
    // gameplay. Shared by placed structures (Build) and the build-mode ghost (PlacementController)
    // so the preview matches exactly. Grid occupancy is logical (by cell), unaffected by the shift.
    public void CenterSpriteOnFootprint(Transform root, SpriteRenderer sr, Vector2Int origin, Vector2Int size)
    {
        Vector2 footprintCenter = islandSystem.Grid.OriginToWorldCenter(origin, size);
        Vector3 c = sr.bounds.center;
        root.position += new Vector3(footprintCenter.x - c.x, footprintCenter.y - c.y, 0f);
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
    // from the run, announce it, and destroy the GameObject. Returns false if nothing is there.
    public bool RemoveStructure(Vector2Int cell)
    {
        var grid = islandSystem.Grid;
        var instance = grid.GetCell(cell)?.occupant;
        if (instance == null) return false;

        grid.Remove(instance.Cell, instance.Def.size);
        run.structures.Remove(instance.Cell);
        // Publish before Destroy (deferred to end-of-frame) so listeners still see a live object.
        EventBus<StructureRemovedEvent>.Publish(new StructureRemovedEvent { Structure = instance.RuntimeObject, Cell = instance.Cell });
        Destroy(instance.RuntimeObject.gameObject);
        return true;
    }

    // Refund sellRefundPercent of each cost entry to the player. Free structures (no cost) refund 0.
    private void RefundCost(StructureDef def)
    {
        if (def.cost == null) return;
        for (int i = 0; i < def.cost.Count; i++)
            resourceSystem.AddResource(def.cost[i].resourceType, def.cost[i].amount * def.sellRefundPercent);
    }

    // Build-mode MOVE, step 1: lift a structure off the grid so it can be dragged. Frees its cells
    // and drops it from the run, but keeps the GameObject alive + active and its Cell unchanged (so
    // a cancel can put it back). Freeing the cells lets it be re-placed overlapping its own old
    // footprint. PlacementController drives the drag; DropStructure commits the destination.
    public void PickUpStructure(StructureInstance instance)
    {
        islandSystem.Grid.Remove(instance.Cell, instance.Def.size);
        run.structures.Remove(instance.Cell);
    }

    // Build-mode MOVE, step 2: place a lifted structure at origin (caller has checked CanPlace, or
    // is returning it to its original Cell on cancel). Re-registers grid + run, updates the
    // instance's Cell, and snaps the GameObject onto the footprint.
    public void DropStructure(StructureInstance instance, Vector2Int origin)
    {
        var grid = islandSystem.Grid;
        grid.Place(origin, instance.Def.size, instance);
        run.structures[origin] = instance;
        instance.Cell = origin;

        var sr = instance.RuntimeObject.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) CenterSpriteOnFootprint(instance.RuntimeObject.transform, sr, origin, instance.Def.size);
    }

    // --- Edge-placed structures (fences) ------------------------------------------------------
    // Parallel to the cell placement path above, but addressed by Edge. Shares the same validation +
    // cost + event flow; only the grid addressing and the H/V pose differ.

    // Player-driven fence placement. Validates the edge and affordability, charges the cost, builds.
    // Returns false (no-op) if the edge is blocked or the cost can't be paid.
    public bool PlaceEdgeStructure(StructureDef def, Edge edge)
    {
        if (!islandSystem.Grid.CanPlaceEdge(edge)) return false;
        if (!resourceSystem.CanAfford(def.cost)) return false;
        resourceSystem.Spend(def.cost);
        BuildEdge(def, edge);
        return true;
    }

    // Instantiate the fence prefab on the edge, set its orientation pose, register it in the grid +
    // run, and announce it. Mirror of Build for the cell path.
    private Structure BuildEdge(StructureDef def, Edge edge)
    {
        var grid = islandSystem.Grid;
        var worldPos = grid.EdgeToWorld(edge);

        var go = Instantiate(def.prefab, worldPos, Quaternion.identity);
        var structure = go.GetComponent<Structure>();
        structure.def = def;

        // Show the pose (horizontal / vertical child) matching the edge the fence sits on.
        if (go.TryGetComponent<EdgeStructureVisual>(out var visual)) visual.Apply(edge.horizontal);

        var instance = new EdgeInstance { Def = def, RuntimeObject = structure, Edge = edge };
        grid.PlaceEdge(edge, instance);
        run.fences[edge] = instance;

        // Fences have no single occupying cell, so they announce themselves by Edge (parallel event).
        EventBus<EdgeStructurePlacedEvent>.Publish(new EdgeStructurePlacedEvent { Structure = structure, Edge = edge });
        return structure;
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
        EventBus<EdgeStructureRemovedEvent>.Publish(new EdgeStructureRemovedEvent { Structure = instance.RuntimeObject, Edge = edge });
        Destroy(instance.RuntimeObject.gameObject);
        return true;
    }

    // Build-mode MOVE for fences, step 1: lift a fence off its edge so it can be dragged. Frees the
    // grid edge + run entry but keeps the GameObject alive and its Edge unchanged (so a cancel can put
    // it back). Mirror of PickUpStructure.
    public void PickUpEdgeStructure(EdgeInstance instance)
    {
        islandSystem.Grid.RemoveEdge(instance.Edge);
        run.fences.Remove(instance.Edge);
    }

    // Build-mode MOVE for fences, step 2: place a lifted fence on `edge` (caller has checked
    // CanPlaceEdge, or is returning it to its original Edge on cancel). Re-registers grid + run,
    // updates the instance's Edge, snaps the object onto the edge and sets the matching pose.
    // Mirror of DropStructure.
    public void DropEdgeStructure(EdgeInstance instance, Edge edge)
    {
        var grid = islandSystem.Grid;
        grid.PlaceEdge(edge, instance);
        run.fences[edge] = instance;
        instance.Edge = edge;

        instance.RuntimeObject.transform.position = grid.EdgeToWorld(edge);
        if (instance.RuntimeObject.TryGetComponent<EdgeStructureVisual>(out var visual)) visual.Apply(edge.horizontal);
    }
}
