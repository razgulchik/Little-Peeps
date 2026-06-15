using UnityEngine;

// Place a new building on the grid; deducts resource cost on Execute
public class PlaceBuildingCmd : ICommand
{
    private readonly BuildingSystem buildingSystem;
    private readonly ResourceSystem resourceSystem;
    private readonly BuildingDef def;
    private readonly Vector2Int cell;

    public PlaceBuildingCmd(BuildingSystem buildingSystem, ResourceSystem resourceSystem, BuildingDef def, Vector2Int cell)
    {
        this.buildingSystem = buildingSystem;
        this.resourceSystem = resourceSystem;
        this.def = def;
        this.cell = cell;
    }

    public bool CanExecute()
    {
        // TODO: buildingSystem.CanPlace check + verify all def.cost entries affordable via resourceSystem.GetResource
        return true;
    }

    public void Execute()
    {
        // TODO: deduct each def.cost entry from resourceSystem; buildingSystem.PlaceBuilding(def, cell)
    }
}
