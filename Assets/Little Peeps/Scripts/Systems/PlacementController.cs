using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Drives the active build-mode tool, chosen by the BuildPanelUI selection:
//  - a build card selected  → PLACE: a ghost preview follows the cursor (snapped + centered on the
//    footprint), tinted green/red by buildable + affordable; clicking a valid cell places it and
//    stays in placement mode for repeats.
//  - the sell button selected → SELL: clicking a placed structure sells it (refund + remove).
//  - nothing selected → MOVE: click a structure to lift it (the real object is dragged), click a
//    valid cell to drop it (free).
// Right-click cancels the current action (any tool): a Move drag returns to its origin, a Place/Sell
// selection is cleared back to Move (the ToolCleared event tells the panel to drop its highlight).
// Active only between Begin()/End(), called by BuildModeState. Clicks over UI are ignored so panel
// buttons don't act on the world. The selection is driven by BuildPanelUI via Select()/SetSellMode().
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
    [SerializeField] private Color sellHoverColor = new Color(1f, 0.4f, 0.4f, 0.6f);   // tint of the structure under the cursor in Sell mode
    [SerializeField] private Color moveHoverColor = new Color(0.4f, 1f, 0.4f, 0.6f);   // tint of a grabbable structure under the cursor in idle Move mode

    [Header("Territory halo (ghost)")]
    [SerializeField] private Color territoryValidColor = new Color(0.4f, 1f, 0.4f, 0.18f);
    [SerializeField] private Color territoryInvalidColor = new Color(1f, 0.4f, 0.4f, 0.18f);
    [SerializeField] private string territorySortingLayer = "Ground";   // same layer as the grid overlay (above the grass)
    [SerializeField] private int territorySortingOrder = 1001;

    // Raised when a right-click clears the active Place/Sell tool, so BuildPanelUI can drop its
    // card / sell-button highlight (the controller has already reset itself to the Move tool).
    public event Action ToolCleared;

    // Active tool, driven by the panel: a card → Place, the sell button → Sell, nothing → Move.
    private enum Tool { Move, Place, Sell }
    private Tool tool = Tool.Move;

    private bool active;
    private StructureDef selected;
    private GameObject ghost;
    private SpriteRenderer[] ghostRenderers;       // every renderer to tint (the prefab's sprites for a cell ghost, 2 poses for a fence)
    private DualVisual ghostVisual;                // non-null when the ghost is an edge structure (fence)
    private DualVisual ghostRowVisual;             // non-null when the cell ghost interlocks by row (forest)

    // Faint square showing the footprint+border the ghost would claim (Place and Move).
    private GameObject territoryGhost;
    private SpriteRenderer territoryRenderer;
    private static Sprite squareSprite;   // shared 1x1 white sprite the territory quad is scaled from

    // Hover tint (Sell target / grabbable Move target): the structure OR fence currently tinted. A cell
    // structure and a fence are never both hovered (the edge wins by NearEdge precedence), so a single
    // TintTarget holds whichever one's renderers + original colors.
    private StructureInstance hoveredInstance;
    private EdgeInstance hoveredEdge;
    private readonly TintTarget hoverTint = new();

    // Move-mode drag: the structure OR fence lifted off the grid and following the cursor. Only one is
    // ever held; heldEdgeVisual drives the fence's pose while dragging; heldTint holds its tint.
    private StructureInstance heldInstance;
    private EdgeInstance heldEdge;
    private DualVisual heldEdgeVisual;
    private readonly TintTarget heldTint = new();

    private void OnEnable()
    {
        inputHandler.OnWorldClick += OnWorldClick;
        inputHandler.OnWorldRightClick += OnWorldRightClick;
    }

    private void OnDisable()
    {
        inputHandler.OnWorldClick -= OnWorldClick;
        inputHandler.OnWorldRightClick -= OnWorldRightClick;
    }

    // Called by BuildModeState.Enter. Show the overlay; the panel drives which structure is selected.
    public void Begin()
    {
        active = true;
        gridOverlay.Show();
    }

    // Called by BuildModeState.Exit. Tear down ghost + overlay.
    public void End()
    {
        CancelMove();   // if mid-drag, return the structure to its origin before leaving
        active = false;
        tool = Tool.Move;
        ClearGhost();
        ClearHover();
        selected = null;
        gridOverlay.Hide();
    }

    // Choose which structure to place (BuildPanelUI calls this). Rebuilds the ghost preview.
    // A null def means "nothing selected" → the Move tool.
    public void Select(StructureDef def)
    {
        CancelMove();       // switching tools mid-drag returns the held structure to its origin
        selected = def;
        tool = def != null ? Tool.Place : Tool.Move;
        ClearGhost();
        ClearHover();   // leaving the previous tool — restore any tinted structure
        if (def == null || def.prefab == null) return;

        if (def.placement == PlacementKind.Edge) BuildEdgeGhost(def);
        else BuildCellGhost(def);
    }

    // Cell ghost: instantiate the real prefab so the preview matches the placed structure 1:1 —
    // including any sprite offset hand-tuned inside the prefab (placement centers the ROOT, so the
    // sprite child sits exactly where it will once built). Then neutralize it: disable every
    // behaviour (so Spawner/ResourceSource/Structure don't spawn units, register, or log) and every
    // collider/rigidbody so it's purely visual. Centered on the footprint each frame (UpdateCellGhost).
    private void BuildCellGhost(StructureDef def)
    {
        ghost = Instantiate(def.prefab);
        ghost.name = "PlacementGhost";

        foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        foreach (var col in ghost.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        foreach (var rb in ghost.GetComponentsInChildren<Rigidbody2D>(true)) rb.simulated = false;

        ghostRenderers = ghost.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in ghostRenderers) r.sortingOrder += 1;   // draw above the placed structures

        ghostRowVisual = ghost.GetComponent<DualVisual>();   // forest: the preview interlocks by row too
    }

    // Edge ghost (fence): instantiate the real prefab so the preview matches 1:1 (both poses), then
    // neutralize it — disable colliders + the Structure behaviour so it's purely visual. The active
    // pose and tint are set every frame in UpdateEdgeGhost.
    private void BuildEdgeGhost(StructureDef def)
    {
        ghost = Instantiate(def.prefab);
        ghost.name = "PlacementGhost";
        ghostVisual = ghost.GetComponent<DualVisual>();

        foreach (var col in ghost.GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
        if (ghost.TryGetComponent<Structure>(out var s)) s.enabled = false;

        ghostRenderers = ghost.GetComponentsInChildren<SpriteRenderer>(true);   // both poses (incl. inactive)
        foreach (var r in ghostRenderers) r.sortingOrder += 1;                  // draw above the real fences
    }

    // Switch to the Sell tool (BuildPanelUI's sell button calls this). No ghost in sell mode.
    public void SetSellMode()
    {
        CancelMove();
        selected = null;
        tool = Tool.Sell;
        ClearGhost();
        ClearHover();   // restart hover fresh so the next frame re-tints in the Sell color
    }

    private void Update()
    {
        if (!active) return;

        switch (tool)
        {
            case Tool.Place: UpdatePlaceGhost(); break;
            case Tool.Sell:  HideTerritory(); UpdateHover(sellHoverColor); break;   // red — "will be sold"
            case Tool.Move:
                if (heldInstance != null || heldEdge != null) UpdateMoveDrag();      // holding → drag it
                else { HideTerritory(); UpdateHover(moveHoverColor); }               // idle → green "grabbable" hint
                break;
        }
    }

    // Place tool: the ghost follows the cursor, tinted by buildable + affordable.
    private void UpdatePlaceGhost()
    {
        if (selected == null || ghost == null) return;
        if (selected.placement == PlacementKind.Edge) UpdateEdgeGhost();
        else UpdateCellGhost();
    }

    private void UpdateCellGhost()
    {
        var grid = islandSystem.Grid;
        Vector2 cursor = ScreenToWorld();
        Vector2Int origin = grid.WorldToOrigin(cursor, selected.size);

        // Same placement rule as a real structure — the builder owns it (ghost matches exactly).
        structureSystem.CenterOnFootprint(ghost.transform, origin, selected.size);
        if (ghostRowVisual != null) ghostRowVisual.Show((origin.y & 1) == 0);   // forest: preview the row's layout

        bool ok = grid.CanPlace(origin, selected.size, selected.allowedTerrain, selected.border)
                  && resourceSystem.CanAfford(selected.cost);
        TintGhost(ok ? validColor : invalidColor);
        ShowTerritory(origin, selected.size, selected.border, ok);
    }

    // Edge ghost (fence): snap to the nearest grid edge, sit on its midpoint, show the matching pose,
    // tint by placeable + affordable. No territory halo for edges in v1.
    private void UpdateEdgeGhost()
    {
        var grid = islandSystem.Grid;
        Edge edge = grid.WorldToEdge(ScreenToWorld());

        ghost.transform.position = grid.EdgeToWorld(edge);
        if (ghostVisual != null) ghostVisual.Show(edge.horizontal);

        bool ok = grid.CanPlaceEdge(edge) && resourceSystem.CanAfford(selected.cost);
        TintGhost(ok ? validColor : invalidColor);
        HideTerritory();
    }

    private void TintGhost(Color color) => Tint(ghostRenderers, color);

    // Color every renderer in the set (skipping nulls). Used to tint the multi-renderer place ghost,
    // which is destroyed rather than restored. (A hovered/dragged structure uses TintTarget instead,
    // which also remembers the originals so the tint can be undone.)
    private static void Tint(SpriteRenderer[] renderers, Color color)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = color;
    }

    // One tinted structure — a hover target or the dragged structure — bundling its renderers with the
    // colors they had before the tint, so the tint can be applied and undone as a unit. Works the same
    // for a cell structure (a forest is many trees) and a fence (its two poses): every renderer is
    // taken includeInactive so a hidden visual root is covered too. Shared by hoverTint and heldTint.
    private sealed class TintTarget
    {
        private SpriteRenderer[] renderers;
        private Color[] originalColors;

        public bool Active => renderers != null;

        // Snapshot every renderer under `root` and the color it currently has (no tint applied yet —
        // call Retint to color them). Replaces any previous capture without restoring it, so callers
        // restore/forget first.
        public void Capture(Component root)
        {
            renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) originalColors[i] = renderers[i].color;
        }

        // Color all captured renderers (skipping destroyed ones). Cheap to call every frame for the
        // drag's valid/invalid tint; does not touch the remembered originals.
        public void Retint(Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = color;
        }

        // Put the original colors back (skipping destroyed renderers) and forget the target.
        public void Restore()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].color = originalColors[i];
            Forget();
        }

        // Forget the target WITHOUT restoring — for when the structure is being destroyed (Sell), so
        // there is nothing to color back.
        public void Forget()
        {
            renderers = null;
            originalColors = null;
        }
    }

    // Tint the structure under the cursor with `tint` so it reads as the hover target — used by Sell
    // (red, "will be sold") and by idle Move (green, "grabbable"). Cheap in steady state: only does
    // real work (and the one GetComponentInChildren) when the hovered structure CHANGES; otherwise
    // it's a dictionary lookup plus a reference compare per frame.
    private void UpdateHover(Color tint)
    {
        var grid = islandSystem.Grid;
        Vector2 cursor = ScreenToWorld();

        // A fence wins the hover when the cursor is right on an occupied edge (same precedence as click).
        Edge edge = grid.WorldToEdge(cursor);
        var fence = grid.GetEdge(edge);
        if (fence != null && NearEdge(cursor, edge))
        {
            if (fence != hoveredEdge)                  // changed target → restore previous, tint this fence
            {
                ClearHover();
                hoveredEdge = fence;
                hoverTint.Capture(fence.RuntimeObject);
                hoverTint.Retint(tint);
            }
            return;
        }

        var instance = grid.GetCell(grid.WorldToGrid(cursor))?.occupant;
        if (instance == hoveredInstance && hoveredEdge == null) return;   // same target (or still none) — nothing to do

        ClearHover();                              // restore the previous target's color (cell or fence)
        if (instance != null)
        {
            hoveredInstance = instance;
            hoverTint.Capture(instance.RuntimeObject);
            hoverTint.Retint(tint);
        }
    }

    // Restore the tinted structure / fence (if any) to its original color and forget it.
    private void ClearHover()
    {
        hoverTint.Restore();
        hoveredInstance = null;
        hoveredEdge = null;
    }

    // Move tool: while a structure is held it follows the cursor (the real object is dragged) and is
    // tinted valid/invalid — translucent, so it reads as a ghost. Idle (nothing held) does nothing.
    private void UpdateMoveDrag()
    {
        if (heldEdge != null) { UpdateEdgeDrag(); return; }
        if (heldInstance == null || !heldTint.Active) return;

        var grid = islandSystem.Grid;
        var def = heldInstance.Def;
        Vector2Int origin = grid.WorldToOrigin(ScreenToWorld(), def.size);

        bool ok = grid.CanPlace(origin, def.size, def.allowedTerrain, def.border);
        structureSystem.CenterOnFootprint(heldInstance.RuntimeObject.transform, origin, def.size);
        StructureSystem.ApplyRowVisual(heldInstance.RuntimeObject.gameObject, origin.y);   // re-lap a dragged forest
        heldTint.Retint(ok ? validColor : invalidColor);
        ShowTerritory(origin, def.size, def.border, ok);
    }

    // Fence drag: the lifted fence follows the cursor snapped to the nearest edge, shows the matching
    // pose, and is tinted valid/invalid. No territory halo for edges (matches the edge ghost).
    private void UpdateEdgeDrag()
    {
        var grid = islandSystem.Grid;
        Edge edge = grid.WorldToEdge(ScreenToWorld());

        heldEdge.RuntimeObject.transform.position = grid.EdgeToWorld(edge);
        if (heldEdgeVisual != null) heldEdgeVisual.Show(edge.horizontal);

        heldTint.Retint(grid.CanPlaceEdge(edge) ? validColor : invalidColor);
        HideTerritory();
    }

    private void OnWorldClick(Vector2 worldPos)
    {
        if (!active) return;
        // Ignore clicks over UI (panel cards / sell / build button) so they don't act on the world.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        switch (tool)
        {
            case Tool.Place: TryPlace(worldPos); break;
            case Tool.Sell:  TrySell(worldPos);  break;
            case Tool.Move:  TryPickUpOrDrop(worldPos); break;
        }
    }

    // Right-click = cancel the current action, like any strategy game:
    //  - dragging a structure (Move) → put it back at its origin;
    //  - a Place or Sell tool selected → clear it (ghost/sell-tint gone) and notify the panel.
    private void OnWorldRightClick(Vector2 worldPos)
    {
        if (!active) return;

        if (heldInstance != null || heldEdge != null)   // Move drag in progress → return it to its origin
        {
            CancelMove();
            return;
        }

        if (tool != Tool.Move)      // a Place/Sell tool is selected → clear it back to Move
        {
            Select(null);           // controller → Move (tears down ghost / restores sell tint)
            ToolCleared?.Invoke();  // let the panel drop its card / sell highlight
        }
    }

    private void TryPlace(Vector2 worldPos)
    {
        if (selected == null) return;
        if (selected.placement == PlacementKind.Edge) { TryPlaceEdge(worldPos); return; }

        var grid = islandSystem.Grid;
        Vector2Int origin = grid.WorldToOrigin(worldPos, selected.size);

        if (!grid.CanPlace(origin, selected.size, selected.allowedTerrain, selected.border)) return; // bad cell — ghost is already red
        if (!resourceSystem.CanAfford(selected.cost))
        {
            EventBus<BuildDeniedEvent>.Publish(new BuildDeniedEvent { Def = selected });
            return;
        }
        structureSystem.PlaceStructure(selected, origin);
        gridOverlay.Refresh();   // new structure occupies cells → update the territory fill
    }

    private void TryPlaceEdge(Vector2 worldPos)
    {
        var grid = islandSystem.Grid;
        Edge edge = grid.WorldToEdge(worldPos);

        if (!grid.CanPlaceEdge(edge)) return;   // occupied edge / both sides off-island — ghost is already red
        if (!resourceSystem.CanAfford(selected.cost))
        {
            EventBus<BuildDeniedEvent>.Publish(new BuildDeniedEvent { Def = selected });
            return;
        }
        structureSystem.PlaceEdgeStructure(selected, edge);
    }

    private void TrySell(Vector2 worldPos)
    {
        var grid = islandSystem.Grid;

        // A fence wins the click when the cursor is right on an occupied edge — otherwise a fence
        // along a structure's cell would be unsellable (the cell underneath would always be hit first).
        Edge edge = grid.WorldToEdge(worldPos);
        if (grid.GetEdge(edge) != null && NearEdge(worldPos, edge))
        {
            if (structureSystem.SellEdgeStructure(edge))
            {
                // The hover target is being destroyed — drop the refs without touching its color.
                hoveredEdge = null;
                hoverTint.Forget();
                // No gridOverlay.Refresh(): fences occupy no cells, so the territory fill is unchanged.
            }
            return;
        }

        Vector2Int cell = grid.WorldToGrid(worldPos);
        var occupant = grid.GetCell(cell)?.occupant;
        if (occupant == null) return;   // empty cell / off-island — nothing to sell
        if (structureSystem.SellStructure(occupant.Cell))
        {
            // The hover target is being destroyed — drop the refs without touching its color.
            hoveredInstance = null;
            hoverTint.Forget();
            gridOverlay.Refresh();   // cells freed → update the territory fill
        }
    }

    // True when the cursor is within ~⅓ cell of the edge's line (perpendicular distance), i.e. the
    // player is actually aiming at the fence rather than the cell beside it.
    private bool NearEdge(Vector2 worldPos, Edge edge)
    {
        var grid = islandSystem.Grid;
        Vector2 mid = grid.EdgeToWorld(edge);
        float perp = edge.horizontal ? Mathf.Abs(worldPos.y - mid.y) : Mathf.Abs(worldPos.x - mid.x);
        return perp <= grid.CellSize * 0.3f;
    }

    // Move tool click: nothing held → pick up the structure/fence under the cursor; holding → drop it.
    private void TryPickUpOrDrop(Vector2 worldPos)
    {
        if (heldInstance == null && heldEdge == null) TryPickUp(worldPos);
        else TryDrop(worldPos);
    }

    private void TryPickUp(Vector2 worldPos)
    {
        var grid = islandSystem.Grid;

        // A fence wins the click when the cursor is right on an occupied edge (same precedence as Sell).
        Edge edge = grid.WorldToEdge(worldPos);
        var fence = grid.GetEdge(edge);
        if (fence != null && NearEdge(worldPos, edge)) { PickUpEdge(fence); return; }

        var occupant = grid.GetCell(grid.WorldToGrid(worldPos))?.occupant;
        if (occupant == null) return;   // empty cell / off-island — nothing to pick up

        // The structure being grabbed is the one the move-hover just tinted green — restore its true
        // colors FIRST, so heldTint captures the real originals (not the green tint).
        ClearHover();

        structureSystem.PickUpStructure(occupant);   // frees its grid cells + run entry
        heldInstance = occupant;
        heldTint.Capture(occupant.RuntimeObject);
        gridOverlay.Refresh();   // structure lifted off the grid → its territory fill clears (the ghost halo takes over)
    }

    // Lift a fence off its edge and start dragging it (the real object follows the cursor).
    private void PickUpEdge(EdgeInstance fence)
    {
        ClearHover();
        structureSystem.PickUpEdgeStructure(fence);   // frees the grid edge + run entry

        heldEdge = fence;
        heldEdgeVisual = fence.RuntimeObject.GetComponent<DualVisual>();
        heldTint.Capture(fence.RuntimeObject);
    }

    private void TryDrop(Vector2 worldPos)
    {
        if (heldEdge != null) { TryDropEdge(worldPos); return; }

        var grid = islandSystem.Grid;
        Vector2Int origin = grid.WorldToOrigin(worldPos, heldInstance.Def.size);
        if (!grid.CanPlace(origin, heldInstance.Def.size, heldInstance.Def.allowedTerrain, heldInstance.Def.border)) return; // invalid — stay held

        structureSystem.DropStructure(heldInstance, origin);
        ReleaseHeld();
        gridOverlay.Refresh();   // structure re-occupies cells at the new spot → update the territory fill
    }

    private void TryDropEdge(Vector2 worldPos)
    {
        var grid = islandSystem.Grid;
        Edge edge = grid.WorldToEdge(worldPos);
        if (!grid.CanPlaceEdge(edge)) return;   // occupied edge / both sides off-island — stay held

        structureSystem.DropEdgeStructure(heldEdge, edge);
        ReleaseHeldEdge();
    }

    // Return a held structure/fence to its origin (Cell/Edge is untouched while dragging) and release it.
    private void CancelMove()
    {
        if (heldEdge != null)
        {
            structureSystem.DropEdgeStructure(heldEdge, heldEdge.Edge);
            ReleaseHeldEdge();
            return;
        }
        if (heldInstance == null) return;
        structureSystem.DropStructure(heldInstance, heldInstance.Cell);
        ReleaseHeld();
        gridOverlay.Refresh();   // structure restored to its origin → update the territory fill
    }

    private void ReleaseHeld()
    {
        heldTint.Restore();
        heldInstance = null;
        HideTerritory();
    }

    private void ReleaseHeldEdge()
    {
        heldTint.Restore();
        heldEdge = null;
        heldEdgeVisual = null;
        HideTerritory();
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
        ghostRenderers = null;
        ghostVisual = null;
        ghostRowVisual = null;
        HideTerritory();
    }

    // Show the faint footprint+border halo for the ghost at `origin`, tinted by validity. Used by
    // both Place (new structure) and Move (held structure) so the claimed area follows the cursor.
    private void ShowTerritory(Vector2Int origin, Vector2Int size, int border, bool valid)
    {
        EnsureTerritory();

        var grid = islandSystem.Grid;
        float cs = grid.CellSize;
        Vector2 center = grid.OriginToWorldCenter(origin, size);   // footprint center = territory center (border is symmetric)

        territoryGhost.transform.position = new Vector3(center.x, center.y, 0f);
        territoryGhost.transform.localScale = new Vector3((size.x + 2 * border) * cs, (size.y + 2 * border) * cs, 1f);
        territoryRenderer.color = valid ? territoryValidColor : territoryInvalidColor;
        territoryGhost.SetActive(true);
    }

    private void HideTerritory()
    {
        if (territoryGhost != null) territoryGhost.SetActive(false);
    }

    private void EnsureTerritory()
    {
        if (territoryGhost != null) return;

        territoryGhost = new GameObject("PlacementTerritory");
        territoryRenderer = territoryGhost.AddComponent<SpriteRenderer>();
        territoryRenderer.sprite = SquareSprite();
        territoryRenderer.sortingLayerName = territorySortingLayer;
        territoryRenderer.sortingOrder = territorySortingOrder;
    }

    // A shared 1x1 white sprite (centered pivot, 1 px/unit) the territory quad is scaled from.
    private static Sprite SquareSprite()
    {
        if (squareSprite == null)
            squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }
}
