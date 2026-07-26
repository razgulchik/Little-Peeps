using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LittlePeeps
{
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
    //
    // This class decides WHAT is targeted and what it costs; everything it DRAWS — the ghost, the
    // territory halo, hover and drag tints — belongs to PlacementVisuals, so no renderer is touched here.
    public class PlacementController : MonoBehaviour
    {
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private StructureSystem structureSystem;
        [SerializeField] private ResourceSystem resourceSystem;
        [SerializeField] private IslandSystem islandSystem;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GridOverlay gridOverlay;

        [SerializeField] private PlacementVisuals visuals = new();

        // Raised when a right-click clears the active Place/Sell tool, so BuildPanelUI can drop its
        // card / sell-button highlight (the controller has already reset itself to the Move tool).
        public event Action ToolCleared;

        // Active tool, driven by the panel: a card → Place, the sell button → Sell, nothing → Move.
        private enum Tool { Move, Place, Sell }
        private Tool tool = Tool.Move;

        private bool active;
        private StructureDef selected;

        // Hover target: the structure OR fence currently tinted. A cell structure and a fence are never
        // both hovered (the edge wins by NearEdge precedence), so only one is ever set.
        private StructureInstance hoveredInstance;
        private EdgeInstance hoveredEdge;

        // Move-mode drag: the structure OR fence lifted off the grid and following the cursor. Only one
        // is ever held.
        private StructureInstance heldInstance;
        private EdgeInstance heldEdge;

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
            visuals.Init(structureSystem);
            active = true;
            gridOverlay.Show();
        }

        // Called by BuildModeState.Exit. Tear down ghost + overlay.
        public void End()
        {
            CancelMove();   // if mid-drag, return the structure to its origin before leaving
            active = false;
            tool = Tool.Move;
            visuals.ClearGhost();
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
            ClearHover();       // leaving the previous tool — restore any tinted structure
            visuals.BuildGhost(def);
        }

        // Switch to the Sell tool (BuildPanelUI's sell button calls this). No ghost in sell mode.
        public void SetSellMode()
        {
            CancelMove();
            selected = null;
            tool = Tool.Sell;
            visuals.ClearGhost();
            ClearHover();   // restart hover fresh so the next frame re-tints in the Sell color
        }

        private void Update()
        {
            if (!active) return;

            switch (tool)
            {
                case Tool.Place: UpdatePlaceGhost(); break;
                case Tool.Sell:  visuals.HideTerritory(); UpdateHover(HoverStyle.Sell); break;
                case Tool.Move:
                    if (heldInstance != null || heldEdge != null) UpdateMoveDrag();          // holding → drag it
                    else { visuals.HideTerritory(); UpdateHover(HoverStyle.Move); }          // idle → grabbable hint
                    break;
            }
        }

        // Place tool: the ghost follows the cursor, tinted by buildable + affordable.
        private void UpdatePlaceGhost()
        {
            if (selected == null || !visuals.HasGhost) return;
            if (selected.placement == PlacementKind.Edge) UpdateEdgeGhost();
            else UpdateCellGhost();
        }

        private void UpdateCellGhost()
        {
            var grid = islandSystem.Grid;
            Vector2Int origin = grid.WorldToOrigin(ScreenToWorld(), selected.size);

            // Same placement rule as a real structure — the builder owns it (ghost matches exactly).
            bool ok = grid.CanPlace(origin, selected.size, selected.allowedTerrain, selected.border)
                      && resourceSystem.CanAfford(selected.cost);

            visuals.PoseCellGhost(origin, selected.size, ok);
            visuals.ShowTerritory(grid, origin, selected.size, selected.border, ok);
        }

        // Edge ghost (fence): snap to the nearest grid edge, sit on its midpoint, show the matching pose,
        // tint by placeable + affordable. No territory halo for edges in v1.
        private void UpdateEdgeGhost()
        {
            var grid = islandSystem.Grid;
            Edge edge = grid.WorldToEdge(ScreenToWorld());

            bool ok = grid.CanPlaceEdge(edge) && resourceSystem.CanAfford(selected.cost);

            visuals.PoseEdgeGhost(grid, edge, ok);
            visuals.HideTerritory();
        }

        // Tint the structure under the cursor so it reads as the hover target — used by Sell ("will be
        // sold") and by idle Move ("grabbable"). Cheap in steady state: only does real work when the
        // hovered structure CHANGES; otherwise it's a dictionary lookup plus a reference compare per frame.
        private void UpdateHover(HoverStyle style)
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
                    visuals.SetHover(fence.RuntimeObject, style);
                }
                return;
            }

            var instance = grid.GetCell(grid.WorldToGrid(cursor))?.occupant;
            if (instance == hoveredInstance && hoveredEdge == null) return;   // same target (or still none) — nothing to do

            ClearHover();                              // restore the previous target's color (cell or fence)
            if (instance != null)
            {
                hoveredInstance = instance;
                visuals.SetHover(instance.RuntimeObject, style);
            }
        }

        // Restore the tinted structure / fence (if any) to its original color and forget it.
        private void ClearHover()
        {
            visuals.ClearHover();
            hoveredInstance = null;
            hoveredEdge = null;
        }

        // Move tool: while a structure is held it follows the cursor (the real object is dragged) and is
        // tinted valid/invalid — translucent, so it reads as a ghost. Idle (nothing held) does nothing.
        private void UpdateMoveDrag()
        {
            if (heldEdge != null) { UpdateEdgeDrag(); return; }
            if (heldInstance == null || !visuals.HasHeld) return;

            var grid = islandSystem.Grid;
            var def = heldInstance.Def;
            Vector2Int origin = grid.WorldToOrigin(ScreenToWorld(), def.size);

            bool ok = grid.CanPlace(origin, def.size, def.allowedTerrain, def.border);

            visuals.PoseHeldCell(origin, def.size, ok);
            visuals.ShowTerritory(grid, origin, def.size, def.border, ok);
        }

        // Fence drag: the lifted fence follows the cursor snapped to the nearest edge, shows the matching
        // pose, and is tinted valid/invalid. No territory halo for edges (matches the edge ghost).
        private void UpdateEdgeDrag()
        {
            var grid = islandSystem.Grid;
            Edge edge = grid.WorldToEdge(ScreenToWorld());

            visuals.PoseHeldEdge(grid, edge, grid.CanPlaceEdge(edge));
            visuals.HideTerritory();
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
                    visuals.ForgetHover();
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
                visuals.ForgetHover();
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
            // colors FIRST, so the held capture takes the real originals (not the green tint).
            ClearHover();

            structureSystem.PickUpStructure(occupant);   // frees its grid cells + run entry
            heldInstance = occupant;
            visuals.CaptureHeld(occupant.RuntimeObject);
            gridOverlay.Refresh();   // structure lifted off the grid → its territory fill clears (the ghost halo takes over)
        }

        // Lift a fence off its edge and start dragging it (the real object follows the cursor).
        private void PickUpEdge(EdgeInstance fence)
        {
            ClearHover();
            structureSystem.PickUpEdgeStructure(fence);   // frees the grid edge + run entry

            heldEdge = fence;
            visuals.CaptureHeld(fence.RuntimeObject);
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
            visuals.ReleaseHeld();
            heldInstance = null;
        }

        private void ReleaseHeldEdge()
        {
            visuals.ReleaseHeld();
            heldEdge = null;
        }

        private Vector2 ScreenToWorld()
        {
            Vector2 screen = Mouse.current.position.ReadValue();
            return mainCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        }
    }
}
