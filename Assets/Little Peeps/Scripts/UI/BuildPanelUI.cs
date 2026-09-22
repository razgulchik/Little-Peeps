using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // Bottom build palette. Its CARDS follow the UI mode (UIModeChangedEvent); whether the panel is on
    // screen is UIVisibility's table, not this class. Spawns a card per BuildPaletteDef entry and drives
    // the PlacementController's tool: click a card to place a structure, click the separate sell button
    // to sell, click the selected card/button again (or right-click in the world, or leave build mode) to
    // clear — which drops back to the Move tool. A right-click clear comes from the controller via its
    // ToolCleared event.
    public class BuildPanelUI : MonoBehaviour
    {
        [SerializeField] private BuildPaletteDef palette;
        [SerializeField] private PlacementController placementController;
        [SerializeField] private ResourceSystem resourceSystem;
        [SerializeField] private BuildCardUI cardPrefab;
        [SerializeField] private Transform cardContainer;   // parent with a Horizontal Layout Group
        [SerializeField] private BuildPanelScroller scroller;

        [Header("Sell tool")]
        [SerializeField] private Button sellButton;          // separate sell-tool button (not a card)
        [SerializeField] private GameObject sellHighlight;   // selected indicator on the sell button

        private readonly List<BuildCardUI> cards = new();
        private BuildCardUI selectedCard;
        private bool sellSelected;
        private bool isOpen;   // true while in build mode (panel visible) — gates the sell hotkey
        private int currentAge;

        private void Awake()
        {
            BuildCards();
            SetSellHighlight(false);
        }

        private void OnEnable()
        {
            EventBus<UIModeChangedEvent>.Subscribe(OnUIMode);
            EventBus<BuildDeniedEvent>.Subscribe(OnBuildDenied);
            EventBus<SellModeRequestedEvent>.Subscribe(OnSellHotkey);
            EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
            if (sellButton != null) sellButton.onClick.AddListener(OnSellClicked);
            if (placementController != null) placementController.ToolCleared += OnToolCleared;
        }

        private void OnDisable()
        {
            EventBus<UIModeChangedEvent>.Unsubscribe(OnUIMode);
            EventBus<BuildDeniedEvent>.Unsubscribe(OnBuildDenied);
            EventBus<SellModeRequestedEvent>.Unsubscribe(OnSellHotkey);
            EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
            if (sellButton != null) sellButton.onClick.RemoveListener(OnSellClicked);
            if (placementController != null) placementController.ToolCleared -= OnToolCleared;
        }

        private void BuildCards()
        {
            if (palette == null || cardPrefab == null || cardContainer == null) return;

            foreach (var def in palette.structures)
            {
                if (def == null) continue;
                var card = Instantiate(cardPrefab, cardContainer);
                card.Init(def, OnCardClicked);
                cards.Add(card);
            }

            if (scroller != null) scroller.ResetToStart();
        }

        // The palette's CONTENT follows the mode; its visibility is UIVisibility's row for this group.
        // Open/Close are about the cards and the selected tool, which is why leaving build mode still has
        // to run through here and cannot just be a CanvasGroup going to zero.
        //
        // Only the CROSSING is acted on. Every mode now reports itself, so without this an age
        // transition or a perk pick would re-run Close() — and with it Deselect(), driving the
        // PlacementController back to the Move tool it is already on, once per screen the player sees.
        private void OnUIMode(UIModeChangedEvent e)
        {
            bool build = e.Mode == UIMode.Build;
            if (build == isOpen) return;

            if (build) Open();
            else Close();
        }

        private void Open()
        {
            isOpen = true;
            RefreshCards();
            if (scroller != null) scroller.RefreshBounds();
        }

        private void Close()
        {
            isOpen = false;
            Deselect();
            foreach (var card in cards) card.ResetInteractionVisuals();
        }

        // Sell hotkey: route through the same toggle path as the sell button so the highlight and the
        // PlacementController stay in sync. Ignored when the panel is closed (not in build mode).
        private void OnSellHotkey(SellModeRequestedEvent _)
        {
            if (isOpen) OnSellClicked();
        }

        private void OnCardClicked(BuildCardUI card)
        {
            if (card == null || card.IsLocked) return;
            if (card == selectedCard) { Deselect(); return; }   // clicking the selected card deselects

            ClearSell();                                        // a card and the sell tool are mutually exclusive
            if (selectedCard != null) selectedCard.SetSelected(false);
            selectedCard = card;
            selectedCard.SetSelected(true);
            placementController.Select(card.Def);
        }

        // Separate sell button: toggles the Sell tool. Selecting it clears any card selection;
        // clicking it again deselects back to the Move tool.
        private void OnSellClicked()
        {
            if (sellSelected) { Deselect(); return; }

            if (selectedCard != null) selectedCard.SetSelected(false);
            selectedCard = null;
            sellSelected = true;
            SetSellHighlight(true);
            placementController.SetSellMode();
        }

        // UI-initiated deselect (clicking the selected card/sell again, or leaving build mode): clear
        // the highlights AND drive the controller back to the Move tool.
        private void Deselect()
        {
            ClearSelectionUI();
            placementController.Select(null);   // null selection = Move tool
        }

        // The controller cleared the tool itself (right-click) — it's already on Move, so only sync the
        // UI highlights; don't drive Select() again.
        private void OnToolCleared()
        {
            ClearSelectionUI();
        }

        // Drop both the card highlight and the sell highlight, without touching the controller.
        private void ClearSelectionUI()
        {
            if (selectedCard != null) selectedCard.SetSelected(false);
            selectedCard = null;
            ClearSell();
        }

        private void ClearSell()
        {
            sellSelected = false;
            SetSellHighlight(false);
        }

        private void SetSellHighlight(bool on)
        {
            if (sellHighlight != null) sellHighlight.SetActive(on);
        }

        // Resources don't change inside build mode (game paused), so refreshing on open is enough.
        private void RefreshCards()
        {
            foreach (var card in cards)
            {
                bool locked = card.Def != null && currentAge < card.Def.requiredAge;
                card.SetLocked(locked, currentAge);
                card.SetAffordable(resourceSystem == null || resourceSystem.CanAfford(card.Def.cost));

                if (locked && card == selectedCard) Deselect();
            }
        }

        private void OnAgeStarted(AgeStartedEvent e)
        {
            currentAge = e.Age;
            RefreshCards();
        }

        private void OnRunStarted(RunStartedEvent e)
        {
            currentAge = e.Run != null ? e.Run.currentAge : 0;
            RefreshCards();
        }

        private void OnBuildDenied(BuildDeniedEvent e)
        {
            if (selectedCard != null && selectedCard.Def == e.Def) selectedCard.PlayDeniedCue();
        }
    }
}
