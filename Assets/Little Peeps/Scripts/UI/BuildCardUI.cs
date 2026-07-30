using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One build-palette card. The fixed root is the layout/click slot; AnimatedVisual is free to grow,
    // rise and tilt without changing the list's spacing. Icon and cost objects are never scaled by flight.
    [RequireComponent(typeof(Button))]
    public class BuildCardUI : MonoBehaviour, IPointerDownHandler
    {
        [Serializable]
        private struct ResourceIcon
        {
            public ResourceType type;
            public Sprite sprite;
        }

        [Header("Interaction")]
        [SerializeField] private Button button;
        [SerializeField] private RectTransform hitRect;
        [SerializeField] private BuildCardMotionProfile motionProfile;

        [Header("Animated layers")]
        [SerializeField] private RectTransform animatedVisual;
        [SerializeField] private RectTransform cardBody;
        [SerializeField] private RectTransform thickness;
        [SerializeField] private RectTransform shadow;
        [SerializeField] private Image shadowImage;
        [SerializeField] private RectTransform selectionFrame;
        [SerializeField] private GameObject selectionFrameObject;

        [Header("Card content")]
        [SerializeField] private RectTransform itemContainer;
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject iconSelectionPlaceholder;
        [SerializeField] private GameObject costPanel;
        [SerializeField] private Image resourceIconImage;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private CanvasGroup artworkCanvasGroup;
        [SerializeField] private ResourceIcon[] resourceIcons;

        [Header("Shine")]
        [SerializeField] private RectTransform shineViewport;
        [SerializeField] private RectTransform shineTransform;
        [SerializeField] private Image shineImage;

        [Header("Age lock")]
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private TMP_Text lockedText;

        public StructureDef Def { get; private set; }
        public bool IsLocked { get; private set; }

        private Action<BuildCardUI> onClick;
        private BuildPanelScroller scroller;
        private MotionHandle hoverMotion;
        private MotionHandle tiltMotion;
        private MotionHandle shineMotion;
        private float hoverProgress;
        private float currentLift;
        private Vector2 tiltValue;
        private bool selected;
        private bool affordable = true;
        private bool pointerInside;
        private bool dragSuppressed;
        private Canvas uiCanvas;
        private CanvasGroup[] interactionCanvasGroups;

        private void Reset()
        {
            button = GetComponent<Button>();
            hitRect = transform as RectTransform;
        }

        public void Init(StructureDef def, Action<BuildCardUI> onClick)
        {
            Def = def;
            this.onClick = onClick;
            scroller = GetComponentInParent<BuildPanelScroller>();
            uiCanvas = GetComponentInParent<Canvas>();
            interactionCanvasGroups = GetComponentsInParent<CanvasGroup>(true);

            if (iconImage != null) iconImage.sprite = def.icon;
            RefreshCost(def);

            if (button == null) button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);

            SetSelected(false);
            SetAffordable(true);
            SetLocked(def.requiredAge > 0, 0);
            ResetInteractionVisuals();
        }

        private void Update()
        {
            dragSuppressed = scroller != null && scroller.IsDragging;
            bool shouldHover = TryGetPointerOverCard(out Vector2 screenPosition, out Camera eventCamera);
            if (shouldHover != pointerInside)
            {
                pointerInside = shouldHover;
                SetHovered(shouldHover);
            }

            // Live tilt is a stateless pointer-to-transform mapping. It is sampled every frame
            // while hovered, so there is no response tween to restart, lag behind or go stale.
            if (shouldHover) ApplyPointerTilt(screenPosition, eventCamera);
        }

        private void OnDisable()
        {
            CancelAllMotions();
        }

        private void OnDestroy()
        {
            CancelAllMotions();
        }

        public void SetSelected(bool value)
        {
            selected = value;
            if (selectionFrameObject != null) selectionFrameObject.SetActive(value);
            if (iconSelectionPlaceholder != null) iconSelectionPlaceholder.SetActive(value);
            ApplyVisualState();
        }

        public void SetAffordable(bool value)
        {
            affordable = value;
            RefreshArtworkAlpha();
        }

        public void SetLocked(bool value, int currentAge)
        {
            IsLocked = value;
            if (lockedOverlay != null) lockedOverlay.SetActive(value);
            if (lockedText != null && Def != null)
                lockedText.text = $"Open on\nthe Age {ToRoman(Mathf.Max(1, Def.requiredAge))}";

            if (button != null) button.interactable = !value;
            if (value)
            {
                pointerInside = false;
                SetHovered(false);
            }
            RefreshArtworkAlpha();
        }

        public void ResetInteractionVisuals()
        {
            pointerInside = false;
            dragSuppressed = false;
            CancelAllMotions();
            hoverProgress = 0f;
            tiltValue = Vector2.zero;
            if (animatedVisual != null)
            {
                animatedVisual.localRotation = Quaternion.identity;
                animatedVisual.localScale = Vector3.one;
            }
            if (shineViewport != null) shineViewport.SetAsLastSibling();
            if (shineImage != null) shineImage.gameObject.SetActive(false);
            ApplyVisualState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            scroller?.NotifyPointerDown();
        }

        public void PlayDeniedCue()
        {
            // Kept intentionally separate from the flight profile. A denied-build shake can be added
            // later without coupling resource feedback to hover state.
        }

        private void OnButtonClicked()
        {
            if (IsLocked || (scroller != null && scroller.ShouldSuppressClick)) return;
            onClick?.Invoke(this);
        }

        private bool CanHover(RectTransform rect)
        {
            return rect != null && !IsLocked && !dragSuppressed && ParentCanvasGroupsAllowInteraction() &&
                   (scroller == null || scroller.CanHover(rect));
        }

        private bool TryGetPointerOverCard(out Vector2 screenPosition, out Camera eventCamera)
        {
            screenPosition = default;
            eventCamera = uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? uiCanvas.worldCamera
                : null;

            Mouse mouse = Mouse.current;
            RectTransform rect = hitRect != null ? hitRect : transform as RectTransform;
            if (mouse == null || !CanHover(rect)) return false;

            screenPosition = mouse.position.ReadValue();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, screenPosition, eventCamera, out Vector2 local))
                return false;

            Rect hitArea = rect.rect;
            Vector4 padding = button != null && button.targetGraphic != null
                ? button.targetGraphic.raycastPadding
                : Vector4.zero;
            hitArea.xMin += padding.x;
            hitArea.yMin += padding.y;
            hitArea.xMax -= padding.z;
            hitArea.yMax -= padding.w;
            return hitArea.Contains(local);
        }

        private bool ParentCanvasGroupsAllowInteraction()
        {
            if (interactionCanvasGroups == null) return true;

            for (int i = 0; i < interactionCanvasGroups.Length; i++)
            {
                CanvasGroup group = interactionCanvasGroups[i];
                if (group == null || !group.isActiveAndEnabled) continue;
                if (group.alpha <= 0.001f || !group.interactable || !group.blocksRaycasts)
                    return false;
                if (group.ignoreParentGroups) break;
            }
            return true;
        }

        private void SetHovered(bool value)
        {
            if (motionProfile == null)
            {
                hoverProgress = value ? 1f : 0f;
                ApplyVisualState();
                return;
            }

            Cancel(ref hoverMotion);
            float target = value ? 1f : 0f;
            float duration = value ? motionProfile.hoverInDuration : motionProfile.hoverOutDuration;
            AnimationCurve ease = value ? motionProfile.hoverInEase : motionProfile.hoverOutEase;

            hoverMotion = LMotion.Create(hoverProgress, target, duration)
                .WithEase(ease)
                .WithScheduler(MotionScheduler.InitializationIgnoreTimeScale)
                .Bind(x =>
                {
                    hoverProgress = x;
                    ApplyVisualState();
                });

            if (value) StartShine();
            else
            {
                StopShine();
                StartTiltReset();
            }
        }

        private void ApplyPointerTilt(Vector2 screenPosition, Camera eventCamera)
        {
            RectTransform rect = hitRect != null ? hitRect : transform as RectTransform;
            if (rect == null || motionProfile == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, screenPosition, eventCamera, out Vector2 local))
                return;

            Vector2 half = rect.rect.size * 0.5f;
            Vector2 normalized = new(
                half.x > 0f ? Mathf.Clamp(local.x / half.x, -1f, 1f) : 0f,
                half.y > 0f ? Mathf.Clamp(local.y / half.y, -1f, 1f) : 0f);

            Cancel(ref tiltMotion);
            tiltValue = new Vector2(
                motionProfile.tiltXResponseCurve.Evaluate(normalized.x),
                motionProfile.tiltYResponseCurve.Evaluate(normalized.y));
            ApplyTilt();
        }

        private void StartTiltReset()
        {
            Cancel(ref tiltMotion);
            if (motionProfile == null || motionProfile.tiltResetDuration <= 0f)
            {
                tiltValue = Vector2.zero;
                ApplyTilt();
                return;
            }

            tiltMotion = LMotion.Create(tiltValue, Vector2.zero, motionProfile.tiltResetDuration)
                .WithEase(motionProfile.tiltResetEase)
                .WithScheduler(MotionScheduler.InitializationIgnoreTimeScale)
                .Bind(x =>
                {
                    tiltValue = x;
                    ApplyTilt();
                });
        }

        private void ApplyTilt()
        {
            if (animatedVisual == null || motionProfile == null) return;

            // Screen Space Overlay canvases do not project X/Y rotations consistently across
            // separate sliced UI graphics. A small roll, parallax, and axis squeeze produces the
            // same pointer-following depth cue while keeping every supplied pixel-art layer intact.
            float scaleX = 1f - Mathf.Abs(tiltValue.x) * motionProfile.maxTiltY / 200f;
            float scaleY = 1f - Mathf.Abs(tiltValue.y) * motionProfile.maxTiltX / 200f;
            animatedVisual.localScale = new Vector3(scaleX, scaleY, 1f);
            animatedVisual.localRotation = Quaternion.Euler(
                0f, 0f, -tiltValue.x * motionProfile.maxRoll);
            animatedVisual.anchoredPosition = new Vector2(
                tiltValue.x * motionProfile.maxTiltY * 0.25f,
                currentLift + tiltValue.y * motionProfile.maxTiltX * 0.2f);
        }

        private void ApplyVisualState()
        {
            if (motionProfile == null) return;

            Vector2 size = Vector2.LerpUnclamped(
                motionProfile.restingCardSize, motionProfile.hoveredCardSize, hoverProgress);
            currentLift = (selected ? motionProfile.selectedLift : 0f) +
                          motionProfile.hoverLift * hoverProgress;

            if (animatedVisual != null)
                ApplyTilt();
            if (cardBody != null)
                cardBody.sizeDelta = size;
            if (selectionFrame != null)
                selectionFrame.sizeDelta = size + motionProfile.selectionFramePadding;
            if (itemContainer != null)
            {
                itemContainer.sizeDelta = PixelSnap(Vector2.LerpUnclamped(
                    motionProfile.restingItemContainerSize,
                    motionProfile.hoveredItemContainerSize,
                    hoverProgress));
                itemContainer.anchoredPosition = PixelSnap(Vector2.LerpUnclamped(
                    motionProfile.restingItemContainerPosition,
                    motionProfile.hoveredItemContainerPosition,
                    hoverProgress));
            }
            if (costPanel != null && costPanel.transform is RectTransform resourcePanel)
            {
                resourcePanel.sizeDelta = PixelSnap(motionProfile.resourcePanelBackgroundSize);
                resourcePanel.anchoredPosition = PixelSnap(new Vector2(
                    motionProfile.resourcePanelBottomOffset.x,
                    -size.y * 0.5f + motionProfile.resourcePanelBottomOffset.y));
            }
            if (resourceIconImage != null)
            {
                resourceIconImage.rectTransform.sizeDelta = PixelSnap(motionProfile.resourceIconSize);
                resourceIconImage.rectTransform.anchoredPosition =
                    PixelSnap(motionProfile.resourceIconOffset);
            }
            if (costText != null)
            {
                costText.rectTransform.sizeDelta = PixelSnap(motionProfile.resourceTextRectSize);
                costText.rectTransform.anchoredPosition =
                    PixelSnap(motionProfile.resourceTextOffset);
                costText.fontSize = motionProfile.resourceTextFontSize;
            }

            float thicknessHeight = Mathf.LerpUnclamped(
                motionProfile.restingThickness, motionProfile.hoveredThickness, hoverProgress);
            if (thickness != null)
            {
                thickness.sizeDelta = new Vector2(size.x, thicknessHeight);
                thickness.anchoredPosition = new Vector2(0f, -size.y * 0.5f - thicknessHeight * 0.5f);
            }

            if (shadow != null)
            {
                shadow.sizeDelta = Vector2.LerpUnclamped(
                    motionProfile.restingShadowSize, motionProfile.hoveredShadowSize, hoverProgress);
                shadow.anchoredPosition = Vector2.LerpUnclamped(
                    motionProfile.restingShadowOffset, motionProfile.hoveredShadowOffset, hoverProgress);
            }

            if (shadowImage != null)
            {
                Color c = shadowImage.color;
                c.a = Mathf.LerpUnclamped(
                    motionProfile.restingShadowAlpha, motionProfile.hoveredShadowAlpha, hoverProgress);
                shadowImage.color = c;
            }

            if (shineViewport != null)
                shineViewport.sizeDelta = new Vector2(Mathf.Max(1f, size.x - 4f), Mathf.Max(1f, size.y - 4f));
        }

        private void StartShine()
        {
            if (motionProfile == null || shineTransform == null || shineImage == null) return;
            StopShine();

            shineTransform.localScale = new Vector3(
                motionProfile.shineScale.x, motionProfile.shineScale.y, 1f);
            shineTransform.localRotation = Quaternion.Euler(0f, 0f, motionProfile.shineRotation);
            shineTransform.anchoredPosition = motionProfile.shineStartPosition;

            Color c = shineImage.color;
            c.a = motionProfile.shineAlpha;
            shineImage.color = c;
            shineImage.gameObject.SetActive(true);

            shineMotion = LMotion.Create(
                    motionProfile.shineStartPosition, motionProfile.shineStopPosition,
                    motionProfile.shineTravelDuration)
                .WithEase(Ease.Linear)
                .WithDelay(motionProfile.shineInterval, DelayType.EveryLoop)
                .WithLoops(-1, LoopType.Restart)
                .WithScheduler(MotionScheduler.InitializationIgnoreTimeScale)
                .Bind(position => shineTransform.anchoredPosition = position);
        }

        private void StopShine()
        {
            Cancel(ref shineMotion);
            if (shineImage != null) shineImage.gameObject.SetActive(false);
        }

        private void RefreshArtworkAlpha()
        {
            if (artworkCanvasGroup == null || motionProfile == null) return;
            artworkCanvasGroup.alpha = IsLocked
                ? motionProfile.lockedArtworkAlpha
                : affordable ? 1f : motionProfile.unaffordableAlpha;
        }

        private void RefreshCost(StructureDef def)
        {
            bool hasCost = def.cost != null && def.cost.Count > 0 && def.cost[0] != null;
            if (costPanel != null) costPanel.SetActive(hasCost);
            if (!hasCost) return;

            ResourceCost cost = def.cost[0];
            if (resourceIconImage != null) resourceIconImage.sprite = FindResourceIcon(cost.resourceType);
            if (costText != null) costText.text = FormatAmount(cost.amount);
        }

        private Sprite FindResourceIcon(ResourceType type)
        {
            if (resourceIcons == null) return null;
            for (int i = 0; i < resourceIcons.Length; i++)
                if (resourceIcons[i].type == type) return resourceIcons[i].sprite;
            return null;
        }

        private static string FormatAmount(float value)
        {
            string[] suffixes = { "", "k", "M", "B", "T" };
            int tier = 0;
            float display = value;
            while (Mathf.Abs(display) >= 10000f && tier < suffixes.Length - 1)
            {
                display /= 1000f;
                tier++;
            }
            return Mathf.FloorToInt(display) + suffixes[tier];
        }

        private static string ToRoman(int value)
        {
            (int value, string numeral)[] numerals =
            {
                (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
                (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
                (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
            };

            value = Mathf.Max(1, value);
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < numerals.Length; i++)
            {
                while (value >= numerals[i].value)
                {
                    result.Append(numerals[i].numeral);
                    value -= numerals[i].value;
                }
            }
            return result.ToString();
        }

        private static Vector2 PixelSnap(Vector2 value) =>
            new(Mathf.Round(value.x), Mathf.Round(value.y));

        private void CancelAllMotions()
        {
            Cancel(ref hoverMotion);
            Cancel(ref tiltMotion);
            Cancel(ref shineMotion);
        }

        private static void Cancel(ref MotionHandle handle)
        {
            if (handle.IsActive()) handle.Cancel();
            handle = default;
        }
    }
}
