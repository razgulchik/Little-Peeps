using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LittlePeeps
{
    // Raw input router. Converts screen coords to world and fires events.
    // Place on a dedicated "Input" GameObject in the scene hierarchy.
    //
    // The game and the UI share one mouse, and a press over UI belongs to the UI: OnWorldClick fires only
    // while the cursor is NOT over a UI element that takes raycasts, so a click on a button or a window
    // never also boosts the people under it or places a building through it. The other two are left
    // unfiltered on purpose: OnAnyClick is a tap anywhere on the screen (the age transition's skip, whose
    // banner IS UI), and OnRightClick is cancel, which means the same wherever the cursor is.
    // OnWorldRelease closes an OnWorldClick: it fires when the button that pressed on the world comes up,
    // wherever the cursor is by then — like the right-drag, a hold that started on the world may cross
    // the panel without letting go. A press over UI gets no release. The two always come in pairs, never two
    // presses in a row: whoever took the press is told it is over before the next one.
    // What the left button MEANS is not decided here: PlacementController gives it to the tool in hand.
    //
    // The right button does two things, told apart by how far the cursor travels while it is down — the
    // EventSystem's Drag Threshold, the same line the UI draws between a click and a drag:
    //   - released within it   → OnRightClick (cancel). On the RELEASE, not the press: at the press it is
    //                            not known yet whether this is a click or the start of a drag.
    //   - carried past it      → a drag, never a cancel. If it was pressed on the world it pans:
    //                            OnPan reports each frame's screen-pixel move while IsPanning, counted
    //                            from the press, so the world point grabbed stays under the cursor. Pressed
    //                            over UI, it does nothing — like a left press, a drag starts on the world.
    // CameraController takes the pan from here, so there is one rule for the right button, not two.
    public class InputHandler : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;

        public event Action<Vector2> OnWorldClick;
        public event Action<Vector2> OnWorldRelease;
        public event Action OnAnyClick;
        public event Action<Vector2> OnRightClick;
        public event Action<Vector2> OnPan;   // screen-pixel move of a right-drag that started on the world

        // True from the frame a right-drag on the world passes the threshold until the button comes up.
        public bool IsPanning { get; private set; }

        private bool worldPressHeld;   // the left button went down on the world and is not up yet

        private bool rightHeld;          // the right button went down and is not up yet
        private bool rightDragged;       // ...and has been carried past the threshold: a drag, not a click
        private bool rightPressOnWorld;  // ...and went down off the UI, so a drag may pan
        private Vector2 rightPressScreen;
        private Vector2 rightPressWorld;  // where it went down, in the world: WorldMousePosition while held
        private Vector2 lastPanScreen;

        // Where the cursor points in the world — the one answer for everything that follows it (the tool's
        // ghost and fence stroke, the grid spot, the tap ring). HasMouse is false when no pointer device is
        // present (don't trust WorldMousePosition then).
        // While a right press that went down on the world is held, the point stays where it went down: that
        // press is a cancel or a grab of the map, and while the map slides under the cursor (the camera
        // catches up with its damping) nothing that follows the cursor should slide across the island.
        // Worked out on every read, so it does not matter whose Update runs first.
        public Vector2 WorldMousePosition => HoldsWorldGrab ? rightPressWorld : ToWorld(Mouse.current);
        public bool HasMouse => Mouse.current != null;

        // Where the cursor is on the screen, in pixels: for telling a click from a drag, which is how far the HAND
        // has moved and must not depend on the camera. Zero with no pointer device.
        public Vector2 ScreenMousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        // The right button went down on the world and is still down.
        private bool HoldsWorldGrab => rightHeld && rightPressOnWorld;

        // True while the cursor is over a UI element that takes raycasts — a button, a window's backdrop,
        // the age banner while it is up. The EventSystem's own answer from its last raycast, so call it
        // from Update, not from inside an Input System callback.
        public static bool IsPointerOverUI() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                ReleaseWorldPress();   // up and down again inside one frame: the press before this one is over
                OnAnyClick?.Invoke();
                if (!IsPointerOverUI())
                {
                    worldPressHeld = true;
                    OnWorldClick?.Invoke(WorldMousePosition);
                }
            }
            if (!mouse.leftButton.isPressed) ReleaseWorldPress();
            HandleRightButton(mouse);
        }

        private void ReleaseWorldPress()
        {
            if (!worldPressHeld) return;
            worldPressHeld = false;
            OnWorldRelease?.Invoke(WorldMousePosition);
        }

        // Click or drag — see the class comment. A press and release inside one frame is a click.
        private void HandleRightButton(Mouse mouse)
        {
            Vector2 screen = mouse.position.ReadValue();

            if (mouse.rightButton.wasPressedThisFrame)
            {
                rightHeld = true;
                rightDragged = false;
                rightPressOnWorld = !IsPointerOverUI();
                rightPressScreen = screen;
                rightPressWorld = ToWorld(mouse);
            }
            if (!rightHeld) return;

            if (!rightDragged && (screen - rightPressScreen).sqrMagnitude >= DragThresholdSqr())
            {
                rightDragged = true;
                if (rightPressOnWorld)
                {
                    IsPanning = true;
                    lastPanScreen = rightPressScreen;   // the first step carries the threshold travel too
                }
            }

            if (IsPanning)
            {
                Vector2 delta = screen - lastPanScreen;
                lastPanScreen = screen;
                if (delta != Vector2.zero) OnPan?.Invoke(delta);
            }

            if (!mouse.rightButton.isPressed)
            {
                rightHeld = false;
                IsPanning = false;
                if (!rightDragged) OnRightClick?.Invoke(WorldMousePosition);
            }
        }

        // The UI's own click-vs-drag line, so a right-drag on the world and a drag of the build strip
        // start at the same travel. Without an EventSystem any travel at all is a drag.
        private static float DragThresholdSqr()
        {
            float px = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 0f;
            return px * px;
        }

        // No pointer → zero; readers check HasMouse first.
        private Vector2 ToWorld(Mouse mouse)
        {
            if (mouse == null) return Vector2.zero;
            var screenPos = mouse.position.ReadValue();
            return mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        }
    }
}
