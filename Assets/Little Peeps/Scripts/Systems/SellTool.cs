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
            hover = new HoverHighlight(ctx);
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
            if (!target.CanSell) return;   // empty cell / off-island / a def that can't be sold — nothing to do

            if (target.IsFence)
            {
                if (!ctx.Structures.SellEdgeStructure(target.Edge)) return;
            }
            else
            {
                if (!ctx.Structures.SellStructure(target.Instance.Cell)) return;
            }

            hover.Forget();   // the highlighted object has just been destroyed
        }

        // Nothing is ever mid-action here — a right-click means "put the sell button down".
        public bool Cancel() => false;
    }
}
