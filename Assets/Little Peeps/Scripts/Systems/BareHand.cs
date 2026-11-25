using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The bare hand: what the left button does to the units when nothing is taken from the build panel. A tool like
    // the build card's and the sell button's, and PlacementController treats it as one — it is the tool in hand
    // whenever no other is. So it never asks whose a press is: one made with a card in hand, over a frozen screen
    // or on the UI does not come here, and one that does is its own until the button comes up.
    //
    // A press does one of two things, and one ring round the cursor says to whom (UnitReach):
    //   - a TAP pushes every unit in the ring (TapSystem);
    //   - a HOLD picks up as many as the hand has room for and carries them, and lets go of them with the boost a
    //     tap gives (CarrySystem). How many there is room for is the run's: none until a meta perk opens it
    //     (StatId.CarryCapacity).
    //
    // While the hand carries nobody, a press can only be a tap, so it is one at once, as the button goes down.
    // Once it can, nobody knows yet which of the two a press is (PressGesture tells), and the units do not wait to
    // hear: they go on as they were, and one is out of the ring in a quarter of a second, a boosted one in half a
    // click. So a press is for whoever was in the ring as the button went DOWN, wherever they have got to by the
    // time it is told:
    //   - let go in time, they are pushed, and so is whoever is in the ring as the button comes up;
    //   - held on, they are drawn to the cursor, they and whoever it has come over since (CarrySystem.PickUp).
    //
    // Whatever takes the button away — a card in hand, a screen over the game — pushes nobody, and the carried are
    // let go as if the button had come up (Exit).
    public class BareHand : MonoBehaviour, IPlacementTool
    {
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private TapSystem tapSystem;

        [Tooltip("Empty = the hand only taps, whatever the run has opened.")]
        [SerializeField] private CarrySystem carrySystem;

        [Tooltip("The ring at the cursor. Empty = no ring; the hand works all the same.")]
        [SerializeField] private TapRadiusVisual ring;

        [Header("Tap or hold")]
        [SerializeField] private PressTiming timing = new();

        private readonly PressGesture press = new();   // the press under way; only while the hand can carry
        private readonly List<Unit> tapped = new();    // whom it is for if it is a tap, nearest first
        private readonly List<Unit> reached = new();   // UnitReach's answer
        private bool inUse;                            // the tool in hand, in normal play

        private bool CanCarry => carrySystem != null && carrySystem.Capacity > 0;

        // One radius for both, until picking up is given one of its own.
        private float Reach => tapSystem.Radius;

        // May run before this object's own Awake: normal play begins inside GameBootstrap's.
        public void Enter() => inUse = true;

        public void Exit()
        {
            inUse = false;
            if (!press.IsDown) return;

            press.End();
            LetGo();
        }

        public void Press(Vector2 world)
        {
            UnitReach.Collect(world, world, Reach, tapped);
            if (!CanCarry)
            {
                tapSystem.Push(tapped);
                return;
            }

            press.Begin(inputHandler.ScreenMousePosition);
            carrySystem.Begin(world, Reach);
        }

        public void Tick(Vector2 cursor)
        {
            if (!press.IsDown) return;

            carrySystem.Follow(cursor, Reach);
            if (press.Advance(timing, Time.unscaledDeltaTime, inputHandler.ScreenMousePosition)) carrySystem.PickUp();
        }

        // Up to where the button came up, whichever ran first this frame.
        public void Release(Vector2 world)
        {
            if (!press.IsDown) return;   // a press that went to another tool, or was a tap already

            bool hold = press.IsHold;
            press.End();

            if (hold) carrySystem.Follow(world, Reach);   // the last bit of the way; the sea may let go on it
            LetGo();
            if (!hold) Tap(world);
        }

        // Nothing of the hand's to undo: a tap is done, and a hold ends with its own button.
        public bool Cancel() => false;

        // Of whoever is carried: they leave as a tapped unit does.
        private void LetGo() => carrySystem.LetGo(tapSystem.BoostMultiplier, tapSystem.BoostDuration);

        // Whoever the press was for, if a door has not taken them in meanwhile, and whoever is in the ring by now.
        private void Tap(Vector2 world)
        {
            tapped.RemoveAll(static unit => unit == null || !unit.IsOnField);

            UnitReach.Collect(world, world, Reach, reached);
            for (int i = 0; i < reached.Count; i++)
                if (!tapped.Contains(reached[i])) tapped.Add(reached[i]);

            tapSystem.Push(tapped);
        }

        // In its own time and not in Tick: the ring goes with the cursor for as long as there is one to go with.
        private void Update()
        {
            if (ring == null) return;

            bool visible = inUse && inputHandler.HasMouse;
            ring.SetVisible(visible);
            if (!visible) return;

            ring.SetRadius(Reach);
            ring.transform.position = inputHandler.WorldMousePosition;
        }
    }
}
