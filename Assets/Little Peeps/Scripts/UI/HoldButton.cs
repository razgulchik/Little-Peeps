using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LittlePeeps
{
    // A button that answers on a HOLD, for the answers that cannot be taken back — Face the Wrath ends the
    // run. The perk card's interaction without the card: press and keep holding for holdDuration while the
    // fill shows how far it has got; letting go early or sliding off abandons it, and coming back does not
    // resume it. holdDuration 0 is an ordinary click, confirmed on RELEASE, so sliding off still cancels.
    //
    // The fill is a plain rectangle that grows from its left edge in whole pixels — an Image without a
    // sprite cannot be Filled, and a crisp edge is the look anyway. The rect the prefab authors is the FULL
    // fill: anchor it by its left edge, it is read once and shrunk to nothing at rest.
    //
    // Unscaled time: the window it sits in runs at timeScale 0.
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("Seconds of holding needed to answer. 0 makes it an ordinary click, which still answers on " +
                 "release rather than on press.")]
        [Min(0f)] [SerializeField] private float holdDuration = 1f;

        [Tooltip("Optional. Grows from its left edge as the hold goes on; its size in the prefab is the full " +
                 "fill. Without one the player holds against no feedback, which reads as a dead button.")]
        [SerializeField] private RectTransform fill;

        // Raised once per completed hold. The listener is the caller's continuation — it may close the window
        // this button sits in — so nothing here is touched after it runs.
        public event Action Confirmed;

        private bool interactable = true;
        private bool holding;
        private float held;
        private float fullWidth = -1f;

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                if (!interactable) CancelHold();
            }
        }

        private void Awake() => ReadFill();

        private void OnDisable() => CancelHold();

        private void Update()
        {
            if (!holding || holdDuration <= 0f) return;

            held += Time.unscaledDeltaTime;
            ApplyProgress(held / holdDuration);

            if (held >= holdDuration) Confirm();
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

        // The escape hatch, for PerkCardUI's reason: it has to keep working while the pointer is still down.
        public void OnPointerExit(PointerEventData eventData) => CancelHold();

        private void CancelHold()
        {
            holding = false;
            held = 0f;
            ApplyProgress(0f);
        }

        // Read lazily as well as in Awake: Edit Mode tests never run Awake.
        private void ReadFill()
        {
            if (fill == null || fullWidth >= 0f) return;

            fullWidth = fill.rect.width;
        }

        private void ApplyProgress(float t)
        {
            if (fill == null) return;

            ReadFill();
            float width = Mathf.Round(Mathf.Clamp01(t) * fullWidth);
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private void Confirm()
        {
            CancelHold();
            Confirmed?.Invoke();
        }
    }
}
