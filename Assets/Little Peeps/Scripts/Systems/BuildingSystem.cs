using UnityEngine;

// Handles placing, removing, and moving buildings; validates against IslandGrid
public class BuildingSystem : MonoBehaviour
{
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private ResourceSystem resourceSystem;

    // Instantiate building from def at cell if placement is valid; return false if not
    public bool PlaceBuilding(BuildingDef def, Vector2Int cell)
    {
        // TODO: if !islandSystem.Grid.CanPlace(cell, def.size, def.allowedTerrain) return false
        // TODO: deduct def.cost from resourceSystem; instantiate def.prefab at GridToWorld(cell)
        // TODO: create BuildingInstance, call grid.Place, publish BuildingPlacedEvent
        return false;
    }

    // Destroy building at cell and free its grid cells
    public void RemoveBuilding(Vector2Int cell)
    {
        // TODO: get BuildingInstance from grid; Destroy(instance.RuntimeObject.gameObject); grid.Remove; publish BuildingRemovedEvent
    }

    // Move building from one cell to another; validate destination first
    public bool MoveBuilding(Vector2Int from, Vector2Int to)
    {
        // TODO: get instance; grid.Move(from, instance.Def.size, to); update transform; return success
        return false;
    }
}
