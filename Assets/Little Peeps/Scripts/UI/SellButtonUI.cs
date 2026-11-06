using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The sell button, a build panel widget of its own. A click — or the sell key (SellModeRequestedEvent),
    // which goes the same way — takes the sell tool into the hand, or puts it down if it is already there.
    // It only ASKS PlacementController; how it looks is read back from the controller (ToolChanged), so a
    // card taking the hand, a right-click or the end of normal play clear it without anybody telling the
    // button. Outside normal play the controller refuses, which makes the key a no-op over any other screen.
    //
    // The Button itself draws all of it. Its own sprites in the prefab are the look with the hand free; with
    // the tool in hand it swaps to the active ones, so hover and press keep working in both. A sale flashes
    // soldSprite in place of activeSprite. The Button's Navigation is None and its Selected sprite is empty:
    // Selected is UI focus, which says nothing about the tool and outlasts it.
    [RequireComponent(typeof(Button))]
    public class SellButtonUI : MonoBehaviour, IUIElement
    {
        [SerializeField] private Button button;

        [Header("Sell tool in hand")]
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite activeHoverSprite;

        [Header("A sale")]
        [Tooltip("Takes activeSprite's place for soldSeconds each time something is sold.")]
        [SerializeField] private Sprite soldSprite;
        [Min(0f)]
        [SerializeField] private float soldSeconds = 0.2f;

        private PlacementController placement;   // from UIServices: the tool in hand

        private Sprite freeSprite;               // the Button's own sprites, as the prefab has them
        private SpriteState freeState;
        private SpriteState activeState;
        private float soldLeft;                  // unscaled: the flash is UI, whatever speed the game runs at

        // From UIRoot, once, in GameBootstrap.Awake — possibly before Awake. Heard until OnDestroy: the
        // controller lives as long as the Canvas.
        public void Initialize(UIServices ui)
        {
            Unhook();
            placement = ui.PlacementController;
            if (placement == null) return;

            placement.ToolChanged += Draw;
            placement.Sold += OnSold;
        }

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();

            freeSprite = button.image.sprite;
            freeState = button.spriteState;
            activeState = freeState;
            activeState.highlightedSprite = activeHoverSprite;

            Draw();
        }

        private void OnDestroy() => Unhook();

        private void Unhook()
        {
            if (placement == null) return;

            placement.ToolChanged -= Draw;
            placement.Sold -= OnSold;
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

        private void Update()
        {
            if (soldLeft <= 0f) return;

            soldLeft -= Time.unscaledDeltaTime;
            if (soldLeft <= 0f) Draw();
        }

        private void OnSellHotkey(SellModeRequestedEvent _) => Toggle();

        private void Toggle()
        {
            if (placement == null) return;

            if (placement.HoldsSell) placement.PutDown();
            else placement.SelectSell();
        }

        private void OnSold()
        {
            soldLeft = soldSeconds;
            Draw();
        }

        // The spriteState setter re-applies the Button's current state, so a tool taken or put down with the
        // cursor still over the button shows the right hover at once.
        private void Draw()
        {
            bool held = placement != null && placement.HoldsSell;
            if (!held) soldLeft = 0f;   // put down mid-flash: nothing is left to come back to

            button.spriteState = held ? activeState : freeState;
            button.image.sprite = !held ? freeSprite : soldLeft > 0f ? soldSprite : activeSprite;
        }
    }
}
