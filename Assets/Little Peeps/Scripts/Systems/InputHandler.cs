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

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)  OnWorldClick?.Invoke(ToWorld(mouse));
        if (mouse.rightButton.wasPressedThisFrame) OnWorldRightClick?.Invoke(ToWorld(mouse));
    }

    private Vector2 ToWorld(Mouse mouse)
    {
        var screenPos = mouse.position.ReadValue();
        return mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
    }
}
