using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // PLACE: a ghost preview follows the cursor, tinted green/red by buildable + under the build limit +
    // affordable (at the NEXT one's price — it grows with how many stand); clicking a
    // valid spot builds and stays in placement mode so the player can repeat.
    //
    // One tool instance per selected StructureDef — the def is what the tool IS, so switching cards makes
    // a new one rather than mutating this. Handles both placement kinds: a cell footprint (with its
    // territory halo) or a fence on a grid edge.
    //
    // A fence is also DRAWN: hold the button and lead the cursor along the grid lines (EdgeStroke). While
    // held, the whole stroke shows as ghosts — green what will go up, red what won't (refused edge, past
    // the limit, past what the purse pays) — and nothing is built or paid until the release, so backing
    // up along the line or a right-click costs nothing. A press released before the stroke grew a single
    // segment is the plain click: one fence on the edge pressed. A stroke drawn and then erased right back
    // to its start builds nothing — the player took it back, not clicked.
    public sealed class PlaceTool : IPlacementTool
    {
        private readonly PlacementContext ctx;
        private readonly StructureDef def;

        private readonly EdgeAim aim = new();   // the cursor ghost's edge, held against jitter
        private readonly EdgeStroke stroke = new();
        private readonly List<StrokeSegment> plan = new();
        private bool strokeGrew;    // the stroke has had a segment since the press — release is no longer a click
        private bool moneyShort;    // the last plan had segments the purse could not pay for (not the limit)

        public PlaceTool(PlacementContext ctx, StructureDef def)
        {
            this.ctx = ctx;
            this.def = def;
        }

        public void Enter() => ctx.Visuals.BuildGhost(def);

        public void Exit()
        {
            stroke.Clear();   // a stroke cut short by a tool switch builds nothing
            ctx.Visuals.ClearGhost();
        }

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
            bool ok = grid.CanPlace(origin, def)
                      && !ctx.Structures.AtLimit(def)
                      && ctx.Structures.CanAfford(def);

            ctx.Visuals.PoseCellGhost(grid, origin, def.Footprint, ok);
            ctx.Visuals.ShowTerritory(grid, origin, def.Footprint, def.border, ok);
        }

        // Fence. No stroke: stand on the edge EdgeAim holds (the nearest, without the jitter), sit on its midpoint, show the matching pose,
        // tint by placeable + affordable. A stroke with segments: the whole plan, re-made every frame so
        // it keeps up with the purse. A stroke still without a segment: the pressed edge, which is what
        // the release would build — or nothing at all, once it was drawn and erased back to the start.
        // No territory halo for edges.
        private void TickEdge(Vector2 cursor)
        {
            var grid = ctx.Grid;
            ctx.Visuals.HideTerritory();

            if (stroke.Active)
            {
                ExtendStroke(cursor);
                if (strokeGrew)
                {
                    PlanStroke();
                    ctx.Visuals.ShowEdgeStroke(grid, plan);
                    return;
                }
            }

            Edge edge = stroke.Active ? stroke.PressedEdge : aim.Update(grid, cursor);
            ctx.Visuals.PoseEdgeGhost(grid, edge, CanBuildOne(edge));
        }

        // Over the panel the stroke holds still: the world under the UI can't be seen, so nothing may be
        // drawn there, and a release over the panel builds exactly what was last in sight.
        private void ExtendStroke(Vector2 cursor)
        {
            if (InputHandler.IsPointerOverUI()) return;
            stroke.Extend(ctx.Grid, cursor);
            if (stroke.SegmentCount > 0) strokeGrew = true;
        }

        private bool CanBuildOne(Edge edge) =>
            ctx.Grid.CanPlaceEdge(edge) && !ctx.Structures.AtLimit(def) && ctx.Structures.CanAfford(def);

        // The stroke as it would be built now: what the grid takes, cut down to the build limit and then
        // to the purse, each fence at its own place in the growing price.
        private void PlanStroke()
        {
            int placeable = stroke.Collect(ctx.Grid, plan);
            int room = ctx.Structures.RoomUnderLimit(def, placeable);
            int affordable = ctx.Structures.AffordableCount(def, room);
            moneyShort = affordable < room;
            EdgeStroke.CapBudget(plan, affordable);
        }

        public void Press(Vector2 world)
        {
            if (def.placement == PlacementKind.Edge)
            {
                stroke.Begin(aim.Update(ctx.Grid, world));   // the ghost's edge, not a fresh nearest one
                strokeGrew = false;
            }
            else PressCell(world);
        }

        public void Release(Vector2 world)
        {
            if (!stroke.Active) return;   // a cell building, or a press another tool took

            ExtendStroke(world);   // up to where the button came up, whichever ran first this frame
            if (!strokeGrew) PlaceOneEdge(stroke.PressedEdge);
            else BuildStroke();
            stroke.Clear();
        }

        private void PressCell(Vector2 world)
        {
            var grid = ctx.Grid;
            Vector2Int origin = grid.WorldToOrigin(world, def.size);

            if (!grid.CanPlace(origin, def)) return; // bad cell — ghost is already red
            if (ctx.Structures.AtLimit(def)) return; // as many stand as the run allows — ghost is red, the card locks
            if (!ctx.Structures.CanAfford(def))
            {
                EventBus<BuildDeniedEvent>.Publish(new BuildDeniedEvent { Def = def });
                return;
            }
            ctx.Structures.PlaceStructure(def, origin);
        }

        private void PlaceOneEdge(Edge edge)
        {
            if (!ctx.Grid.CanPlaceEdge(edge)) return;   // occupied / inside a structure / off-island — ghost is already red
            if (ctx.Structures.AtLimit(def)) return;
            if (!ctx.Structures.CanAfford(def))
            {
                EventBus<BuildDeniedEvent>.Publish(new BuildDeniedEvent { Def = def });
                return;
            }
            ctx.Structures.PlaceEdgeStructure(def, edge);
        }

        // Every green segment goes up, in drawing order. The plan is made afresh first, so what is built is
        // what the purse pays for at this moment; the builder still checks each one itself.
        private void BuildStroke()
        {
            PlanStroke();
            for (int i = 0; i < plan.Count; i++)
                if (plan[i].State == StrokeSegmentState.Build)
                    ctx.Structures.PlaceEdgeStructure(def, plan[i].Edge);

            if (moneyShort) EventBus<BuildDeniedEvent>.Publish(new BuildDeniedEvent { Def = def });
            ctx.Visuals.HideEdgeStroke();
        }

        // A right-click mid-stroke drops the stroke — nothing was built or paid. Otherwise nothing is under
        // way, and the right-click puts the card down (the controller's job).
        public bool Cancel()
        {
            if (!stroke.Active) return false;
            stroke.Clear();
            ctx.Visuals.HideEdgeStroke();
            return true;
        }
    }
}
