using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The build toggle: owns whether the build panel — the sell button, the tab column and the palette — is
    // shown or tucked away. Not a mode: the game runs the same either way, it is only how much of the screen
    // the panel takes. A click or its key (BuildPanelToggleRequestedEvent) flips it; the sell key or a tab
    // digit brings a hidden panel back, since reaching for either means wanting it — in normal play only.
    // The state goes out as BuildPanelShownEvent, which UIVisibility applies to the rows that follow it.
    //
    // Hiding puts down whatever is in hand: a tool held by an invisible panel would turn the next tap into a
    // building. Starts shown, as UIVisibility does, and keeps the player's choice across screens and runs.
    [RequireComponent(typeof(Button))]
    public class BuildPanelToggle : MonoBehaviour, IUIElement
    {
        [SerializeField] private Button button;

        [Tooltip("Optional. Shows hideLabel while the panel is up, showLabel while it is tucked away.")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private string hideLabel = "B";
        [SerializeField] private string showLabel = "B";

        private PlacementController placement;   // from UIServices: to put the tool down on hiding
        private bool shown = true;

        // Boots live, for the reason BuildPaletteUI gives.
        private bool live = true;

        public void Initialize(UIServices ui)
        {
            placement = ui.PlacementController;
        }

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            ApplyLabel();
        }

        private void OnEnable()
        {
            button.onClick.AddListener(Flip);
            EventBus<BuildPanelToggleRequestedEvent>.Subscribe(OnToggleRequested);
            EventBus<SellModeRequestedEvent>.Subscribe(OnSellHotkey);
            EventBus<BuildTabRequestedEvent>.Subscribe(OnTabHotkey);
            EventBus<UIModeChangedEvent>.Subscribe(OnUIMode);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(Flip);
            EventBus<BuildPanelToggleRequestedEvent>.Unsubscribe(OnToggleRequested);
            EventBus<SellModeRequestedEvent>.Unsubscribe(OnSellHotkey);
            EventBus<BuildTabRequestedEvent>.Unsubscribe(OnTabHotkey);
            EventBus<UIModeChangedEvent>.Unsubscribe(OnUIMode);
        }

        private void OnToggleRequested(BuildPanelToggleRequestedEvent _) => Flip();
        private void OnSellHotkey(SellModeRequestedEvent _) => Reveal();
        private void OnTabHotkey(BuildTabRequestedEvent _) => Reveal();
        private void OnUIMode(UIModeChangedEvent e) => live = e.Mode == UIMode.Playing;

        private void Flip() => SetShown(!shown);

        // Over another screen the keys leave the panel as the player left it.
        private void Reveal()
        {
            if (live) SetShown(true);
        }

        private void SetShown(bool value)
        {
            if (shown == value) return;
            shown = value;
            if (!shown && placement != null) placement.PutDown();

            ApplyLabel();
            EventBus<BuildPanelShownEvent>.Publish(new BuildPanelShownEvent { Shown = shown });
        }

        private void ApplyLabel()
        {
            if (label != null) label.text = shown ? hideLabel : showLabel;
        }
    }
}
