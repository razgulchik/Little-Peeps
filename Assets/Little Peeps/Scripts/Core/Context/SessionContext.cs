using UnityEngine;

// Runtime-only state; never saved. Cleared on prestige or scene reload.
public class SessionContext
{
    public UnitPool unitPool;
    public Building draggedBuilding;
    public Vector2Int? hoveredCell;
}
