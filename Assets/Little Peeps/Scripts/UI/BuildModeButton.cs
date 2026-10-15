using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // Bottom-right toggle button. A click publishes BuildModeToggleRequestedEvent; the label follows the
    // UI mode — one label while the build panel is up, the other otherwise. Building no longer stops the
    // game, so there is nothing else to reflect: the button is always clickable. It defaults to the
    // playing label in Awake, so it does not depend on receiving an initial event.
    [RequireComponent(typeof(Button))]
    public class BuildModeButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private string buildLabel = "B";
        [SerializeField] private string playLabel = ">";

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            ApplyLabel(inBuildMode: false);   // default = playing
        }

        private void OnEnable()
        {
            button.onClick.AddListener(OnClick);
            EventBus<UIModeChangedEvent>.Subscribe(OnUIMode);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(OnClick);
            EventBus<UIModeChangedEvent>.Unsubscribe(OnUIMode);
        }

        private void OnClick()
        {
            EventBus<BuildModeToggleRequestedEvent>.Publish(new BuildModeToggleRequestedEvent());
        }

        private void OnUIMode(UIModeChangedEvent e) => ApplyLabel(e.Mode == UIMode.Build);

        private void ApplyLabel(bool inBuildMode)
        {
            if (modeText != null)
                modeText.text = inBuildMode ? playLabel : buildLabel;
        }
    }
}
