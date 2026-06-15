using UnityEngine;

// Manages building drag-and-drop in BuildMode; driven by input from BuildModeState
public class DragController : MonoBehaviour
{
    [SerializeField] private BuildingSystem buildingSystem;
    [SerializeField] private IslandSystem islandSystem;

    private SessionContext session;
    private Vector2Int pickupCell;

    public void Initialize(SessionContext session)
    {
        this.session = session;
    }

    // Disable building collider, record it in SessionContext as the dragged building
    public void OnPickup(Vector2Int cell)
    {
        // TODO: get BuildingInstance from islandSystem.Grid; session.draggedBuilding = instance.RuntimeObject
        // TODO: session.draggedBuilding.SetColliderEnabled(false); pickupCell = cell
    }

    // Move dragged building transform to follow mouse world position each frame
    public void OnDrag(Vector2 worldPos)
    {
        // TODO: session.draggedBuilding.transform.position = worldPos
        // TODO: session.hoveredCell = islandSystem.Grid.WorldToGrid(worldPos)
    }

    // Execute move command, re-enable collider, clear SessionContext
    public void OnDrop(Vector2Int targetCell)
    {
        // TODO: new MoveBuildingCmd(buildingSystem, pickupCell, targetCell).Execute()
        // TODO: session.draggedBuilding.SetColliderEnabled(true); session.draggedBuilding = null; session.hoveredCell = null
    }

    // Cancel drag: return building to its original cell
    public void OnCancel()
    {
        // TODO: set building position back to GridToWorld(pickupCell), SetColliderEnabled(true), clear session fields
    }
}
