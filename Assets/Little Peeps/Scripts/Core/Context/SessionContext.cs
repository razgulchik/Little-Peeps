using UnityEngine;

// Runtime-only state; never saved. Cleared on prestige or scene reload.
public class SessionContext
{
    public UnitPool unitPool;
    public Structure draggedStructure;
    public Vector2Int? hoveredCell;
}
