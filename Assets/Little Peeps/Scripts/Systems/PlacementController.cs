using System;
using UnityEngine;

namespace LittlePeeps
{
    // Holds the tool in hand and routes the left button (and the right one's cancel) to it:
    //  - a build card's structure (Select)  → PlaceTool
    //  - the sell button's tool (SelectSell) → SellTool
    //  - nothing (PutDown)                   → the bare hand, which taps and carries the units (BareHand)
    // There is no build mode: the panel is always up during normal play and the game runs underneath,
    // so what is in hand is what decides whether a press builds or goes to the units — and
    // whether the grid shows, as a spot around the cursor (GridOverlay).
    //
    // The one place that says whose a press is: the tool in hand gets it, and nothing else listens to the button.
    // A tool swapped in mid-press takes the release with nothing under way and ignores it, so no press is ever
    // answered twice: the click that puts up the last building of a limit does not tap the units under it as well.
    //
    // The ONE owner of what is in hand. The build UI only asks (a card, the sell button, the build toggle
    // hiding the panel) and draws what ToolChanged reports — every change goes out, whoever made it, so no
    // widget keeps a copy that could disagree with another's. Right-click puts the tool down. Active only
    // between Begin()/End(), called by PlayingState: any other screen (a perk pick, an age transition, the
    // end of an offering cycle) empties the hand and stops the bare one too, and a request made outside normal
    // play is refused.
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
        [SerializeField] private GridOverlay gridOverlay;

        [Tooltip("What the left button does with nothing taken from the build panel: taps and carries the units.")]
        [SerializeField] private BareHand bareHand;

        [SerializeField] private PlacementVisuals visuals = new();

        // Raised on EVERY change of what is in hand — a card or the sell button taking a tool, a click or a
        // right-click putting it down, a limit reached, normal play ending. The UI reads HeldStructure /
        // HoldsSell here instead of remembering what it asked for.
        public event Action ToolChanged;

        // The sell tool has just sold something — once per act, however many fences a stroke took.
        public event Action Sold;

        // What is in hand, for the UI to draw: the structure a build card asked for, or null.
        public StructureDef HeldStructure { get; private set; }

        // The sell tool is in hand.
        public bool HoldsSell => sellTool != null && tool == sellTool;

        private PlacementContext context;
        private IPlacementTool emptyHand;   // the default: nothing selected, so the bare hand
        private SellTool sellTool;
        private IPlacementTool tool;

        private bool active;

        // A build card or the sell tool is in hand: the next press in the world is placement's.
        private bool HoldsTool => active && tool != emptyHand;

        private void OnEnable()
        {
            inputHandler.OnWorldClick += OnWorldClick;
            inputHandler.OnWorldRelease += OnWorldRelease;
            inputHandler.OnRightClick += OnRightClick;
        }

        private void OnDisable()
        {
            inputHandler.OnWorldClick -= OnWorldClick;
            inputHandler.OnWorldRelease -= OnWorldRelease;
            inputHandler.OnRightClick -= OnRightClick;
        }

        // Called by PlayingState.Enter. From here on the bare hand works, and the build UI may put a tool in it.
        public void Begin()
        {
            EnsureTools();
            active = true;
            tool.Enter();
        }

        // Called by PlayingState.Exit. Whatever was in hand goes down, the active tool tearing down whatever it
        // owns (a ghost, a tint), and then the bare hand lets go of whatever it holds.
        public void End()
        {
            EnsureTools();
            active = false;
            SwitchTo(emptyHand);
            tool.Exit();
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
            sellTool.Sold += () => Sold?.Invoke();

            // Loud but not fatal: the panel still builds and sells.
            if (bareHand == null)
                Debug.LogError("[PlacementController] no BareHand assigned — the left button does nothing to units.", this);
            emptyHand = bareHand != null ? (IPlacementTool)bareHand : new NoHand();
            tool = emptyHand;
        }

        private void Update()
        {
            if (!active) return;

            // Runs for the whole of normal play now, not just inside a build mode — so no pointer (none
            // attached) has to be a quiet frame, not an exception every frame.
            if (!inputHandler.HasMouse)
            {
                gridOverlay.Hide();
                return;
            }

            // InputHandler's answer, not the mouse's: it holds still while a right press grabs the map.
            Vector2 cursor = inputHandler.WorldMousePosition;
            tool.Tick(cursor);

            // The grid only while it means something: with a tool in hand, around where it would act.
            if (HoldsTool) gridOverlay.Track(cursor);
            else gridOverlay.Hide();
        }

        private void OnWorldClick(Vector2 worldPos)
        {
            if (!active) return;
            tool.Press(worldPos);
        }

        private void OnWorldRelease(Vector2 worldPos)
        {
            if (!active) return;
            tool.Release(worldPos);
        }

        // Right-click = cancel the current action, like any strategy game. The tool gets first refusal:
        // if it had something in progress it consumes the click, otherwise the tool is put down.
        // Over the panel too: cancel means the same wherever the cursor is. It arrives on the release and
        // never after a right-drag — that one pans the camera (InputHandler tells the two apart).
        private void OnRightClick(Vector2 worldPos)
        {
            if (!active) return;

            if (tool.Cancel()) return;   // the tool had something in progress and undid it
            PutDown();
        }

        // The bare hand of a scene that has none wired. A tool that does nothing rather than a null, so every path
        // above can talk to `tool` without asking first.
        private sealed class NoHand : IPlacementTool
        {
            public void Enter() { }
            public void Exit() { }
            public void Tick(Vector2 cursor) { }
            public void Press(Vector2 world) { }
            public void Release(Vector2 world) { }
            public bool Cancel() => false;
        }
    }
}
