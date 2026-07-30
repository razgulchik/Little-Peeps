using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // Bottom-right toggle button. A click publishes BuildModeToggleRequestedEvent; the button
    // reflects mode + cooldown from BuildModeUIStateEvent (label swap + interactable). It defaults
    // to the playing state in Awake, so it does not depend on receiving an initial event.
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
            ApplyState(inBuildMode: false, interactable: true);   // default = playing, enabled
        }

        private void OnEnable()
        {
            button.onClick.AddListener(OnClick);
            EventBus<BuildModeUIStateEvent>.Subscribe(OnUIState);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(OnClick);
            EventBus<BuildModeUIStateEvent>.Unsubscribe(OnUIState);
        }

        private void OnClick()
        {
            EventBus<BuildModeToggleRequestedEvent>.Publish(new BuildModeToggleRequestedEvent());
        }

        private void OnUIState(BuildModeUIStateEvent e)
        {
            ApplyState(e.InBuildMode, e.Interactable);
        }

        private void ApplyState(bool inBuildMode, bool interactable)
        {
            if (button != null) button.interactable = interactable;
            if (modeText != null)
                modeText.text = inBuildMode ? playLabel : buildLabel;
        }
    }
}
