using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace LittlePeeps
{
    // One zone card: the biome's name, a window onto its collage and a line on what the biome is like.
    // The collage is how the player sees what a zone of this biome holds — the trees, the animals, the
    // river — and the zone itself is drawn on the island the moment the card is pointed at.
    //
    // Hover is what this card is really for: the panel is told the moment the cursor lands on it, and
    // the world answers (the zone lights up, the camera goes to it). The focus that results is the
    // panel's, not the card's — it stays where the cursor last was, so the cursor leaving is not
    // reported and the frame is switched by the panel (SetFocused), not by the pointer. The cursor
    // leaving does stop the collage: that one is the card's own, it plays only while being looked at.
    // The pick is a plain click, unlike the perk card's hold — the player has been looking at exactly
    // what they are about to get.
    //
    // The focused card stands up out of the row (focusLift). What moves is `body`, a child holding
    // everything drawn, never the root: the root's place is the row's layout group's to decide.
    //
    // Written apart from PerkCardUI on purpose: the two share a frame and nothing else that matters,
    // and the hold/press machinery over there is most of that file.
    public class ZoneCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("What the biome is like, from BiomeDef.description.")]
        [SerializeField] private TMP_Text descriptionLabel;

        [Tooltip("The window onto the biome's collage. Plays while the cursor is on the card.")]
        [SerializeField] private ZoneCollageView collage;

        [Tooltip("Everything the card draws. Raised by focusLift while this card's zone is the one drawn " +
                 "on the world.")]
        [SerializeField] private RectTransform body;

        [Tooltip("How far the focused card stands up out of the row, in canvas pixels.")]
        [Min(0)] [SerializeField] private int focusLift = 5;

        [Tooltip("Optional. The frame shown while this card's zone is the one drawn on the world.")]
        [FormerlySerializedAs("hoverHighlight")]
        [SerializeField] private GameObject focusHighlight;

        private ZoneOffer offer;
        private Action<ZoneOffer> onHovered;
        private Action<ZoneOffer> onConfirmed;

        private bool interactable = true;

        // Which offer this card stands for — the panel matches its focus against it.
        public ZoneOffer Offer => offer;

        // Fill in the card and say where hover and pick go. Callbacks rather than events: this is card
        // to panel, and leaving the panel to publish keeps one publisher of ZoneSelectedEvent instead of
        // three.
        public void Init(ZoneOffer offer, Action<ZoneOffer> onHovered, Action<ZoneOffer> onConfirmed)
        {
            this.offer = offer;
            this.onHovered = onHovered;
            this.onConfirmed = onConfirmed;

            interactable = true;
            SetFocused(false);

            var biome = offer?.Biome;
            if (titleLabel != null) titleLabel.text = biome != null ? biome.DisplayName : string.Empty;
            if (descriptionLabel != null) descriptionLabel.text = biome != null ? biome.description : string.Empty;
            if (collage != null) collage.Show(biome != null ? biome.collage : null);
        }

        // The panel's focus: on for the card whose zone is drawn, off for the rest.
        public void SetFocused(bool focused)
        {
            if (focusHighlight != null) focusHighlight.SetActive(focused);
            if (body != null) body.anchoredPosition = new Vector2(0f, focused ? focusLift : 0f);
        }

        // Called on every card as soon as one of them is picked: until the state hides the panel, the
        // other cards are still live objects under the pointer.
        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!value && collage != null) collage.SetPlaying(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!interactable) return;

            if (collage != null) collage.SetPlaying(true);
            onHovered?.Invoke(offer);
        }

        // Only the collage hears about this — the focus stays with the card (see the top of the file).
        public void OnPointerExit(PointerEventData eventData)
        {
            if (collage != null) collage.SetPlaying(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable || eventData.button != PointerEventData.InputButton.Left) return;

            // Closed BEFORE the callback runs: the callback ends with the panel hiding and the cards
            // being destroyed, so anything touched after it is running on an object on its way out.
            var picked = offer;
            var callback = onConfirmed;
            interactable = false;

            callback?.Invoke(picked);
        }
    }
}
