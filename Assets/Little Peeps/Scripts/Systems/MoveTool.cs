using UnityEngine;

namespace LittlePeeps
{
    // MOVE: the default tool, active whenever no card and no sell button is selected. Idle, it tints
    // whatever is under the cursor as "grabbable". Click to lift the real object off the grid and carry
    // it; click again on a valid spot to drop it, free of charge.
    //
    // This is the only tool that holds state between clicks, and the only one that can leave the world in
    // a half-finished state — a structure is genuinely off the grid while carried. Every exit path
    // therefore runs through CancelDrag: tool switch, right-click, and the end of build mode.
    public sealed class MoveTool : IPlacementTool
    {
        private readonly PlacementContext ctx;
        private readonly HoverHighlight hover;

        // The structure OR fence lifted off the grid and following the cursor. Only one is ever held.
        private StructureInstance heldInstance;
        private EdgeInstance heldEdge;

        public MoveTool(PlacementContext ctx)
        {
            this.ctx = ctx;
            hover = new HoverHighlight(ctx, HoverStyle.Move);
        }

        private bool IsHolding => heldInstance != null || heldEdge != null;

        public void Enter() { }

        // Leaving with something in hand puts it back where it came from — a carried structure must never
        // be left off the grid.
        public void Exit()
        {
            CancelDrag();
            hover.Clear();
        }

        public void Tick(Vector2 cursor)
        {
            if (IsHolding) { TickDrag(cursor); return; }

            ctx.Visuals.HideTerritory();
            hover.Tick(cursor);
        }

        // While held, the real object follows the cursor and is tinted valid/invalid — translucent, so it
        // reads as a ghost.
        private void TickDrag(Vector2 cursor)
        {
            if (heldEdge != null) { TickEdgeDrag(cursor); return; }
            if (!ctx.Visuals.HasHeld) return;

            var grid = ctx.Grid;
            var def = heldInstance.Def;
            Vector2Int origin = grid.WorldToOrigin(cursor, def.size);

            bool ok = grid.CanPlace(origin, def.size, def.allowedTerrain, def.border);

            ctx.Visuals.PoseHeldCell(origin, def.size, ok);
            ctx.Visuals.ShowTerritory(grid, origin, def.size, def.border, ok);
        }

        // Fence drag: snapped to the nearest edge, showing the matching pose. No territory halo for edges
        // (matches the edge ghost).
        private void TickEdgeDrag(Vector2 cursor)
        {
            var grid = ctx.Grid;
            Edge edge = grid.WorldToEdge(cursor);

            ctx.Visuals.PoseHeldEdge(grid, edge, grid.CanPlaceEdge(edge));
            ctx.Visuals.HideTerritory();
        }

        public void Click(Vector2 world)
        {
            if (IsHolding) TryDrop(world);
            else TryPickUp(world);
        }

        private void TryPickUp(Vector2 world)
        {
            var target = PlacementTarget.Resolve(ctx.Grid, world);
            if (target.IsNone) return;   // empty cell / off-island — nothing to pick up

            // The object being grabbed is the one the hover just tinted green — restore its true colors
            // FIRST, so the held capture takes the real originals and not the hint tint.
            hover.Clear();

            if (target.IsFence)
            {
                ctx.Structures.PickUpEdgeStructure(target.Fence);   // frees the grid edge + run entry
                heldEdge = target.Fence;
                ctx.Visuals.CaptureHeld(target.RuntimeObject);
                return;
            }

            ctx.Structures.PickUpStructure(target.Instance);   // frees its grid cells + run entry
            heldInstance = target.Instance;
            ctx.Visuals.CaptureHeld(target.RuntimeObject);
            ctx.Overlay.Refresh();   // lifted off the grid → its territory fill clears (the halo takes over)
        }

        private void TryDrop(Vector2 world)
        {
            if (heldEdge != null) { TryDropEdge(world); return; }

            var grid = ctx.Grid;
            var def = heldInstance.Def;
            Vector2Int origin = grid.WorldToOrigin(world, def.size);
            if (!grid.CanPlace(origin, def.size, def.allowedTerrain, def.border)) return;   // invalid — stay held

            ctx.Structures.DropStructure(heldInstance, origin);
            ReleaseInstance();
            ctx.Overlay.Refresh();   // re-occupies cells at the new spot → update the territory fill
        }

        private void TryDropEdge(Vector2 world)
        {
            var grid = ctx.Grid;
            Edge edge = grid.WorldToEdge(world);
            if (!grid.CanPlaceEdge(edge)) return;   // occupied edge / both sides off-island — stay held

            ctx.Structures.DropEdgeStructure(heldEdge, edge);
            ReleaseEdge();
        }

        // Right-click puts a carried object back; with empty hands there is nothing to cancel and the
        // controller handles the click instead.
        public bool Cancel()
        {
            if (!IsHolding) return false;
            CancelDrag();
            return true;
        }

        // Return whatever is held to its origin (Cell/Edge is untouched while dragging) and release it.
        private void CancelDrag()
        {
            if (heldEdge != null)
            {
                ctx.Structures.DropEdgeStructure(heldEdge, heldEdge.Edge);
                ReleaseEdge();
                return;
            }
            if (heldInstance == null) return;

            ctx.Structures.DropStructure(heldInstance, heldInstance.Cell);
            ReleaseInstance();
            ctx.Overlay.Refresh();   // restored to its origin → update the territory fill
        }

        private void ReleaseInstance()
        {
            ctx.Visuals.ReleaseHeld();
            heldInstance = null;
        }

        private void ReleaseEdge()
        {
            ctx.Visuals.ReleaseHeld();
            heldEdge = null;
        }
    }
}
