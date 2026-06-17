using UnityEngine;
using UnityEngine.UI;

// Bottom-right toggle button. A click publishes BuildModeToggleRequestedEvent; the button
// reflects mode + cooldown from BuildModeUIStateEvent (icon swap + interactable). It defaults
// to the playing state in Awake, so it does not depend on receiving an initial event.
[RequireComponent(typeof(Button))]
public class BuildModeButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;   // graphic whose sprite is swapped
    [SerializeField] private Sprite buildIcon;   // shown while playing (click → enter build mode)
    [SerializeField] private Sprite playIcon;    // shown in build mode (click → resume play)

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
        if (iconImage != null && buildIcon != null && playIcon != null)
            iconImage.sprite = inBuildMode ? playIcon : buildIcon;
    }
}
