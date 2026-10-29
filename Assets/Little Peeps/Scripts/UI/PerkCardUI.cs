using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    // The WHOLE card is the button; "Hold to Claim" is only where its progress shows. The card's three
    // visuals are deliberately independent: the lit look is HOVER, the scale is PRESS, the fill is HOLD.
    // They were once a single flag raised on OnPointerDown, which made the card look dead until pressed
    // and made the frame read as a selection marker.
    //
    // Everything is timed on UNSCALED time: the perk pick runs at timeScale 0, so a scaled timer would
    // never advance and neither the hold nor the press response would move at all.
    public class PerkCardUI : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler,
                                             IPointerExitHandler
    {
        [Serializable]
        public struct RarityFrame
        {
            public PerkRarity rarity;
            public Sprite frame;
        }

        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private Image iconImage;

        [Tooltip("Colour of the numbers in the description — x2, +50%, -0.5. The card paints them itself, " +
                 "so a hand-written description needs no colour tags.")]
        [SerializeField] private Color numberColor = new Color32(0x68, 0xD5, 0x00, 0xFF);

        [Header("Frame and level")]
        [Tooltip("The coloured frame around the icon, picked from Rarity Frames by the perk's rarity. A " +
                 "rarity with no entry hides it.")]
        [SerializeField] private Image rarityFrame;

        [SerializeField] private List<RarityFrame> rarityFrames = new();

        [Tooltip("The level this card offers, in roman numerals — always shown, I for a perk taken the " +
                 "first time or one with a single level.")]
        [SerializeField] private TMP_Text levelLabel;

        [Header("Resources")]
        [Tooltip("Parent of the row of resources the perk acts on. Give it a Grid Layout Group: its cell " +
                 "size is the icon size, its column count the row length. The icons are created from code.")]
        [SerializeField] private RectTransform resourceRow;

        [SerializeField] private ResourceIconSet iconSet;

        [Header("Hover")]
        [Tooltip("Optional. Shown while the cursor is over the card — the lit panel, drawn over the plain " +
                 "one. HOVER only: being held is what the fill shows, and the two are deliberately " +
                 "separate visuals.")]
        [FormerlySerializedAs("heldHighlight")]
        [SerializeField] private GameObject hoverHighlight;

        [SerializeField] private Color titleColor = Color.white;
        [SerializeField] private Color hoverTitleColor = new Color32(0xFF, 0xC9, 0x33, 0xFF);

        [Tooltip("Optional. The Hold to Claim button's background, lit together with the card.")]
        [SerializeField] private Image claimImage;

        [SerializeField] private Sprite claimSprite;
        [SerializeField] private Sprite hoverClaimSprite;

        [Header("Hold")]
        [Tooltip("Seconds of holding needed to confirm. 0 makes the card an ordinary click, which still " +
                 "confirms on release rather than on press.")]
        [Min(0f)] [SerializeField] private float holdDuration = 1f;

        [Tooltip("Optional. The fill inside Hold to Claim: grows from its left edge in whole pixels as the " +
                 "hold goes on, and its size in the prefab is the FULL fill — anchor it by its left edge " +
                 "(HoldButton's rule). Without one the player holds against no feedback, which reads as a " +
                 "dead button.")]
        [SerializeField] private RectTransform fill;

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

        // A number as a whole word: an optional sign or multiplier, digits, an optional fraction and
        // percent — x2, ×2, +50%, -0.5, +1. Not after a letter, digit, dot or the '=' / '#' of a rich-text
        // tag, and not running into a letter, so "M6" or a <size=12> is left alone.
        private static readonly Regex Number = new(@"(?<![\w.=#])[+\-xX×]?\d+(?:\.\d+)?%?(?!\w)");

        private PerkDef perk;
        private Action<PerkDef> onConfirmed;

        private bool interactable = true;
        private bool holding;
        private float held;
        private float fullFill = -1f;

        private readonly List<ResourceType> resources = new();
        private readonly List<Image> resourceIcons = new();

        private MotionHandle scaleMotion;

        private void Awake()
        {
            if (scaleTarget == null) scaleTarget = transform as RectTransform;
            ReadFill();
        }

        // A motion outlives the object it writes to unless someone stops it, and these cards are built
        // and destroyed every single age transition — ClearCards can land while a press tween is still
        // in flight. Same guard BuildCardUI uses, and it matters more here than it does there.
        private void OnDisable() => Cancel(ref scaleMotion);

        private void OnDestroy() => Cancel(ref scaleMotion);

        // Fill in the card and say where a confirmed pick goes. The callback rather than an event: this
        // is card to panel, the same internal wiring BuildCardUI and BuildPaletteUI use. Leaving the panel
        // to publish keeps one publisher of PerkSelectedEvent instead of three. `level` is the one the card
        // offers: what it describes and what its numeral says.
        public void Init(PerkDef def, int level, Action<PerkDef> onConfirmed)
        {
            perk = def;
            this.onConfirmed = onConfirmed;

            interactable = true;
            SetHovered(false);
            CancelHold();
            ScaleTo(1f, 0f);          // snap, not tween: a fresh card must not animate in from nowhere

            if (titleLabel != null) titleLabel.text = def != null ? def.title : string.Empty;
            if (descriptionLabel != null)
                descriptionLabel.text = def != null ? HighlightNumbers(def.DescriptionAt(level), numberColor) : string.Empty;
            if (levelLabel != null) levelLabel.text = def != null ? RomanNumeral.From(level) : string.Empty;

            // Hidden rather than left showing whatever the prefab was authored with: an unset sprite
            // would otherwise print the placeholder art as though it belonged to this perk.
            if (iconImage != null) ShowSprite(iconImage, def != null ? def.icon : null);
            if (rarityFrame != null) ShowSprite(rarityFrame, def != null ? FrameFor(def.rarity) : null);

            ShowResources(def);
        }

        // Called on every card as soon as one of them is confirmed. Until the state applies the perk and
        // hides the panel, the other cards are still live objects under the pointer.
        public void SetInteractable(bool value)
        {
            interactable = value;

            // A card locked while the cursor happens to be resting on it must drop its lit look too, or
            // the losing cards keep looking pickable for as long as the panel stays up.
            if (!interactable)
            {
                SetHovered(false);
                CancelHold();
                ScaleTo(1f, releaseDuration);
            }
        }

        // The description with every number painted — see Number for what counts as one. Pure, so the
        // matching is tested without a card.
        public static string HighlightNumbers(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            Color32 c = color;
            string open = $"<color=#{c.r:X2}{c.g:X2}{c.b:X2}>";
            return Number.Replace(text, m => open + m.Value + "</color>");
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

        // Hover is its OWN visual, independent of the press: the lit card says this is the one under your
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

        // Leaving does three separate things: it drops the lit look, it lets the card back up, and it
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

        // The lit panel, the title colour and the claim button all follow the cursor together.
        private void SetHovered(bool hovered)
        {
            if (hoverHighlight != null) hoverHighlight.SetActive(hovered);
            if (titleLabel != null) titleLabel.color = hovered ? hoverTitleColor : titleColor;

            Sprite claim = hovered ? hoverClaimSprite : claimSprite;
            if (claimImage != null && claim != null) claimImage.sprite = claim;
        }

        // Read lazily as well as in Awake: Edit Mode tests never run Awake.
        private void ReadFill()
        {
            if (fill == null || fullFill >= 0f) return;

            fullFill = fill.rect.width;
        }

        // Whole pixels, so the fill's edge stays as crisp as the art around it.
        private void ApplyProgress(float t)
        {
            if (fill == null) return;

            ReadFill();
            float width = Mathf.Round(Mathf.Clamp01(t) * fullFill);
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private Sprite FrameFor(PerkRarity rarity)
        {
            for (int i = 0; i < rarityFrames.Count; i++)
                if (rarityFrames[i].rarity == rarity) return rarityFrames[i].frame;
            return null;
        }

        private static void ShowSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        // One icon per resource the perk acts on, created under the row and laid out by its grid. A
        // resource with no icon in the set is left out rather than drawn as a white square.
        private void ShowResources(PerkDef def)
        {
            if (resourceRow == null) return;

            resources.Clear();
            if (def != null && iconSet != null) def.GetResources(resources);

            int shown = 0;
            for (int i = 0; i < resources.Count; i++)
            {
                Sprite icon = iconSet.IconFor(resources[i]);
                if (icon == null) continue;

                if (shown == resourceIcons.Count) resourceIcons.Add(CreateResourceIcon());
                resourceIcons[shown].sprite = icon;
                resourceIcons[shown].gameObject.SetActive(true);
                shown++;
            }

            for (int i = shown; i < resourceIcons.Count; i++) resourceIcons[i].gameObject.SetActive(false);
        }

        // Not a raycast target: the card root takes the pointer, and an icon catching it would only be in
        // the way.
        private Image CreateResourceIcon()
        {
            var go = new GameObject("Resource", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(resourceRow, false);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
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
