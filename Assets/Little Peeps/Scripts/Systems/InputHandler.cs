using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Raw input router. Converts screen coords to world and fires events.
// Place on a dedicated "Input" GameObject in the scene hierarchy.
public class InputHandler : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;

    public event Action<Vector2> OnWorldClick;
    public event Action<Vector2> OnWorldRightClick;

    // Current mouse position in world space, refreshed every frame. HasMouse is false when no
    // pointer device is present (don't trust WorldMousePosition then). Consumers that need to
    // follow the cursor every frame (e.g. TapSystem's radius ring) read these instead of polling.
    public Vector2 WorldMousePosition { get; private set; }
    public bool HasMouse { get; private set; }

    private void Update()
    {
        var mouse = Mouse.current;
        HasMouse = mouse != null;
        if (!HasMouse) return;

        WorldMousePosition = ToWorld(mouse);

        if (mouse.leftButton.wasPressedThisFrame)  OnWorldClick?.Invoke(WorldMousePosition);
        if (mouse.rightButton.wasPressedThisFrame) OnWorldRightClick?.Invoke(WorldMousePosition);
    }

    private Vector2 ToWorld(Mouse mouse)
    {
        var screenPos = mouse.position.ReadValue();
        return mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
    }
}
