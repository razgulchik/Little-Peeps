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

        // A prefab can't serialize scene-system references, so inject them before the new
        // components' Start runs (Build runs in GameBootstrap.Awake → injection happens first).
        if (go.TryGetComponent<Spawner>(out var spawner)) spawner.Initialize(spawnSystem);
        if (go.TryGetComponent<ResourceSource>(out var source)) source.Initialize(resourceSystem);

        // Center the sprite on its footprint (shared rule — the placement ghost uses the same call).
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) CenterSpriteOnFootprint(go.transform, sr, cell, def.size);

        var instance = new StructureInstance { Def = def, RuntimeObject = structure, Cell = cell };
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
}
