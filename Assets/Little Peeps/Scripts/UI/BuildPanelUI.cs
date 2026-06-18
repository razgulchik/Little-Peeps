using System.Collections.Generic;
using UnityEngine;

// Bottom build palette. Shows while in build mode (BuildModeUIStateEvent), spawns a card per
// BuildPaletteDef entry, and drives the PlacementController's selection: click a card to pick a
// structure (ghost follows the cursor), click it again to deselect. Leaving build mode clears
// the selection. Mirrors BuildModeButton's pattern (event-driven, default-hidden in Awake).
public class BuildPanelUI : MonoBehaviour
{
    [SerializeField] private BuildPaletteDef palette;
    [SerializeField] private PlacementController placementController;
    [SerializeField] private ResourceSystem resourceSystem;
    [SerializeField] private BuildCardUI cardPrefab;
    [SerializeField] private Transform cardContainer;   // parent with a Horizontal Layout Group
    [SerializeField] private CanvasGroup canvasGroup;   // hides the panel without deactivating this listener

    private readonly List<BuildCardUI> cards = new();
    private BuildCardUI selectedCard;

    private void Awake()
    {
        BuildCards();
        SetVisible(false);   // default hidden; no dependency on receiving an initial event
    }

    private void OnEnable()
    {
        EventBus<BuildModeUIStateEvent>.Subscribe(OnUIState);
        EventBus<BuildDeniedEvent>.Subscribe(OnBuildDenied);
    }

    private void OnDisable()
    {
        EventBus<BuildModeUIStateEvent>.Unsubscribe(OnUIState);
        EventBus<BuildDeniedEvent>.Unsubscribe(OnBuildDenied);
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
        RefreshAffordability();
        SetVisible(true);
    }

    private void Close()
    {
        Deselect();
        SetVisible(false);
    }

    private void OnCardClicked(BuildCardUI card)
    {
        if (card == selectedCard) { Deselect(); return; }   // clicking the selected card deselects

        if (selectedCard != null) selectedCard.SetSelected(false);
        selectedCard = card;
        selectedCard.SetSelected(true);
        placementController.Select(card.Def);
    }

    private void Deselect()
    {
        if (selectedCard != null) selectedCard.SetSelected(false);
        selectedCard = null;
        placementController.Select(null);
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
