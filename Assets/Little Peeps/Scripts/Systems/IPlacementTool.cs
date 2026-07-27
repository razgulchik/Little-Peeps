using UnityEngine;

namespace LittlePeeps
{
    // One build-mode tool: Place, Sell or Move. Exactly one is active at a time; PlacementController
    // routes input to it and swaps it when the panel selection changes.
    //
    // The contract that matters is Exit: a tool may leave NOTHING behind — no ghost, no tinted structure,
    // no half-finished drag. Every switch and the end of build mode go through it, so if a tool forgets
    // to undo something, the leftover outlives the tool that made it.
    public interface IPlacementTool
    {
        // Becoming the active tool.
        void Enter();

        // Ceasing to be active (tool switch, or build mode ending). Must undo everything Enter/Tick/Click
        // put on screen or lifted off the grid.
        void Exit();

        // Per-frame preview at the cursor.
        void Tick(Vector2 cursor);

        // Left click in the world (already filtered: never fires over UI).
        void Click(Vector2 world);

        // Right click. True = the tool had an action in progress and consumed the click (a drag returned
        // to its origin). False = nothing to cancel, so the controller clears back to the Move tool.
        bool Cancel();
    }

    // The systems every tool needs, bundled so each tool takes one constructor argument instead of five.
    // Passed by the controller, which owns the inspector-wired references.
    public sealed class PlacementContext
    {
        public readonly IslandSystem Island;
        public readonly StructureSystem Structures;
        public readonly ResourceSystem Resources;
        public readonly GridOverlay Overlay;
        public readonly PlacementVisuals Visuals;

        public PlacementContext(IslandSystem island, StructureSystem structures, ResourceSystem resources,
                                GridOverlay overlay, PlacementVisuals visuals)
        {
            Island = island;
            Structures = structures;
            Resources = resources;
            Overlay = overlay;
            Visuals = visuals;
        }

        // Fetched through the system on every use rather than cached, exactly as before the tools existed.
        public IslandGrid Grid => Island.Grid;
    }
}
