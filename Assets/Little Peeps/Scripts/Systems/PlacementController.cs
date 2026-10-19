using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LittlePeeps
{
    // Holds the tool in hand and routes building input to it:
    //  - a build card's structure (Select)  → PlaceTool
    //  - the sell button's tool (SelectSell) → SellTool
    //  - nothing (PutDown)                   → the empty hand: no tool, and the click is a tap (TapSystem)
    // There is no build mode: the panel is always up during normal play and the game runs underneath,
    // so "something in hand" (HoldsTool) is what decides whether a left click builds or taps — and
    // whether the grid shows, as a spot around the cursor (GridOverlay).
    //
    // The ONE owner of what is in hand. The build UI only asks (a card, the sell button, the build toggle
    // hiding the panel) and draws what ToolChanged reports — every change goes out, whoever made it, so no
    // widget keeps a copy that could disagree with another's. Right-click puts the tool down. Active only
    // between Begin()/End(), called by PlayingState: any other screen (a perk pick, an age transition, the
    // end of an offering cycle) empties the hand, and a request made outside normal play is refused.
    // Clicks over UI never get here — InputHandler keeps them out of OnWorldClick — so panel buttons don't
    // act on the world.
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

        // Raised on EVERY change of what is in hand — a card or the sell button taking a tool, a click or a
        // right-click putting it down, a limit reached, normal play ending. The UI reads HeldStructure /
        // HoldsSell here instead of remembering what it asked for.
        public event Action ToolChanged;

        // What is in hand, for the UI to draw: the structure a build card asked for, or null.
        public StructureDef HeldStructure { get; private set; }

        // The sell tool is in hand.
        public bool HoldsSell => sellTool != null && tool == sellTool;

        private PlacementContext context;
        private readonly IPlacementTool emptyHand = new EmptyHand();   // the default: nothing selected
        private SellTool sellTool;
        private IPlacementTool tool;

        private bool active;

        // A build card or the sell tool is in hand: the next left click in the world is placement's.
        // False outside normal play and with nothing selected — the click is a tap then.
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

        // Called by PlayingState.Enter. From here on the build UI may put a tool in hand.
        public void Begin()
        {
            EnsureTools();
            active = true;
        }

        // Called by PlayingState.Exit. Whatever was in hand goes down, and the active tool tears down
        // whatever it owns (a ghost, a tint).
        public void End()
        {
            EnsureTools();
            active = false;
            SwitchTo(emptyHand);
            gridOverlay.Hide();
        }

        // Take a structure into the hand (a build card asks). Null is the empty hand, as PutDown.
        public void Select(StructureDef def)
        {
            if (def == null)
            {
                PutDown();
                return;
            }

            if (!active) return;   // outside normal play nothing can be taken
            EnsureTools();
            SwitchTo(new PlaceTool(context, def), def);
        }

        // Take the sell tool into the hand (the sell button or its key asks).
        public void SelectSell()
        {
            if (!active) return;
            EnsureTools();
            SwitchTo(sellTool);
        }

        // Back to the empty hand. Harmless with nothing in hand, so a caller never has to ask first.
        public void PutDown()
        {
            EnsureTools();
            SwitchTo(emptyHand);
        }

        // The single path between tools. Exit before Enter, always — that ordering is what guarantees a
        // ghost or a tint can never survive into the next tool. Switching to the tool already in hand is no
        // change and reports none; a new PlaceTool is always a change.
        private void SwitchTo(IPlacementTool next, StructureDef held = null)
        {
            if (next == tool) return;

            tool.Exit();
            tool = next;
            HeldStructure = held;
            tool.Enter();
            ToolChanged?.Invoke();
        }

        // Built once, lazily, so the tools exist no matter which entry point fires first.
        private void EnsureTools()
        {
            if (context != null) return;

            visuals.Init(structureSystem);
            context = new PlacementContext(islandSystem, structureSystem, resourceSystem, visuals);
            sellTool = new SellTool(context);
            tool = emptyHand;
        }

        private void Update()
        {
            if (!active) return;

            // Runs for the whole of normal play now, not just inside a build mode — so no pointer (none
            // attached) has to be a quiet frame, not an exception every frame.
            if (Mouse.current == null)
            {
                gridOverlay.Hide();
                return;
            }

            Vector2 cursor = ScreenToWorld();
            tool.Tick(cursor);

            // The grid only while it means something: with a tool in hand, around where it would act.
            if (HoldsTool) gridOverlay.Track(cursor);
            else gridOverlay.Hide();
        }

        private void OnWorldClick(Vector2 worldPos)
        {
            if (!active) return;
            tool.Click(worldPos);
        }

        // Right-click = cancel the current action, like any strategy game. The tool gets first refusal:
        // if it had something in progress it consumes the click, otherwise the tool is put down.
        // Over the panel too: cancel means the same wherever the cursor is.
        private void OnRightClick(Vector2 worldPos)
        {
            if (!active) return;

            if (tool.Cancel()) return;   // the tool had something in progress and undid it
            PutDown();
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
