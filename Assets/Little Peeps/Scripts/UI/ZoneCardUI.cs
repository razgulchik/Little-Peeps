using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One zone card: the biome's name and icon, and what the zone actually holds — exact counts from
    // the content already generated for it, so the player is choosing between "14 trees and a river"
    // and "6 stones and a mountain", not between blurbs.
    //
    // Hover is what this card is really for: the panel is told the moment the cursor lands on it, and
    // the world answers (the zone lights up, the camera goes to it). The pick is a plain click, unlike
    // the perk card's hold — the player has been looking at exactly what they are about to get.
    //
    // Written apart from PerkCardUI on purpose: the two share a hover frame and nothing else that
    // matters, and the hold/press machinery over there is most of that file.
    public class ZoneCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Image iconImage;

        [Tooltip("One line per kind of feature the zone holds: \"Forest ×14\".")]
        [SerializeField] private TMP_Text contentsLabel;

        [Tooltip("Optional. The frame shown while the cursor is over the card.")]
        [SerializeField] private GameObject hoverHighlight;

        private ZoneOffer offer;
        private Action<ZoneOffer> onHovered;
        private Action<ZoneOffer> onConfirmed;

        private bool interactable = true;

        // Fill in the card and say where hover and pick go. Callbacks rather than events: this is card
        // to panel, and leaving the panel to publish keeps one publisher of ZoneSelectedEvent instead of
        // three. `onHovered` gets null when the cursor leaves.
        public void Init(ZoneOffer offer, Action<ZoneOffer> onHovered, Action<ZoneOffer> onConfirmed)
        {
            this.offer = offer;
            this.onHovered = onHovered;
            this.onConfirmed = onConfirmed;

            interactable = true;
            SetHovered(false);

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

        // Called on every card as soon as one of them is picked: until the state hides the panel, the
        // other cards are still live objects under the pointer.
        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!interactable) SetHovered(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!interactable) return;

            SetHovered(true);
            onHovered?.Invoke(offer);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
            if (interactable) onHovered?.Invoke(null);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable || eventData.button != PointerEventData.InputButton.Left) return;

            // Closed BEFORE the callback runs: the callback ends with the panel hiding and the cards
            // being destroyed, so anything touched after it is running on an object on its way out.
            var picked = offer;
            var callback = onConfirmed;
            interactable = false;
            SetHovered(false);

            callback?.Invoke(picked);
        }

        private void SetHovered(bool hovered)
        {
            if (hoverHighlight != null) hoverHighlight.SetActive(hovered);
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
