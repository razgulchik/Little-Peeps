using UnityEngine;
using UnityEngine.InputSystem;

namespace LittlePeeps
{
    // Discrete command hotkeys → published as EventBus events, keeping the input layer decoupled from
    // whatever handles each command (FSM, build panel). Continuous camera movement is NOT here —
    // it lives in CameraController. Bindings are editable in the inspector; defaults B / X / Esc.
    // Digits 1–9 (top row or numpad) open the build panel's tabs and are fixed: the key IS the tab's
    // position, so there is nothing to rebind.
    //
    // Place on a dedicated "Input" / "Hotkeys" GameObject. Self-contained: no wiring beyond the scene.
    public class GameHotkeys : MonoBehaviour
    {
        [SerializeField] private Key buildModeKey  = Key.B;    // toggle build mode
        [SerializeField] private Key sellKey       = Key.X;    // toggle the sell tool (only in build mode)
        [SerializeField] private Key exitToMenuKey = Key.Escape;

        private static readonly Key[] TabKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };
        private static readonly Key[] TabNumpadKeys =
        {
            Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5,
            Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9,
        };

        private void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;

            if (k[buildModeKey].wasPressedThisFrame)  EventBus<BuildModeToggleRequestedEvent>.Publish(default);
            if (k[sellKey].wasPressedThisFrame)       EventBus<SellModeRequestedEvent>.Publish(default);
            if (k[exitToMenuKey].wasPressedThisFrame) EventBus<ExitToMenuRequestedEvent>.Publish(default);

            for (int i = 0; i < TabKeys.Length; i++)
            {
                if (k[TabKeys[i]].wasPressedThisFrame || k[TabNumpadKeys[i]].wasPressedThisFrame)
                    EventBus<BuildTabRequestedEvent>.Publish(new BuildTabRequestedEvent { Index = i });
            }
        }
    }
}
