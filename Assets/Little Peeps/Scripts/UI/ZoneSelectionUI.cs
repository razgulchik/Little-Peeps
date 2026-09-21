using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The zone cards. Owns the screen — the cards, the preview drawn on the world and where the camera
    // looks — and nothing else: it does not commit a zone and does not know the run. A picked card is
    // published as ZoneSelectedEvent and ZoneSelectionState does the rest, the same split
    // PerkSelectionUI → PerkSelectionState uses and for the same reason (a long-lived UI object must
    // never cache the run).
    //
    // The preview and the camera focus are driven from here rather than through the state because they
    // are presentation: hovering a card changes what the player is looking at and nothing else, and the
    // world side of that has one owner, the way PlacementController owns the build grid overlay.
    //
    // The HUD is hidden while the offers are up: this screen IS the game for that moment, and the
    // resource bar and the age button underneath would only be noise. One CanvasGroup over the whole
    // HUD for now; if panels ever need to stay, it becomes per-panel.
    //
    // Cards are instantiated per offer (a biome that fits nowhere simply has no card) and the panel is
    // hidden by a CanvasGroup, never by deactivating this object — see PerkSelectionUI for why an
    // inactive panel's Awake would otherwise run inside the very Show() that reveals it.
    [RequireComponent(typeof(CanvasGroup))]
    public class ZoneSelectionUI : MonoBehaviour
    {
        [SerializeField] private ZoneCardUI cardPrefab;

        [Tooltip("Parent for the spawned cards — give it a Layout Group.")]
        [SerializeField] private Transform cardContainer;

        [SerializeField] private CanvasGroup canvasGroup;

        [Header("What the screen replaces and reveals")]
        [Tooltip("The HUD's root CanvasGroup, hidden while zones are on offer. Must NOT contain this " +
                 "panel — a parent group would hide the cards too.")]
        [SerializeField] private CanvasGroup hud;

        [Tooltip("Draws the offers on the island. Optional, but without it the cards are the only clue.")]
        [SerializeField] private ZonePreviewOverlay preview;

        [Tooltip("Widened to reach the offers while they are up; sent to look at the hovered one.")]
        [SerializeField] private CameraController cameraController;

        private readonly List<ZoneCardUI> cards = new();

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        // The idle state is established here rather than trusted from the prefab — whatever the panel
        // was left showing when the Canvas was last saved would otherwise be the first frame of a run.
        // The HUD is deliberately NOT touched here: it starts visible and only this screen hides it.
        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                Debug.LogError($"ZoneSelectionUI on '{name}' has no CanvasGroup — the zone panel cannot " +
                               "be hidden and will cover the game.", this);
                return;
            }

            SetVisible(false);
        }

        // Show the panel with one card per offer. Never called with an empty list: ZoneSelectionState
        // skips the choice instead, so there is no "and if there are none" state to draw.
        public void Show(IReadOnlyList<ZoneOffer> offers)
        {
            ClearCards();

            if (cardPrefab == null || cardContainer == null)
            {
                Debug.LogError($"ZoneSelectionUI on '{name}' is missing its card prefab or container — " +
                               "the panel would open empty.", this);
                return;
            }

            for (int i = 0; i < offers.Count; i++)
            {
                var card = Instantiate(cardPrefab, cardContainer);
                card.Init(offers[i], OnCardHovered, OnCardConfirmed);
                cards.Add(card);
            }

            SetVisible(true);
            SetHudVisible(false);

            if (preview != null)
            {
                preview.Show(offers);
                if (cameraController != null) cameraController.SetExtraBounds(preview.WorldBounds(offers));
            }
        }

        public void Hide()
        {
            SetVisible(false);
            ClearCards();
            SetHudVisible(true);

            if (preview != null) preview.Hide();
            if (cameraController != null) cameraController.ClearExtraBounds();
        }

        // Null when the cursor leaves a card. The camera stays where it was then — the player may have
        // moved it to look at the zone, and snapping back would take that away.
        private void OnCardHovered(ZoneOffer offer)
        {
            if (preview == null) return;

            preview.Highlight(offer);
            if (offer != null && cameraController != null) cameraController.FocusOn(preview.WorldCentre(offer));
        }

        private void OnCardConfirmed(ZoneOffer offer)
        {
            // Lock every card the moment one of them is picked. Hiding only happens once the state has
            // acted on the event, and until then the others are still live objects under the pointer.
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) cards[i].SetInteractable(false);

            EventBus<ZoneSelectedEvent>.Publish(new ZoneSelectedEvent { Offer = offer });
        }

        // blocksRaycasts matters as much as alpha: an invisible panel that still swallows clicks would
        // put a dead rectangle over the island.
        private void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        private void SetHudVisible(bool visible)
        {
            if (hud == null) return;

            hud.alpha = visible ? 1f : 0f;
            hud.blocksRaycasts = visible;
            hud.interactable = visible;
        }

        private void ClearCards()
        {
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) Destroy(cards[i].gameObject);

            cards.Clear();
        }
    }
}
