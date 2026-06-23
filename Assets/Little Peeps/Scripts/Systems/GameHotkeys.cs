using UnityEngine;
using UnityEngine.InputSystem;

// Discrete command hotkeys → published as EventBus events, keeping the input layer decoupled from
// whatever handles each command (FSM, build panel, info UI). Continuous camera movement is NOT here —
// it lives in CameraController. Bindings are editable in the inspector; defaults B / X / Esc / I.
//
// Place on a dedicated "Input" / "Hotkeys" GameObject. Self-contained: no wiring beyond the scene.
public class GameHotkeys : MonoBehaviour
{
    [SerializeField] private Key buildModeKey  = Key.B;    // toggle build mode
    [SerializeField] private Key sellKey       = Key.X;    // toggle the sell tool (only in build mode)
    [SerializeField] private Key exitToMenuKey = Key.Escape;
    [SerializeField] private Key infoKey       = Key.I;    // toggle the info window

    private void Update()
    {
        var k = Keyboard.current;
        if (k == null) return;

        if (k[buildModeKey].wasPressedThisFrame)  EventBus<BuildModeToggleRequestedEvent>.Publish(default);
        if (k[sellKey].wasPressedThisFrame)       EventBus<SellModeRequestedEvent>.Publish(default);
        if (k[exitToMenuKey].wasPressedThisFrame) EventBus<ExitToMenuRequestedEvent>.Publish(default);
        if (k[infoKey].wasPressedThisFrame)       EventBus<InfoToggleRequestedEvent>.Publish(default);
    }
}
