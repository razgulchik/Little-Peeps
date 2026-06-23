using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Bottom build palette. Shows while in build mode (BuildModeUIStateEvent), spawns a card per
// BuildPaletteDef entry, and drives the PlacementController's tool: click a card to place a
// structure, click the separate sell button to sell, click the selected card/button again (or
// right-click in the world, or leave build mode) to clear — which drops back to the Move tool.
// A right-click clear comes from the controller via its ToolCleared event. Mirrors BuildModeButton's
// pattern (event-driven, default-hidden in Awake).
public class BuildPanelUI : MonoBehaviour
{
    [SerializeField] private BuildPaletteDef palette;
    [SerializeField] private PlacementController placementController;
    [SerializeField] private ResourceSystem resourceSystem;
    [SerializeField] private BuildCardUI cardPrefab;
    [SerializeField] private Transform cardContainer;   // parent with a Horizontal Layout Group
    [SerializeField] private CanvasGroup canvasGroup;   // hides the panel without deactivating this listener

    [Header("Sell tool")]
    [SerializeField] private Button sellButton;          // separate sell-tool button (not a card)
    [SerializeField] private GameObject sellHighlight;   // selected indicator on the sell button

    private readonly List<BuildCardUI> cards = new();
    private BuildCardUI selectedCard;
    private bool sellSelected;
    private bool isOpen;   // true while in build mode (panel visible) — gates the sell hotkey

    private void Awake()
    {
        BuildCards();
        SetSellHighlight(false);
        SetVisible(false);   // default hidden; no dependency on receiving an initial event
    }

    private void OnEnable()
    {
        EventBus<BuildModeUIStateEvent>.Subscribe(OnUIState);
        EventBus<BuildDeniedEvent>.Subscribe(OnBuildDenied);
        EventBus<SellModeRequestedEvent>.Subscribe(OnSellHotkey);
        if (sellButton != null) sellButton.onClick.AddListener(OnSellClicked);
        if (placementController != null) placementController.ToolCleared += OnToolCleared;
    }

    private void OnDisable()
    {
        EventBus<BuildModeUIStateEvent>.Unsubscribe(OnUIState);
        EventBus<BuildDeniedEvent>.Unsubscribe(OnBuildDenied);
        EventBus<SellModeRequestedEvent>.Unsubscribe(OnSellHotkey);
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
    }

    private void OnUIState(BuildModeUIStateEvent e)
    {
        if (e.InBuildMode) Open();
        else Close();
    }

    private void Open()
    {
        isOpen = true;
        RefreshAffordability();
        SetVisible(true);
    }

    private void Close()
    {
        isOpen = false;
        Deselect();
        SetVisible(false);
    }

    // Sell hotkey: route through the same toggle path as the sell button so the highlight and the
    // PlacementController stay in sync. Ignored when the panel is closed (not in build mode).
    private void OnSellHotkey(SellModeRequestedEvent _)
    {
        if (isOpen) OnSellClicked();
    }

    private void OnCardClicked(BuildCardUI card)
    {
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
    private void RefreshAffordability()
    {
        foreach (var card in cards)
            card.SetAffordable(resourceSystem.CanAfford(card.Def.cost));
    }

    private void OnBuildDenied(BuildDeniedEvent e)
    {
        if (selectedCard != null) selectedCard.PlayDeniedCue();
    }

    // Hide via CanvasGroup (not SetActive) so this component stays active and keeps listening for
    // the next build-mode event.
    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
