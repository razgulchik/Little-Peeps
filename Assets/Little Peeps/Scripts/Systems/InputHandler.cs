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
    // CameraController polls the mouse itself and asks IsPointerOverUI for the same rule.
    public class InputHandler : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;

        public event Action<Vector2> OnWorldClick;
        public event Action OnAnyClick;
        public event Action<Vector2> OnRightClick;

        // Current mouse position in world space, refreshed every frame. HasMouse is false when no
        // pointer device is present (don't trust WorldMousePosition then). Consumers that need to
        // follow the cursor every frame (e.g. TapSystem's radius ring) read these instead of polling.
        public Vector2 WorldMousePosition { get; private set; }
        public bool HasMouse { get; private set; }

        // True while the cursor is over a UI element that takes raycasts — a button, a window's backdrop,
        // the age banner while it is up. The EventSystem's own answer from its last raycast, so call it
        // from Update, not from inside an Input System callback.
        public static bool IsPointerOverUI() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void Update()
        {
            var mouse = Mouse.current;
            HasMouse = mouse != null;
            if (!HasMouse) return;

            WorldMousePosition = ToWorld(mouse);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                OnAnyClick?.Invoke();
                if (!IsPointerOverUI()) OnWorldClick?.Invoke(WorldMousePosition);
            }
            if (mouse.rightButton.wasPressedThisFrame) OnRightClick?.Invoke(WorldMousePosition);
        }

        private Vector2 ToWorld(Mouse mouse)
        {
            var screenPos = mouse.position.ReadValue();
            return mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        }
    }
}
