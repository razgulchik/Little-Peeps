using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LittlePeeps
{
    // Routes build-mode input to whichever tool the BuildPanelUI selection has chosen:
    //  - a build card selected    → PlaceTool
    //  - the sell button selected → SellTool
    //  - nothing selected         → the empty hand: no tool, and the click is a tap (TapSystem)
    // The game runs underneath the whole time, so "something in hand" (HoldsTool) is what decides
    // whether a left click builds or taps. Right-click puts the tool down: the selection is cleared back
    // to the empty hand and ToolCleared tells the panel to drop its highlight. Active only between
    // Begin()/End(), called by BuildModeState. Clicks over UI never get here — InputHandler keeps them
    // out of OnWorldClick — so panel buttons don't act on the world.
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
        // card / sell-button highlight (the controller has already gone back to the empty hand).
        public event Action ToolCleared;

        private PlacementContext context;
        private readonly IPlacementTool emptyHand = new EmptyHand();   // the default: nothing selected
        private SellTool sellTool;
        private IPlacementTool tool;

        private bool active;

        // A build card or the sell tool is in hand: the next left click in the world is placement's.
        // False outside build mode and with nothing selected — the click is a tap then.
        public bool HoldsTool => active && tool != null && tool != emptyHand;

        private void OnEnable()
        {
            inputHandler.OnWorldClick += OnWorldClick;
            inputHandler.OnRightClick += OnRightClick;
        }

        private void OnDisable()
        {
            inputHandler.OnWorldClick -= OnWorldClick;
            inputHandler.OnRightClick -= OnRightClick;
        }

        // Called by BuildModeState.Enter. Show the overlay; the panel drives which structure is selected.
        public void Begin()
        {
            EnsureTools();
            active = true;
            gridOverlay.Show();
        }

        // Called by BuildModeState.Exit. The active tool tears down whatever it owns (a ghost, a tint).
        public void End()
        {
            EnsureTools();
            tool.Exit();
            tool = emptyHand;
            active = false;
            gridOverlay.Hide();
        }

        // Choose which structure to place (BuildPanelUI calls this). A null def means "nothing selected"
        // → back to the empty hand.
        public void Select(StructureDef def)
        {
            EnsureTools();
            SwitchTo(def != null ? new PlaceTool(context, def) : emptyHand);
        }

        // Switch to the Sell tool (BuildPanelUI's sell button calls this).
        public void SetSellMode()
        {
            EnsureTools();
            SwitchTo(sellTool);
        }

        // The single path between tools. Exit before Enter, always — that ordering is what guarantees a
        // ghost or a tint can never survive into the next tool.
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
            sellTool = new SellTool(context);
            tool = emptyHand;
        }

        private void Update()
        {
            if (!active) return;
            tool.Tick(ScreenToWorld());
        }

        private void OnWorldClick(Vector2 worldPos)
        {
            if (!active) return;
            tool.Click(worldPos);
        }

        // Right-click = cancel the current action, like any strategy game. The tool gets first refusal:
        // if it had something in progress it consumes the click, otherwise the selection is cleared.
        // Over the panel too: cancel means the same wherever the cursor is.
        private void OnRightClick(Vector2 worldPos)
        {
            if (!active) return;

            if (tool.Cancel()) return;       // the tool had something in progress and undid it
            if (tool == emptyHand) return;   // nothing selected — nothing to clear

            SwitchTo(emptyHand);
            ToolCleared?.Invoke();           // let the panel drop its card / sell highlight
        }

        private Vector2 ScreenToWorld()
        {
            Vector2 screen = Mouse.current.position.ReadValue();
            return mainCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        }

        // Nothing in hand. A tool that does nothing rather than a null, so every path above can talk to
        // `tool` without asking first; the click it ignores is TapSystem's (see HoldsTool).
        private sealed class EmptyHand : IPlacementTool
        {
            public void Enter() { }
            public void Exit() { }
            public void Tick(Vector2 cursor) { }
            public void Click(Vector2 world) { }
            public bool Cancel() => false;
        }
    }
}
