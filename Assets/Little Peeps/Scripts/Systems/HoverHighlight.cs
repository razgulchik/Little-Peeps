using UnityEngine;

namespace LittlePeeps
{
    // Tints whatever the cursor is over, so the player can see what a click would act on. Sell uses it to
    // mean "will be sold" and idle Move to mean "can be grabbed" — same behaviour, different colour, so
    // it lives here once instead of in both tools.
    //
    // Cheap in steady state: it resolves the target every frame but only touches renderers when the
    // target actually CHANGES, so the usual frame costs one dictionary lookup and one compare.
    public sealed class HoverHighlight
    {
        private readonly PlacementContext ctx;
        private readonly HoverStyle style;

        private PlacementTarget hovered;

        public HoverHighlight(PlacementContext ctx, HoverStyle style)
        {
            this.ctx = ctx;
            this.style = style;
        }

        public void Tick(Vector2 cursor)
        {
            // Same targeting the click uses, so what is highlighted and what is acted on can never differ.
            var target = PlacementTarget.Resolve(ctx.Grid, cursor);
            if (target.Equals(hovered)) return;   // same target (or still none) — nothing to do

            Clear();                              // restore the previous target's color (cell or fence)
            hovered = target;
            if (!target.IsNone) ctx.Visuals.SetHover(target.RuntimeObject, style);
        }

        // Restore the tinted structure / fence (if any) to its original color and forget it.
        public void Clear()
        {
            ctx.Visuals.ClearHover();
            hovered = default;
        }

        // Drop the target WITHOUT restoring it — for when it has just been destroyed (Sell) or lifted off
        // the grid, so there is nothing left to colour back.
        public void Forget()
        {
            ctx.Visuals.ForgetHover();
            hovered = default;
        }
    }
}
