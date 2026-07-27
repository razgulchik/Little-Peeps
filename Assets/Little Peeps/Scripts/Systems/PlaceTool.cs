using UnityEngine;

namespace LittlePeeps
{
    // PLACE: a ghost preview follows the cursor, tinted green/red by buildable + affordable; clicking a
    // valid spot builds and stays in placement mode so the player can repeat.
    //
    // One tool instance per selected StructureDef — the def is what the tool IS, so switching cards makes
    // a new one rather than mutating this. Handles both placement kinds: a cell footprint (with its
    // territory halo) or a fence on a grid edge.
    public sealed class PlaceTool : IPlacementTool
    {
        private readonly PlacementContext ctx;
        private readonly StructureDef def;

        public PlaceTool(PlacementContext ctx, StructureDef def)
        {
            this.ctx = ctx;
            this.def = def;
        }

        public void Enter() => ctx.Visuals.BuildGhost(def);

        public void Exit() => ctx.Visuals.ClearGhost();

        public void Tick(Vector2 cursor)
        {
            if (!ctx.Visuals.HasGhost) return;   // a def with no prefab has nothing to preview
            if (def.placement == PlacementKind.Edge) TickEdge(cursor);
            else TickCell(cursor);
        }

        private void TickCell(Vector2 cursor)
        {
            var grid = ctx.Grid;
            Vector2Int origin = grid.WorldToOrigin(cursor, def.size);

            // Same placement rule as a real structure — the builder owns it (ghost matches exactly).
            bool ok = grid.CanPlace(origin, def.size, def.allowedTerrain, def.border)
                      && ctx.Resources.CanAfford(def.cost);

            ctx.Visuals.PoseCellGhost(origin, def.size, ok);
            ctx.Visuals.ShowTerritory(grid, origin, def.size, def.border, ok);
        }

        // Fence: snap to the nearest grid edge, sit on its midpoint, show the matching pose, tint by
        // placeable + affordable. No territory halo for edges in v1.
        private void TickEdge(Vector2 cursor)
        {
            var grid = ctx.Grid;
            Edge edge = grid.WorldToEdge(cursor);

            bool ok = grid.CanPlaceEdge(edge) && ctx.Resources.CanAfford(def.cost);

            ctx.Visuals.PoseEdgeGhost(grid, edge, ok);
            ctx.Visuals.HideTerritory();
        }

        public void Click(Vector2 world)
        {
            if (def.placement == PlacementKind.Edge) ClickEdge(world);
            else ClickCell(world);
        }

        private void ClickCell(Vector2 world)
        {
            var grid = ctx.Grid;
            Vector2Int origin = grid.WorldToOrigin(world, def.size);

            if (!grid.CanPlace(origin, def.size, def.allowedTerrain, def.border)) return; // bad cell — ghost is already red
            if (!ctx.Resources.CanAfford(def.cost))
            {
                EventBus<BuildDeniedEvent>.Publish(new BuildDeniedEvent { Def = def });
                return;
            }
            ctx.Structures.PlaceStructure(def, origin);
            ctx.Overlay.Refresh();   // new structure occupies cells → update the territory fill
        }

        private void ClickEdge(Vector2 world)
        {
            var grid = ctx.Grid;
            Edge edge = grid.WorldToEdge(world);

            if (!grid.CanPlaceEdge(edge)) return;   // occupied edge / both sides off-island — ghost is already red
            if (!ctx.Resources.CanAfford(def.cost))
            {
                EventBus<BuildDeniedEvent>.Publish(new BuildDeniedEvent { Def = def });
                return;
            }
            ctx.Structures.PlaceEdgeStructure(def, edge);
        }

        // Nothing is ever mid-action here — a right-click means "put the card down", which is the
        // controller's job.
        public bool Cancel() => false;
    }
}
