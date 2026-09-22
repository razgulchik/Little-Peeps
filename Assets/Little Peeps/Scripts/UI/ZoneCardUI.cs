using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One zone card: the biome's name and icon, and what the zone actually holds — exact counts from
    // the content already generated for it, so the player is choosing between "14 trees and a river"
    // and "6 stones and a mountain", not between blurbs.
    //
    // Hover is what this card is really for: the panel is told the moment the cursor lands on it, and
    // the world answers (the zone lights up, the camera goes to it). The focus that results is the
    // panel's, not the card's — it stays where the cursor last was, so the cursor leaving is not
    // reported and the frame is switched by the panel (SetFocused), not by the pointer. The pick is a
    // plain click, unlike the perk card's hold — the player has been looking at exactly what they are
    // about to get.
    //
    // Written apart from PerkCardUI on purpose: the two share a frame and nothing else that matters,
    // and the hold/press machinery over there is most of that file.
    public class ZoneCardUI : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Image iconImage;

        [Tooltip("One line per kind of feature the zone holds: \"Forest ×14\".")]
        [SerializeField] private TMP_Text contentsLabel;

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
            if (contentsLabel != null) contentsLabel.text = offer != null ? ContentsText(offer) : string.Empty;

            if (iconImage != null)
            {
                iconImage.sprite = biome != null ? biome.icon : null;

                // Hidden rather than left showing the prefab's placeholder as though it were this biome's.
                iconImage.enabled = iconImage.sprite != null;
            }
        }

        // The panel's focus: on for the card whose zone is drawn, off for the rest.
        public void SetFocused(bool focused)
        {
            if (focusHighlight != null) focusHighlight.SetActive(focused);
        }

        // Called on every card as soon as one of them is picked: until the state hides the panel, the
        // other cards are still live objects under the pointer.
        public void SetInteractable(bool value)
        {
            interactable = value;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!interactable) return;
            onHovered?.Invoke(offer);
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

        // "Forest ×14" per line, in the generator's placement order. A def with no display name falls
        // back to its id, so an unnamed asset still says something.
        private static string ContentsText(ZoneOffer offer)
        {
            var sb = new StringBuilder();
            foreach (var (def, count) in offer.Counts)
            {
                if (sb.Length > 0) sb.Append('\n');
                string name = !string.IsNullOrEmpty(def.displayName) ? def.displayName : def.id;
                sb.Append(name).Append(" ×").Append(count);
            }
            return sb.ToString();
        }
    }
}
