using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // SELL: the structure or fence under the cursor is tinted "will be sold"; clicking it refunds part of
    // the build cost and removes it. No ghost — there is nothing being created.
    //
    // What the press lands on decides what the hold does. On a structure, it is sold at once. On a fence,
    // the hold is a sell STROKE — the same path along the grid lines a fence is drawn with (EdgeStroke):
    // every fence it runs over is tinted, and they are all sold on the release. Only fences — a stroke
    // started on a wall never takes the building beside it. Backing up along the line un-marks, a
    // right-click drops the stroke, and a release before the stroke grew sells just the fence pressed.
    public sealed class SellTool : IPlacementTool
    {
        private readonly PlacementContext ctx;
        private readonly HoverHighlight hover;

        private readonly EdgeStroke stroke = new();
        private readonly List<Edge> strokeFences = new();   // the sellable fences under the stroke, in drawing order
        private readonly List<Structure> marked = new();    // their objects as last tinted
        private readonly List<Structure> marking = new();   // this frame's, to compare against `marked`
        private bool strokeGrew;                            // the stroke has had a segment since the press

        public SellTool(PlacementContext ctx)
        {
            this.ctx = ctx;
            hover = new HoverHighlight(ctx);
        }

        public void Enter() { }

        public void Exit()
        {
            DropStroke();
            hover.Clear();
        }

        public void Tick(Vector2 cursor)
        {
            ctx.Visuals.HideTerritory();
            if (stroke.Active) TickStroke(cursor);
            else hover.Tick(cursor);
        }

        // Until the stroke grows, the pressed fence keeps its hover tint — release now is a click on it.
        // From then on the marks take over.
        private void TickStroke(Vector2 cursor)
        {
            ExtendStroke(cursor);
            if (!strokeGrew) return;

            hover.Clear();
            CollectStrokeFences();
            MarkIfChanged();
        }

        // Over the panel the stroke holds still, as a building stroke does (PlaceTool).
        private void ExtendStroke(Vector2 cursor)
        {
            if (InputHandler.IsPointerOverUI()) return;
            stroke.Extend(ctx.Grid, cursor);
            if (stroke.SegmentCount > 0) strokeGrew = true;
        }

        // The fences under the stroke that the Sell tool may take (the def decides, as for a click).
        private void CollectStrokeFences()
        {
            stroke.CollectFences(ctx.Grid, strokeFences);
            for (int i = strokeFences.Count - 1; i >= 0; i--)
                if (!CanSell(ctx.Grid.GetEdge(strokeFences[i]))) strokeFences.RemoveAt(i);
        }

        private static bool CanSell(EdgeInstance fence) => fence.Def != null && fence.Def.canSell;

        // Re-tint only when the marked objects change — a new fence under the stroke, one backed off, or a
        // fence site swapped for its finished fence.
        private void MarkIfChanged()
        {
            marking.Clear();
            foreach (var edge in strokeFences) marking.Add(ctx.Grid.GetEdge(edge).RuntimeObject);
            if (SameObjects(marking, marked)) return;

            ctx.Visuals.MarkForSale(marking);
            marked.Clear();
            marked.AddRange(marking);
        }

        private static bool SameObjects(List<Structure> a, List<Structure> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        public void Press(Vector2 world)
        {
            var target = PlacementTarget.Resolve(ctx.Grid, world);
            if (!target.CanSell) return;   // empty cell / off-island / a def that can't be sold — nothing to do

            if (target.IsFence)
            {
                stroke.Begin(target.Edge);   // sold on the release — alone, or with the stroke drawn from it
                strokeGrew = false;
                return;
            }

            if (ctx.Structures.SellStructure(target.Instance.Cell))
                hover.Forget();   // the highlighted object has just been destroyed
        }

        public void Release(Vector2 world)
        {
            if (!stroke.Active) return;   // a structure was sold on the press, or nothing was pressed

            ExtendStroke(world);   // up to where the button came up, whichever ran first this frame
            if (!strokeGrew)
            {
                if (ctx.Structures.SellEdgeStructure(stroke.PressedEdge))
                    hover.Forget();   // the pressed fence, still tinted as the hover, is gone
            }
            else
            {
                CollectStrokeFences();
                foreach (var edge in strokeFences) ctx.Structures.SellEdgeStructure(edge);
                // Restored, not forgotten: the catch-up above may have backed off a marked fence that is
                // still standing. A sold one is harmless to recolour — its Destroy lands at frame end.
                DropStroke();
                return;
            }
            stroke.Clear();
        }

        // A right-click mid-stroke drops the stroke — nothing was sold. Otherwise nothing is under way, and
        // the right-click puts the sell button down.
        public bool Cancel()
        {
            if (!stroke.Active) return false;
            DropStroke();
            return true;
        }

        private void DropStroke()
        {
            stroke.Clear();
            ctx.Visuals.ClearSaleMarks();
            marked.Clear();
        }
    }
}
