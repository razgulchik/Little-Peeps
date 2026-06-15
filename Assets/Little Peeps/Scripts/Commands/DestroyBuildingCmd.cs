using UnityEngine;

// Permanently remove a building; may refund a portion of its cost
public class DestroyBuildingCmd : ICommand
{
    private readonly BuildingSystem buildingSystem;
    private readonly ResourceSystem resourceSystem;
    private readonly Vector2Int cell;

    public DestroyBuildingCmd(BuildingSystem buildingSystem, ResourceSystem resourceSystem, Vector2Int cell)
    {
        this.buildingSystem = buildingSystem;
        this.resourceSystem = resourceSystem;
        this.cell = cell;
    }

    public bool CanExecute()
    {
        // TODO: check that a building occupies this cell via IslandGrid.GetCell(cell).occupant != null
        return true;
    }

    public void Execute()
    {
        // TODO: optionally refund partial cost to resourceSystem; buildingSystem.RemoveBuilding(cell)
    }
}
