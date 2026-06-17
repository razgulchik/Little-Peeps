using UnityEngine;

// Move an existing structure to a different grid cell (no resource cost)
public class MoveStructureCmd : ICommand
{
    private readonly StructureSystem structureSystem;
    private readonly Vector2Int from;
    private readonly Vector2Int to;

    public MoveStructureCmd(StructureSystem structureSystem, Vector2Int from, Vector2Int to)
    {
        this.structureSystem = structureSystem;
        this.from = from;
        this.to = to;
    }

    public bool CanExecute()
    {
        // TODO: validate destination via structureSystem / IslandGrid.CanPlace for the structure's size and terrain
        return true;
    }

    public void Execute()
    {
        // TODO: structureSystem.MoveStructure(from, to)
    }
}
