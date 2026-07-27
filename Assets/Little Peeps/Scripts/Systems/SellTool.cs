using UnityEngine;

namespace LittlePeeps
{
    // SELL: the structure or fence under the cursor is tinted "will be sold"; clicking it refunds part of
    // the build cost and removes it. No ghost — there is nothing being created.
    public sealed class SellTool : IPlacementTool
    {
        private readonly PlacementContext ctx;
        private readonly HoverHighlight hover;

        public SellTool(PlacementContext ctx)
        {
            this.ctx = ctx;
            hover = new HoverHighlight(ctx, HoverStyle.Sell);
        }

        public void Enter() { }

        public void Exit() => hover.Clear();

        public void Tick(Vector2 cursor)
        {
            ctx.Visuals.HideTerritory();
            hover.Tick(cursor);
        }

        public void Click(Vector2 world)
        {
            var target = PlacementTarget.Resolve(ctx.Grid, world);
            if (target.IsNone) return;   // empty cell / off-island — nothing to sell

            if (target.IsFence)
            {
                if (!ctx.Structures.SellEdgeStructure(target.Edge)) return;
                // No Overlay.Refresh(): fences occupy no cells, so the territory fill is unchanged.
            }
            else
            {
                if (!ctx.Structures.SellStructure(target.Instance.Cell)) return;
                ctx.Overlay.Refresh();   // cells freed → update the territory fill
            }

            hover.Forget();   // the highlighted object has just been destroyed
        }

        // Nothing is ever mid-action here — a right-click means "put the sell button down".
        public bool Cancel() => false;
    }
}
