using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LittlePeeps
{
    // Routes build-mode input to whichever tool the BuildPanelUI selection has chosen:
    //  - a build card selected    → PlaceTool
    //  - the sell button selected → SellTool
    //  - nothing selected         → MoveTool (the default)
    // Right-click cancels: a tool with an action in progress consumes it (a Move drag returns to its
    // origin), otherwise the selection is cleared back to Move and ToolCleared tells the panel to drop
    // its highlight. Active only between Begin()/End(), called by BuildModeState. Clicks over UI are
    // ignored so panel buttons don't act on the world.
    //
    // Everything that decides WHAT happens lives in the tools, and everything DRAWN lives in
    // PlacementVisuals — this class only owns the inspector-wired references, the active tool, and the
    // rule that every switch goes through Exit so no tool can leave anything behind.
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

        private PlacementContext context;
        private MoveTool moveTool;    // also the default: "nothing selected" IS the Move tool
        private SellTool sellTool;
        private IPlacementTool tool;

        private bool active;

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
            EnsureTools();
            active = true;
            gridOverlay.Show();
        }

        // Called by BuildModeState.Exit. The active tool tears down whatever it owns — including returning
        // a structure that is still being carried.
        public void End()
        {
            EnsureTools();
            tool.Exit();
            tool = moveTool;
            active = false;
            gridOverlay.Hide();
        }

        // Choose which structure to place (BuildPanelUI calls this). A null def means "nothing selected"
        // → back to the Move tool.
        public void Select(StructureDef def)
        {
            EnsureTools();
            SwitchTo(def != null ? new PlaceTool(context, def) : moveTool);
        }

        // Switch to the Sell tool (BuildPanelUI's sell button calls this).
        public void SetSellMode()
        {
            EnsureTools();
            SwitchTo(sellTool);
        }

        // The single path between tools. Exit before Enter, always — that ordering is what guarantees a
        // ghost, a tint or a carried structure can never survive into the next tool. Switching to the
        // tool that is already active is meaningful too: it restarts it (Select(null) mid-drag is how a
        // carried structure gets put back).
        private void SwitchTo(IPlacementTool next)
        {
            tool.Exit();
            tool = next;
            tool.Enter();
        }

        // Built once, lazily, so the tools exist no matter which entry point fires first.
        private void EnsureTools()
        {
            if (context != null) return;

            visuals.Init(structureSystem);
            context = new PlacementContext(islandSystem, structureSystem, resourceSystem, gridOverlay, visuals);
            moveTool = new MoveTool(context);
            sellTool = new SellTool(context);
            tool = moveTool;
        }

        private void Update()
        {
            if (!active) return;
            tool.Tick(ScreenToWorld());
        }

        private void OnWorldClick(Vector2 worldPos)
        {
            if (!active) return;
            // Ignore clicks over UI (panel cards / sell / build button) so they don't act on the world.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            tool.Click(worldPos);
        }

        // Right-click = cancel the current action, like any strategy game. The tool gets first refusal:
        // if it had something in progress it consumes the click, otherwise the selection is cleared.
        private void OnWorldRightClick(Vector2 worldPos)
        {
            if (!active) return;

            if (tool.Cancel()) return;      // a drag was returned to its origin
            if (tool == moveTool) return;   // nothing selected — nothing to clear

            SwitchTo(moveTool);
            ToolCleared?.Invoke();          // let the panel drop its card / sell highlight
        }

        private Vector2 ScreenToWorld()
        {
            Vector2 screen = Mouse.current.position.ReadValue();
            return mainCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        }
    }
}
