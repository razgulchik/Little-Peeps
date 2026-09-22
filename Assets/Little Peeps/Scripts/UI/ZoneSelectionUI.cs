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
    // At most one offer has the focus and only that one is drawn on the world: none when the panel
    // opens — the island is simply as the player left it — and then whichever card the cursor last
    // entered. The focus is sticky: the cursor leaving a card changes nothing, so the player can pan
    // over to the zone and keep seeing it. The camera follows the focus for the same reason, which
    // also means it only ever moves because the player pointed at something — and it puts the zone
    // above the middle of the screen (focusOffsetY), clear of the cards along the bottom.
    //
    // The HUD is hidden while the offers are up: this screen IS the game for that moment, and the
    // resource bar and the age button underneath would only be noise. That is not done from here — the
    // state announces UIMode.ZonePick and UIVisibility's table decides what survives it. This panel used
    // to hold a `hud` CanvasGroup and switch off another branch of the hierarchy itself.
    //
    // Cards are instantiated per offer (a biome that fits nowhere simply has no card). Showing and
    // hiding the panel is likewise the table's job, never SetActive on this object — see PerkSelectionUI
    // for why an inactive panel's Awake would otherwise run inside the very Show() that reveals it.
    [RequireComponent(typeof(CanvasGroup))]
    public class ZoneSelectionUI : MonoBehaviour
    {
        [SerializeField] private ZoneCardUI cardPrefab;

        [Tooltip("Parent for the spawned cards — give it a Layout Group.")]
        [SerializeField] private Transform cardContainer;

        [Tooltip("Draws the offers on the island. Optional, but without it the cards are the only clue.")]
        [SerializeField] private ZonePreviewOverlay preview;

        [Tooltip("Widened to reach the offers while they are up; sent to look at the hovered one.")]
        [SerializeField] private CameraController cameraController;

        [Tooltip("How far above the middle of the screen the focused zone sits, as a fraction of the " +
                 "visible height — enough that the cards along the bottom do not cover it. 0 = dead centre.")]
        [Range(0f, 0.4f)] [SerializeField] private float focusOffsetY = 0.15f;

        private readonly List<ZoneCardUI> cards = new();
        private ZoneOffer focused;

        // Fill the screen with one card per offer. Never called with an empty list: ZoneSelectionState
        // skips the choice instead, so there is no "and if there are none" state to draw. What it does
        // NOT do is reveal anything — the panel is already visible by the time this runs, because the
        // state published UIMode.ZonePick first. This owns the CONTENT of the screen, not the screen.
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

            if (preview != null && cameraController != null)
                cameraController.SetExtraBounds(preview.WorldBounds(offers));

            // Nothing drawn until a card is pointed at — and the cards were just made, so this only
            // clears whatever the previous choice left behind.
            SetFocus(null);
        }

        public void Hide()
        {
            ClearCards();
            focused = null;

            if (preview != null) preview.Hide();
            if (cameraController != null) cameraController.ClearExtraBounds();
        }

        // Only ever called with the cursor entering a card, never leaving one: the focus stays until
        // another card takes it.
        private void OnCardHovered(ZoneOffer offer)
        {
            if (offer == focused) return;

            SetFocus(offer);
            if (preview != null && cameraController != null)
                cameraController.FocusOn(preview.WorldCentre(offer), focusOffsetY);
        }

        // The focus in one place, so the card's frame and the world drawing always agree. Null is the
        // state the panel opens in: no frame, nothing drawn.
        private void SetFocus(ZoneOffer offer)
        {
            focused = offer;

            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) cards[i].SetFocused(offer != null && cards[i].Offer == offer);

            if (preview == null) return;

            if (offer != null) preview.Show(offer);
            else preview.Hide();
        }

        private void OnCardConfirmed(ZoneOffer offer)
        {
            // Lock every card the moment one of them is picked. Hiding only happens once the state has
            // acted on the event, and until then the others are still live objects under the pointer.
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) cards[i].SetInteractable(false);

            EventBus<ZoneSelectedEvent>.Publish(new ZoneSelectedEvent { Offer = offer });
        }

        private void ClearCards()
        {
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) Destroy(cards[i].gameObject);

            cards.Clear();
        }
    }
}
