using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One perk card. Confirms on a HOLD rather than a click, because the screen appears unprompted and
    // the perk lasts the rest of the run: a stray click on a card the player had not finished reading is
    // expensive and cannot be taken back.
    //
    // holdDuration 0 is a real CLICK, not an instant one — it confirms on release. Firing on press would
    // make the fastest setting also the most accident-prone, since there would be no moment left to slide
    // off and change your mind, which is the whole thing the hold exists to give.
    //
    // Everything is timed on UNSCALED time: the perk pick runs at timeScale 0, so a scaled timer would
    // never advance and the card could never be confirmed at all.
    public class PerkCardUI : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler,
                                             IPointerExitHandler
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Image iconImage;

        [Tooltip("Optional. An Image with Image Type = Filled — its fillAmount tracks the hold. Without " +
                 "one the player holds for a second against no feedback, which reads as a dead button.")]
        [SerializeField] private Image holdFill;

        [Tooltip("Optional. The frame shown while the cursor is over the card. HOVER only — being held " +
                 "is what holdFill shows, and the two are deliberately separate visuals.")]
        [FormerlySerializedAs("heldHighlight")]
        [SerializeField] private GameObject hoverHighlight;

        [Tooltip("Seconds of holding needed to confirm. 0 makes the card an ordinary click, which still " +
                 "confirms on release rather than on press.")]
        [Min(0f)] [SerializeField] private float holdDuration = 1f;

        private PerkDef perk;
        private Action<PerkDef> onConfirmed;

        private bool interactable = true;
        private bool holding;
        private float held;

        // Fill in the card and say where a confirmed pick goes. The callback rather than an event: this
        // is card → panel, the same internal wiring BuildCardUI → BuildPanelUI uses. Leaving the panel
        // to publish keeps one publisher of PerkSelectedEvent instead of three.
        public void Init(PerkDef def, Action<PerkDef> onConfirmed)
        {
            perk = def;
            this.onConfirmed = onConfirmed;

            interactable = true;
            SetHovered(false);
            CancelHold();

            if (titleLabel != null) titleLabel.text = def != null ? def.title : string.Empty;
            if (descriptionLabel != null) descriptionLabel.text = def != null ? def.description : string.Empty;

            if (iconImage != null)
            {
                iconImage.sprite = def != null ? def.icon : null;

                // Hidden rather than left showing whatever the prefab was authored with: an unset icon
                // would otherwise print the placeholder art as if it were this perk's.
                iconImage.enabled = iconImage.sprite != null;
            }
        }

        // Called on every card as soon as one of them is confirmed. Until the state applies the perk and
        // hides the panel, the other cards are still live objects under the player's finger.
        public void SetInteractable(bool value)
        {
            interactable = value;

            // A card locked while the cursor happens to be resting on it must drop its frame too, or the
            // losing cards keep looking pickable for as long as the panel stays up.
            if (!interactable)
            {
                SetHovered(false);
                CancelHold();
            }
        }

        private void Update()
        {
            if (!holding) return;

            // A zero duration is release-to-confirm, so there is no progress to run here.
            if (holdDuration <= 0f) return;

            held += Time.unscaledDeltaTime;
            ApplyProgress(held / holdDuration);

            if (held >= holdDuration) Confirm();
        }

        // Hover is its OWN visual, independent of the press: the frame says "this is the card under your
        // cursor", the fill says "and you are choosing it". Both used to hang off OnPointerDown, which
        // left the card looking inert until pressed and made the frame read as a selection marker.
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!interactable) return;

            SetHovered(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable) return;

            holding = true;
            held = 0f;
            ApplyProgress(0f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!holding) return;

            bool confirmOnRelease = holdDuration <= 0f;
            CancelHold();

            if (confirmOnRelease && interactable) Confirm();
        }

        // Leaving does two separate things: it drops the hover frame, and it abandons any hold in
        // progress. The second is the escape hatch the whole interaction is built around, so it has to
        // keep working while the pointer is still down.
        //
        // Coming back does NOT resume the hold: the timer is already at zero, and silently restarting it
        // under a finger that never moved is the exact accident the hold exists to prevent. A new press
        // is required.
        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
            CancelHold();
        }

        private void CancelHold()
        {
            holding = false;
            held = 0f;
            ApplyProgress(0f);
        }

        private void SetHovered(bool hovered)
        {
            if (hoverHighlight != null) hoverHighlight.SetActive(hovered);
        }

        private void ApplyProgress(float t)
        {
            if (holdFill != null) holdFill.fillAmount = Mathf.Clamp01(t);
        }

        // Closed BEFORE the callback runs, not after: the callback ends with the panel hiding and the
        // cards being destroyed, so anything touched after it is running on an object on its way out.
        private void Confirm()
        {
            var confirmedPerk = perk;
            var callback = onConfirmed;

            interactable = false;
            CancelHold();

            callback?.Invoke(confirmedPerk);
        }
    }
}
