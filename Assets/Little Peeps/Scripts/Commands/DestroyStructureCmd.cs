using UnityEngine;

// Permanently remove a structure; may refund a portion of its cost
public class DestroyStructureCmd : ICommand
{
    private readonly StructureSystem structureSystem;
    private readonly ResourceSystem resourceSystem;
    private readonly Vector2Int cell;

    public DestroyStructureCmd(StructureSystem structureSystem, ResourceSystem resourceSystem, Vector2Int cell)
    {
        this.structureSystem = structureSystem;
        this.resourceSystem = resourceSystem;
        this.cell = cell;
    }

    public bool CanExecute()
    {
        // TODO: check that a structure occupies this cell via IslandGrid.GetCell(cell).occupant != null
        return true;
    }

    public void Execute()
    {
        // TODO: optionally refund partial cost to resourceSystem; structureSystem.RemoveStructure(cell)
    }
}
