using UnityEngine;

// Manages structure drag-and-drop in BuildMode; driven by input from BuildModeState
public class DragController : MonoBehaviour
{
    [SerializeField] private StructureSystem buildingSystem;
    [SerializeField] private IslandSystem islandSystem;

    private SessionContext session;
    private Vector2Int pickupCell;

    public void Initialize(SessionContext session)
    {
        this.session = session;
    }

    // Disable structure collider, record it in SessionContext as the dragged structure
    public void OnPickup(Vector2Int cell)
    {
        // TODO: get StructureInstance from islandSystem.Grid; session.draggedStructure = instance.RuntimeObject
        // TODO: session.draggedStructure.SetColliderEnabled(false); pickupCell = cell
    }

    // Move dragged structure transform to follow mouse world position each frame
    public void OnDrag(Vector2 worldPos)
    {
        // TODO: session.draggedStructure.transform.position = worldPos
        // TODO: session.hoveredCell = islandSystem.Grid.WorldToGrid(worldPos)
    }

    // Execute move command, re-enable collider, clear SessionContext
    public void OnDrop(Vector2Int targetCell)
    {
        // TODO: new MoveStructureCmd(buildingSystem, pickupCell, targetCell).Execute()
        // TODO: session.draggedStructure.SetColliderEnabled(true); session.draggedStructure = null; session.hoveredCell = null
    }

    // Cancel drag: return structure to its original cell
    public void OnCancel()
    {
        // TODO: set structure position back to GridToWorld(pickupCell), SetColliderEnabled(true), clear session fields
    }
}
