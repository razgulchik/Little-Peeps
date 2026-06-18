using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Drives build-mode placement: a ghost preview follows the cursor (snapped + centered on the
// footprint), tinted green/red by whether the spot is buildable and affordable; clicking a valid
// cell places the structure and stays in placement mode for repeats. Active only between
// Begin()/End(), called by BuildModeState. Clicks over UI are ignored so panel cards don't place.
// The selected StructureDef is set by the BuildPanelUI via Select().
public class PlacementController : MonoBehaviour
{
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private StructureSystem structureSystem;
    [SerializeField] private ResourceSystem resourceSystem;
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GridOverlay gridOverlay;

    [Header("Ghost tint")]
    [SerializeField] private Color validColor = new Color(0.4f, 1f, 0.4f, 0.6f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.4f, 0.4f, 0.6f);

    private bool active;
    private StructureDef selected;
    private GameObject ghost;
    private SpriteRenderer ghostRenderer;

    private void OnEnable()  => inputHandler.OnWorldClick += OnWorldClick;
    private void OnDisable() => inputHandler.OnWorldClick -= OnWorldClick;

    // Called by BuildModeState.Enter. Show the overlay; the panel drives which structure is selected.
    public void Begin()
    {
        active = true;
        gridOverlay.Show();
    }

    // Called by BuildModeState.Exit. Tear down ghost + overlay.
    public void End()
    {
        active = false;
        ClearGhost();
        selected = null;
        gridOverlay.Hide();
    }

    // Choose which structure to place (Phase 3 panel will call this). Rebuilds the ghost preview.
    public void Select(StructureDef def)
    {
        selected = def;
        ClearGhost();
        if (def == null || def.prefab == null) return;

        var sourceSr = def.prefab.GetComponentInChildren<SpriteRenderer>();
        ghost = new GameObject("PlacementGhost");
        ghostRenderer = ghost.AddComponent<SpriteRenderer>();
        if (sourceSr != null)
        {
            ghostRenderer.sprite = sourceSr.sprite;
            ghostRenderer.sortingLayerID = sourceSr.sortingLayerID;
            ghostRenderer.sortingOrder = sourceSr.sortingOrder + 1;
            ghost.transform.localScale = sourceSr.transform.lossyScale;
        }
    }

    private void Update()
    {
        if (!active || selected == null || ghost == null) return;

        var grid = islandSystem.Grid;
        Vector2 cursor = ScreenToWorld();
        Vector2Int origin = grid.WorldToOrigin(cursor, selected.size);

        // Same placement rule as a real structure — the builder owns it (ghost matches exactly).
        structureSystem.CenterSpriteOnFootprint(ghost.transform, ghostRenderer, origin, selected.size);

        bool ok = grid.CanPlace(origin, selected.size, selected.allowedTerrain)
                  && resourceSystem.CanAfford(selected.cost);
        ghostRenderer.color = ok ? validColor : invalidColor;
    }

    private void OnWorldClick(Vector2 worldPos)
    {
        if (!active || selected == null) return;
        // Ignore clicks over UI (panel cards / build button) so they don't drop a structure.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        var grid = islandSystem.Grid;
        Vector2Int origin = grid.WorldToOrigin(worldPos, selected.size);

        if (!grid.CanPlace(origin, selected.size, selected.allowedTerrain)) return; // bad cell — ghost is already red
        if (!resourceSystem.CanAfford(selected.cost))
        {
            EventBus<BuildDeniedEvent>.Publish(new BuildDeniedEvent { Def = selected });
            return;
        }
        structureSystem.PlaceStructure(selected, origin);
    }

    private Vector2 ScreenToWorld()
    {
        Vector2 screen = Mouse.current.position.ReadValue();
        return mainCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
    }

    private void ClearGhost()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
        ghostRenderer = null;
    }
}
