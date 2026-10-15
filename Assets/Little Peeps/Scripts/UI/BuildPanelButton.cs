using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The build button: shows or hides the build palette and the sell button, nothing else — building
    // itself has no mode, the panel is just out of the way or not. A click asks (BuildPanelToggleRequestedEvent,
    // the same request the hotkey sends); BuildPanelUI owns the state and answers with BuildPanelShownEvent,
    // which only swaps the label here. Starts on the "shown" label, as the panel starts shown.
    [RequireComponent(typeof(Button))]
    public class BuildPanelButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("Optional. Shows hideLabel while the panel is up, showLabel while it is tucked away.")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private string hideLabel = "B";
        [SerializeField] private string showLabel = "B";

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            ApplyLabel(shown: true);
        }

        private void OnEnable()
        {
            button.onClick.AddListener(OnClick);
            EventBus<BuildPanelShownEvent>.Subscribe(OnShown);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(OnClick);
            EventBus<BuildPanelShownEvent>.Unsubscribe(OnShown);
        }

        private void OnClick() => EventBus<BuildPanelToggleRequestedEvent>.Publish(default);

        private void OnShown(BuildPanelShownEvent e) => ApplyLabel(e.Shown);

        private void ApplyLabel(bool shown)
        {
            if (label != null) label.text = shown ? hideLabel : showLabel;
        }
    }
}
