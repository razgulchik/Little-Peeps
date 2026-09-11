using System;
using LitMotion;
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
    // The card behaves as a button, and its three visuals are deliberately independent: the frame is
    // HOVER, the scale is PRESS, the fill is HOLD. They were once a single flag raised on OnPointerDown,
    // which made the card look dead until pressed and made the frame read as a selection marker.
    //
    // Everything is timed on UNSCALED time: the perk pick runs at timeScale 0, so a scaled timer would
    // never advance and neither the hold nor the press response would move at all.
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

        [Header("Press response")]
        [Tooltip("The visual scaled on press. Point this at a CHILD of the card rather than the root: " +
                 "scaling the root pulls its edges out from under the cursor, and a pointer resting " +
                 "near an edge would then fire OnPointerExit and cancel the hold. Falls back to this " +
                 "object when left empty.")]
        [SerializeField] private RectTransform scaleTarget;

        [Tooltip("Scale while pressed. Below 1 sinks the card, above 1 makes it pop — both read fine, " +
                 "so pick by eye. Exactly 1 turns the press response off.")]
        [SerializeField] private float pressedScale = 0.97f;

        [Tooltip("Seconds to reach the pressed scale. Press feedback has to be FAST — past roughly a " +
                 "tenth of a second it stops reading as softness and starts reading as lag. 0 snaps.")]
        [Min(0f)] [SerializeField] private float pressDuration = 0.07f;

        [Tooltip("Seconds to return to rest. May be a little slower than the press without feeling laggy.")]
        [Min(0f)] [SerializeField] private float releaseDuration = 0.12f;

        [SerializeField] private Ease scaleEase = Ease.OutQuad;

        [Space]
        [Tooltip("Seconds of holding needed to confirm. 0 makes the card an ordinary click, which still " +
                 "confirms on release rather than on press.")]
        [Min(0f)] [SerializeField] private float holdDuration = 1f;

        private PerkDef perk;
        private Action<PerkDef> onConfirmed;

        private bool interactable = true;
        private bool holding;
        private float held;

        private MotionHandle scaleMotion;

        private void Awake()
        {
            if (scaleTarget == null) scaleTarget = transform as RectTransform;
        }

        // A motion outlives the object it writes to unless someone stops it, and these cards are built
        // and destroyed every single age transition — ClearCards can land while a press tween is still
        // in flight. Same guard BuildCardUI uses, and it matters more here than it does there.
        private void OnDisable() => Cancel(ref scaleMotion);

        private void OnDestroy() => Cancel(ref scaleMotion);

        // Fill in the card and say where a confirmed pick goes. The callback rather than an event: this
        // is card to panel, the same internal wiring BuildCardUI and BuildPanelUI use. Leaving the panel
        // to publish keeps one publisher of PerkSelectedEvent instead of three.
        public void Init(PerkDef def, Action<PerkDef> onConfirmed)
        {
            perk = def;
            this.onConfirmed = onConfirmed;

            interactable = true;
            SetHovered(false);
            CancelHold();
            ScaleTo(1f, 0f);          // snap, not tween: a fresh card must not animate in from nowhere

            if (titleLabel != null) titleLabel.text = def != null ? def.title : string.Empty;
            if (descriptionLabel != null) descriptionLabel.text = def != null ? def.description : string.Empty;

            if (iconImage != null)
            {
                iconImage.sprite = def != null ? def.icon : null;

                // Hidden rather than left showing whatever the prefab was authored with: an unset icon
                // would otherwise print the placeholder art as though it belonged to this perk.
                iconImage.enabled = iconImage.sprite != null;
            }
        }

        // Called on every card as soon as one of them is confirmed. Until the state applies the perk and
        // hides the panel, the other cards are still live objects under the pointer.
        public void SetInteractable(bool value)
        {
            interactable = value;

            // A card locked while the cursor happens to be resting on it must drop its frame too, or the
            // losing cards keep looking pickable for as long as the panel stays up.
            if (!interactable)
            {
                SetHovered(false);
                CancelHold();
                ScaleTo(1f, releaseDuration);
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

        // Hover is its OWN visual, independent of the press: the frame says this is the card under your
        // cursor, the fill says you are choosing it.
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
            ScaleTo(pressedScale, pressDuration);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ScaleTo(1f, releaseDuration);

            if (!holding) return;

            bool confirmOnRelease = holdDuration <= 0f;
            CancelHold();

            if (confirmOnRelease && interactable) Confirm();
        }

        // Leaving does three separate things: it drops the hover frame, it lets the card back up, and it
        // abandons any hold in progress. The last is the escape hatch the whole interaction is built
        // around, so it has to keep working while the pointer is still down.
        //
        // Coming back does NOT resume the hold: the timer is already at zero, and silently restarting it
        // under a finger that never moved is the exact accident the hold exists to prevent. A new press
        // is required.
        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
            ScaleTo(1f, releaseDuration);
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

        // Tween to a uniform scale, or snap when the duration is zero. Starting from the CURRENT scale
        // rather than from a known rest value is what lets a press interrupt a half-finished release
        // without a jump.
        private void ScaleTo(float target, float duration)
        {
            Cancel(ref scaleMotion);
            if (scaleTarget == null) return;

            if (duration <= 0f)
            {
                ApplyScale(target);
                return;
            }

            scaleMotion = LMotion.Create(scaleTarget.localScale.x, target, duration)
                .WithEase(scaleEase)
                .WithScheduler(MotionScheduler.InitializationIgnoreTimeScale)
                .Bind(ApplyScale);
        }

        private void ApplyScale(float s)
        {
            if (scaleTarget != null) scaleTarget.localScale = new Vector3(s, s, 1f);
        }

        private static void Cancel(ref MotionHandle handle)
        {
            if (handle.IsActive()) handle.Cancel();
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
