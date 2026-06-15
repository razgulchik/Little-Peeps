using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Raw input router. Converts screen coords to world and fires events.
// Place on a dedicated "Input" GameObject in the scene hierarchy.
public class InputHandler : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;

    public event Action<Vector2> OnWorldClick;

    private void Update()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        var screenPos = Mouse.current.position.ReadValue();
        var worldPos  = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        OnWorldClick?.Invoke(worldPos);
    }
}
