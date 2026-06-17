using UnityEngine;

// Handles placing, removing, and moving structures; validates against IslandGrid
public class StructureSystem : MonoBehaviour
{
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private ResourceSystem resourceSystem;

    // Instantiate structure from def at cell if placement is valid; return false if not
    public bool PlaceStructure(StructureDef def, Vector2Int cell)
    {
        // TODO: if !islandSystem.Grid.CanPlace(cell, def.size, def.allowedTerrain) return false
        // TODO: deduct def.cost from resourceSystem; instantiate def.prefab at GridToWorld(cell)
        // TODO: create StructureInstance, call grid.Place, publish StructurePlacedEvent
        return false;
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
