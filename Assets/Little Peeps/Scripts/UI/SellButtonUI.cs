using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The sell button, a build panel widget of its own. A click — or the sell key (SellModeRequestedEvent),
    // which goes the same way — takes the sell tool into the hand, or puts it down if it is already there.
    // It only ASKS PlacementController; whether it shows as selected is read back from the controller
    // (ToolChanged), so a card taking the hand, a right-click or the end of normal play clear it without
    // anybody telling the button. Outside normal play the controller refuses, which makes the key a no-op
    // over any other screen.
    [RequireComponent(typeof(Button))]
    public class SellButtonUI : MonoBehaviour, IUIElement
    {
        [SerializeField] private Button button;

        [Tooltip("Shown while the sell tool is in hand.")]
        [SerializeField] private GameObject selectedFrame;

        private PlacementController placement;   // from UIServices: the tool in hand

        // From UIRoot, once, in GameBootstrap.Awake — possibly before Awake. Heard until OnDestroy: the
        // controller lives as long as the Canvas.
        public void Initialize(UIServices ui)
        {
            if (placement != null) placement.ToolChanged -= OnToolChanged;
            placement = ui.PlacementController;
            if (placement != null) placement.ToolChanged += OnToolChanged;
        }

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            ShowSelected(placement != null && placement.HoldsSell);
        }

        private void OnDestroy()
        {
            if (placement != null) placement.ToolChanged -= OnToolChanged;
        }

        private void OnEnable()
        {
            button.onClick.AddListener(Toggle);
            EventBus<SellModeRequestedEvent>.Subscribe(OnSellHotkey);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(Toggle);
            EventBus<SellModeRequestedEvent>.Unsubscribe(OnSellHotkey);
        }

        private void OnSellHotkey(SellModeRequestedEvent _) => Toggle();

        private void Toggle()
        {
            if (placement == null) return;

            if (placement.HoldsSell) placement.PutDown();
            else placement.SelectSell();
        }

        private void OnToolChanged() => ShowSelected(placement.HoldsSell);

        private void ShowSelected(bool value)
        {
            if (selectedFrame != null) selectedFrame.SetActive(value);
        }
    }
}
