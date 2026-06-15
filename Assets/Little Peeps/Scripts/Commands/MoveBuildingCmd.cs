using UnityEngine;

// Move an existing building to a different grid cell (no resource cost)
public class MoveBuildingCmd : ICommand
{
    private readonly BuildingSystem buildingSystem;
    private readonly Vector2Int from;
    private readonly Vector2Int to;

    public MoveBuildingCmd(BuildingSystem buildingSystem, Vector2Int from, Vector2Int to)
    {
        this.buildingSystem = buildingSystem;
        this.from = from;
        this.to = to;
    }

    public bool CanExecute()
    {
        // TODO: validate destination via buildingSystem / IslandGrid.CanPlace for the building's size and terrain
        return true;
    }

    public void Execute()
    {
        // TODO: buildingSystem.MoveBuilding(from, to)
    }
}
