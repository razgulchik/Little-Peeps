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
        if (!islandSystem.Grid.CanPlace(cell, def.size, def.allowedTerrain)) return false;
        if (!resourceSystem.CanAfford(def.cost)) return false;
        resourceSystem.Spend(def.cost);
        Build(def, cell);
        return true;
    }

    // Free placement for run-start structures (StartingLayoutDef): no cost, but still validated.
    // A failing entry is a data error in the layout — warn and skip rather than corrupt the grid.
    public void PlaceInitial(StructureDef def, Vector2Int cell)
    {
        if (!islandSystem.Grid.CanPlace(cell, def.size, def.allowedTerrain))
        {
            Debug.LogWarning($"StartingLayout: cannot place '{def.id}' at {cell} (out of bounds, occupied, or wrong terrain) — skipped.", this);
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

    // Destroy structure at cell and free its grid cells
    public void RemoveStructure(Vector2Int cell)
    {
        // TODO: get StructureInstance from grid; Destroy(instance.RuntimeObject.gameObject); grid.Remove; publish StructureRemovedEvent
    }

    // Move structure from one cell to another; validate destination first
    public bool MoveStructure(Vector2Int from, Vector2Int to)
    {
        // TODO: get instance; grid.Move(from, instance.Def.size, to); update transform; return success
        return false;
    }
}
