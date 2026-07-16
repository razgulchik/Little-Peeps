using UnityEngine;

// Owns the pier for a run: a Structure that always sits in the island's BOTTOM-RIGHT corner and rides
// along as the island grows. RunManager places it once at run start; from then on it moves itself to
// the new corner on every age expansion, reusing StructureSystem's pick-up/drop path so grid occupancy
// stays correct. Expansion happens behind the age transition's black screen, so the move is invisible.
//
// The pier is NOT part of StartingLayoutDef — this system is the single owner, so its cell is always
// the live right edge rather than a hard-coded coordinate.
public class PierSystem : MonoBehaviour
{
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private StructureSystem structureSystem;
    [Tooltip("StructureDef for the pier. Use border = 0 (a border would demand cells past the island " +
             "edge, so it could never place) and a size whose height the ages' right-edge growth covers.")]
    [SerializeField] private StructureDef pierDef;

    private StructureInstance pier;

    private void OnEnable()  => EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);
    private void OnDisable() => EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);

    // Called by RunManager after the island + starting structures exist. Places the pier at the
    // current right edge and remembers it for the rest of the run. A null result (no room, already
    // warned by PlaceInitial) just leaves the pier absent — OnAgeStarted then no-ops.
    public void PlaceForRun()
    {
        pier = null;
        if (!TryComputeSlot(out var origin)) return;
        pier = structureSystem.PlaceInitial(pierDef, origin);
    }

    // The island grew this age → move the pier to the new bottom-right slot.
    private void OnAgeStarted(AgeStartedEvent _)
    {
        if (pier == null) return;
        if (!TryComputeSlot(out var origin)) return;

        // Free the old cells first so the new slot may overlap them (it usually does). PickUp keeps
        // pier.Cell unchanged, so we can put it back there if the new slot turns out invalid.
        structureSystem.PickUpStructure(pier);

        var grid = islandSystem.Grid;
        if (grid.CanPlace(origin, pierDef.size, pierDef.allowedTerrain, pierDef.border))
        {
            structureSystem.DropStructure(pier, origin);
        }
        else
        {
            structureSystem.DropStructure(pier, pier.Cell);
            Debug.LogWarning($"PierSystem: no room for the pier {pierDef.size} at the island's right " +
                             $"edge (slot {origin}) — kept in place. Grow the right edge tall enough " +
                             $"in this age's expansionBlocks.", this);
        }
    }

    // Bottom-right slot for the pier's footprint: pinned to the rightmost columns, anchored to the
    // RIGHTMOST COLUMN'S own lowest existing cell — not the whole island's, since a stepped shoreline
    // can leave the right side higher than the left. CanPlace (in the callers) then validates the full
    // footprint against a ragged edge. Returns false only for an empty grid.
    private bool TryComputeSlot(out Vector2Int origin)
    {
        origin = default;
        var grid = islandSystem != null ? islandSystem.Grid : null;
        if (grid == null || pierDef == null) return false;
        if (!grid.CellBounds(out var min, out var max)) return false;

        int py = max.y;
        for (int y = min.y; y <= max.y; y++)
            if (grid.GetCell(new Vector2Int(max.x, y)) != null) { py = y; break; }

        origin = new Vector2Int(max.x - pierDef.size.x + 1, py);
        return true;
    }
}
