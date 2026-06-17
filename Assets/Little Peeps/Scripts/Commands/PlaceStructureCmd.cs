using UnityEngine;

// Place a new structure on the grid; deducts resource cost on Execute
public class PlaceStructureCmd : ICommand
{
    private readonly StructureSystem structureSystem;
    private readonly ResourceSystem resourceSystem;
    private readonly StructureDef def;
    private readonly Vector2Int cell;

    public PlaceStructureCmd(StructureSystem structureSystem, ResourceSystem resourceSystem, StructureDef def, Vector2Int cell)
    {
        this.structureSystem = structureSystem;
        this.resourceSystem = resourceSystem;
        this.def = def;
        this.cell = cell;
    }

    public bool CanExecute()
    {
        // TODO: structureSystem.CanPlace check + verify all def.cost entries affordable via resourceSystem.GetResource
        return true;
    }

    public void Execute()
    {
        // TODO: deduct each def.cost entry from resourceSystem; structureSystem.PlaceStructure(def, cell)
    }
}
