using System;
using System.Collections.Generic;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittlePeeps
{
    // One build-palette card. The root is the layout/click slot; AnimatedVisual is free to grow, rise and
    // tilt without changing the list's spacing. Icon and cost objects are never scaled by flight.
    //
    // The card in hand is OPEN: wider, with the building's name, description and the terrains it may stand
    // on. Its slot widens with it (the row moves the neighbours apart), it grows upward and does not fly.
    // Selected and open are one state — the palette reports the hand (SetSelected), the card animates it.
    [RequireComponent(typeof(Button))]
    public class BuildCardUI : MonoBehaviour, IPointerDownHandler
    {
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

        [Header("Card content")]
        [SerializeField] private RectTransform itemContainer;
        [SerializeField] private Image iconImage;
        // The price of the next one: icon + amount, the HUD's own price tag. Anchored to the card's centre;
        // where it stands is the profile's.
        [SerializeField] private ResourceUnit costUnit;
        // Optional: "1/2" on a building with a limit, hidden on one without. Left empty, the card shows no
        // count — the limit still locks it when reached.
        [SerializeField] private TMP_Text countText;
        [SerializeField] private CanvasGroup artworkCanvasGroup;
        [SerializeField] private ResourceIconSet iconSet;

        [Header("Open card")]
        [Tooltip("The column shown while the card is open: it fades in over the end of the opening.")]
        [SerializeField] private CanvasGroup infoGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [Tooltip("One object per terrain. The card shows those its building may stand on — every one for a " +
                 "building without a terrain rule.")]
        [SerializeField] private List<TerrainChip> terrainChips = new();

        [Header("Shine")]
        [SerializeField] private RectTransform shineViewport;
        [SerializeField] private RectTransform shineTransform;
        [SerializeField] private Image shineImage;

        [Header("Age lock")]
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private TMP_Text lockedText;

        [Header("Hint")]
        [Tooltip("Show the hover tooltip above this build card.")]
        [SerializeField] private bool showHint;
        [Tooltip("The hint window opens above this rect. Empty = the hit rect; point it at a child placed " +
                 "where the card flies to if the hint should clear the raised card.")]
        [SerializeField] private RectTransform hintAnchor;

        [Serializable]
        private struct TerrainChip
        {
            public TerrainType terrain;
            public GameObject chip;
        }

        public StructureDef Def { get; private set; }
        public bool IsLocked { get; private set; }

        private Action<BuildCardUI> onClick;
        private BuildPanelScroller scroller;
        private BuildCardRow cardRow;
        private RectTransform slot;      // the root: the card's place in the row
        private MotionHandle hoverMotion;
        private MotionHandle expandMotion;
        private MotionHandle tiltMotion;
        private MotionHandle shineMotion;
        private float hoverProgress;
        private float expandProgress;    // 0 closed .. 1 open
        private float currentLift;
        private Vector2 tiltValue;
        private Vector4 restingRaycastPadding;
        private bool selected;
        private bool affordable = true;
        private int standing;   // how many of Def stand — the price shown is the next one's
        private int limit;      // only meaningful when Def.HasLimit
        private StructureLock lockState;
        private bool pointerInside;
        private bool dragSuppressed;
        private Canvas uiCanvas;
        private CanvasGroup[] interactionCanvasGroups;

        private ResourceSystem wallet;   // the hint paints each price line red or not against it
        private readonly HintContent hint = new();
        private bool hintShown;
        private bool hintSuppressed;     // clicked: no hint until the pointer leaves the card
        private bool hintStale;          // count / lock / wallet changed while the hint may be up

        private void Reset()
        {
            button = GetComponent<Button>();
            hitRect = transform as RectTransform;
        }

        public void Init(StructureDef def, Action<BuildCardUI> onClick, ResourceSystem wallet)
        {
            Def = def;
            this.onClick = onClick;
            this.wallet = wallet;
            slot = transform as RectTransform;
            cardRow = slot != null && slot.parent != null ? slot.parent.GetComponent<BuildCardRow>() : null;
            scroller = GetComponentInParent<BuildPanelScroller>();
            uiCanvas = GetComponentInParent<Canvas>();
            interactionCanvasGroups = GetComponentsInParent<CanvasGroup>(true);
            ConfigureShineLocalSpace();

            if (iconImage != null) iconImage.sprite = def.icon;
            FillInfo(def);
            ShowCount(null);   // before any run: nothing stands yet

            if (button == null) button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
            if (button.targetGraphic != null) restingRaycastPadding = button.targetGraphic.raycastPadding;

            SetAffordable(true);
            SetLocked(def.LockState(RunContext.FirstAge, null));   // before any run: nothing opened by a perk yet
            ResetInteractionVisuals();
        }

        private void Update()
        {
            dragSuppressed = scroller != null && scroller.IsDragging;
            bool over = TryGetPointerOverCard(out Vector2 screenPosition, out Camera eventCamera);
            UpdateHint(over);

            // A locked card stays put — but can still show its hint when enabled. The open card
            // stays put too: it carries text, and flight would only shake it.
            bool shouldHover = over && !IsLocked && !selected;
            if (shouldHover != pointerInside)
            {
                pointerInside = shouldHover;
                SetHovered(shouldHover);
            }

            // Live tilt is a stateless pointer-to-transform mapping. It is sampled every frame
            // while hovered, so there is no response tween to restart, lag behind or go stale.
            if (shouldHover) ApplyPointerTilt(screenPosition, eventCamera);
        }

        private void OnEnable()
        {
            RefreshDrawOrder();
        }

        private void OnDisable()
        {
            CancelAllMotions();
            if (cardRow != null) cardRow.SetDrawPriority(slot, 0);
            HideHint();
            hintSuppressed = false;
        }

        private void OnDestroy()
        {
            CancelAllMotions();
        }

        // Selected = in hand = open. The palette passes on every change of the hand to every card, so most
        // calls change nothing.
        public void SetSelected(bool value)
        {
            if (value == selected) return;
            selected = value;

            if (value)
            {
                // The open card says itself what the hint would: it neither flies nor shows the hint.
                pointerInside = false;
                SetHovered(false);
                HideHint();
            }
            SetExpanded(value);
        }

        public void SetAffordable(bool value)
        {
            affordable = value;
            hintStale = true;
            RefreshArtworkAlpha();
        }

        // The run-dependent numbers: how many of Def stand, which sets the price on the card (the NEXT
        // one's) and, on a building with a limit, the "1/2" count. Before SetLocked, whose limit text
        // reads them. A null run = nothing stands, the bare limit.
        public void ShowCount(RunContext run)
        {
            if (Def == null) return;
            standing = run != null ? run.CountOf(Def) : 0;
            limit = Def.LimitIn(run);
            hintStale = true;

            RefreshCost();
            if (countText != null)
            {
                countText.gameObject.SetActive(Def.HasLimit);
                if (Def.HasLimit) countText.text = $"{standing}/{limit}";
            }
        }

        // The overlay says what would open the card: its age, a perk, or a free slot under the limit
        // (StructureDef.LockState decides which wins when several apply).
        public void SetLocked(StructureLock state)
        {
            bool value = state != StructureLock.None;
            IsLocked = value;
            lockState = state;
            hintStale = true;
            if (lockedOverlay != null) lockedOverlay.SetActive(value);
            if (lockedText != null && Def != null)
                lockedText.text = state switch
                {
                    StructureLock.Perk => "Open with\na perk",
                    StructureLock.Limit => $"Limit\n{standing}/{limit}",
                    _ => $"Open on\nthe Age {RomanNumeral.From(Def.requiredAge)}",
                };

            if (button != null) button.interactable = !value;
            if (value)
            {
                pointerInside = false;
                SetHovered(false);
            }
            RefreshArtworkAlpha();
        }

        // Drops every tween and lands on the state the card is in: closed, or open if it is in hand.
        public void ResetInteractionVisuals()
        {
            pointerInside = false;
            dragSuppressed = false;
            CancelAllMotions();
            hoverProgress = 0f;
            expandProgress = selected ? 1f : 0f;
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
            hintSuppressed = true;
            HideHint();
            onClick?.Invoke(this);
        }

        // The open card's column: what the building is and where it may stand. Terrains by the rule
        // placement itself uses (IslandGrid.IsTerrainAllowed), so the card never promises a different one.
        private void FillInfo(StructureDef def)
        {
            if (titleText != null) titleText.text = def.ShownName;
            if (descriptionText != null) descriptionText.text = def.description;
            foreach (var entry in terrainChips)
                if (entry.chip != null)
                    entry.chip.SetActive(IslandGrid.IsTerrainAllowed(entry.terrain, def.allowedTerrain));
        }

        // Whether the pointer may count as over this card at all. Lock is not part of it: a locked card
        // gets no flight (Update) but still shows its hint.
        private bool CanHover(RectTransform rect)
        {
            return rect != null && !dragSuppressed && ParentCanvasGroupsAllowInteraction() &&
                   (scroller == null || scroller.CanHover(rect));
        }

        // The hint follows the pointer over the card — locked ones included, they are where "why is it shut"
        // gets asked. A click puts it away until the pointer leaves: the tool is in hand. A refresh of the
        // card's numbers while the hint is up republishes it (same owner = updated in place). None over the
        // open card: it shows its name and description itself.
        private void UpdateHint(bool over)
        {
            if (!over) hintSuppressed = false;
            bool want = showHint && over && !hintSuppressed && !selected;

            if (!want) HideHint();
            else if (!hintShown || hintStale) PublishHint();
        }

        private void PublishHint()
        {
            hintShown = true;
            hintStale = false;
            StructureHint.Fill(hint, Def, standing, limit, lockState, wallet);

            RectTransform anchor = hintAnchor != null ? hintAnchor
                                 : hitRect != null ? hitRect
                                 : transform as RectTransform;
            EventBus<HintShowEvent>.Publish(new HintShowEvent { Owner = this, Content = hint, Anchor = anchor });
        }

        private void HideHint()
        {
            if (!hintShown) return;
            hintShown = false;
            EventBus<HintHideEvent>.Publish(new HintHideEvent { Owner = this });
        }

        private bool TryGetPointerOverCard(out Vector2 screenPosition, out Camera eventCamera)
        {
            screenPosition = default;
            eventCamera = uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? uiCanvas.worldCamera
                : null;

            RectTransform rect = hitRect != null ? hitRect : transform as RectTransform;
            if (!CanHover(rect)) return false;

            // Legacy Input.mousePosition is intentionally used here. With both input backends
            // enabled, Input System's Mouse.current can alternate device state between frames in
            // the Editor Game view, making hover enter/exit flap every frame on part of the panel.
            screenPosition = Input.mousePosition;
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
            RefreshDrawOrder();
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

        // Opens or closes the card. A hidden card (a tab not on screen) has nothing to watch: it lands on
        // the end state at once.
        private void SetExpanded(bool value)
        {
            RefreshDrawOrder();
            Cancel(ref expandMotion);
            float target = value ? 1f : 0f;
            if (motionProfile == null || !isActiveAndEnabled)
            {
                expandProgress = target;
                ApplyVisualState();
                return;
            }

            float duration = value ? motionProfile.expandDuration : motionProfile.collapseDuration;
            AnimationCurve ease = value ? motionProfile.expandEase : motionProfile.collapseEase;

            expandMotion = LMotion.Create(expandProgress, target, duration)
                .WithEase(ease)
                .WithScheduler(MotionScheduler.InitializationIgnoreTimeScale)
                .Bind(x =>
                {
                    expandProgress = x;
                    ApplyVisualState();
                });
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

        // Where each moving part of the card stands in one state. The profile gives three — resting,
        // hovered, open — and ApplyVisualState blends them: resting → hovered by the hover tween, that →
        // open by the open tween.
        private struct Pose
        {
            public Vector2 size;               // the card body
            public float lift;                 // the body's centre above the slot's
            public Vector2 containerSize;
            public Vector2 containerPosition;  // from the body's centre
            public Vector2 costOffset;         // x from the body's centre, y up from its bottom edge
            public float thickness;
            public Vector2 shadowSize;
            public Vector2 shadowOffset;       // from the slot's centre
            public float shadowAlpha;

            public static Pose Lerp(in Pose a, in Pose b, float t) => new()
            {
                size = Vector2.LerpUnclamped(a.size, b.size, t),
                lift = Mathf.LerpUnclamped(a.lift, b.lift, t),
                containerSize = Vector2.LerpUnclamped(a.containerSize, b.containerSize, t),
                containerPosition = Vector2.LerpUnclamped(a.containerPosition, b.containerPosition, t),
                costOffset = Vector2.LerpUnclamped(a.costOffset, b.costOffset, t),
                thickness = Mathf.LerpUnclamped(a.thickness, b.thickness, t),
                shadowSize = Vector2.LerpUnclamped(a.shadowSize, b.shadowSize, t),
                shadowOffset = Vector2.LerpUnclamped(a.shadowOffset, b.shadowOffset, t),
                shadowAlpha = Mathf.LerpUnclamped(a.shadowAlpha, b.shadowAlpha, t),
            };
        }

        private Pose RestingPose() => new()
        {
            size = motionProfile.restingCardSize,
            lift = 0f,
            containerSize = motionProfile.restingItemContainerSize,
            containerPosition = motionProfile.restingItemContainerPosition,
            costOffset = motionProfile.costBottomOffset,
            thickness = motionProfile.restingThickness,
            shadowSize = motionProfile.restingShadowSize,
            shadowOffset = motionProfile.restingShadowOffset,
            shadowAlpha = motionProfile.restingShadowAlpha,
        };

        private Pose HoveredPose() => new()
        {
            size = motionProfile.hoveredCardSize,
            lift = motionProfile.hoverLift,
            containerSize = motionProfile.hoveredItemContainerSize,
            containerPosition = motionProfile.hoveredItemContainerPosition,
            costOffset = motionProfile.costBottomOffset,
            thickness = motionProfile.hoveredThickness,
            shadowSize = motionProfile.hoveredShadowSize,
            shadowOffset = motionProfile.hoveredShadowOffset,
            shadowAlpha = motionProfile.hoveredShadowAlpha,
        };

        // Grows up from the slot's bottom edge: the body's centre rises by half the extra height, plus the
        // open card's own lift.
        private Pose ExpandedPose() => new()
        {
            size = motionProfile.expandedCardSize,
            lift = (motionProfile.expandedCardSize.y - motionProfile.restingCardSize.y) * 0.5f +
                   motionProfile.expandedLift,
            containerSize = motionProfile.expandedItemContainerSize,
            containerPosition = motionProfile.expandedItemContainerPosition,
            costOffset = motionProfile.expandedCostBottomOffset,
            thickness = motionProfile.restingThickness,
            shadowSize = motionProfile.expandedShadowSize,
            shadowOffset = motionProfile.expandedShadowOffset,
            shadowAlpha = motionProfile.expandedShadowAlpha,
        };

        private void ApplyVisualState()
        {
            RefreshDrawOrder();
            if (motionProfile == null) return;

            Pose pose = Pose.Lerp(
                Pose.Lerp(RestingPose(), HoveredPose(), hoverProgress), ExpandedPose(), expandProgress);
            currentLift = pose.lift;
            ApplySlot();

            if (animatedVisual != null)
                ApplyTilt();
            if (cardBody != null)
                cardBody.sizeDelta = pose.size;
            if (itemContainer != null)
            {
                itemContainer.sizeDelta = PixelSnap(pose.containerSize);
                itemContainer.anchoredPosition = PixelSnap(pose.containerPosition);
            }
            if (costUnit != null && costUnit.transform is RectTransform costRect)
                PlaceCentred(costRect, new Vector2(pose.costOffset.x, -pose.size.y * 0.5f + pose.costOffset.y));

            if (thickness != null)
            {
                thickness.sizeDelta = new Vector2(pose.size.x, pose.thickness);
                thickness.anchoredPosition = new Vector2(0f, -pose.size.y * 0.5f - pose.thickness * 0.5f);
            }

            // Cover the full face and its bottom edge, including while a newly locked card settles.
            if (lockedOverlay != null && lockedOverlay.transform is RectTransform overlayRect)
            {
                float depth = thickness != null ? pose.thickness : 0f;
                overlayRect.sizeDelta = new Vector2(pose.size.x, pose.size.y + depth);
                overlayRect.anchoredPosition = new Vector2(0f, -depth * 0.5f);
                if (lockedText != null)
                    lockedText.rectTransform.anchoredPosition = new Vector2(0f, depth * 0.5f);
            }

            if (shadow != null)
            {
                shadow.sizeDelta = pose.shadowSize;
                shadow.anchoredPosition = pose.shadowOffset;
            }

            if (shadowImage != null)
            {
                Color c = shadowImage.color;
                c.a = pose.shadowAlpha;
                shadowImage.color = c;
            }

            if (shineViewport != null)
                shineViewport.sizeDelta = new Vector2(Mathf.Max(1f, pose.size.x - 4f), Mathf.Max(1f, pose.size.y - 4f));

            if (infoGroup != null)
                infoGroup.alpha = Mathf.InverseLerp(motionProfile.expandedTextFrom, 1f, expandProgress);
        }

        private void RefreshDrawOrder()
        {
            if (cardRow == null || slot == null) return;

            // The pointer wins over an open card; a returning card stays above its resting neighbours
            // until it has landed. Move the whole card so its overlay and border always travel together.
            int hoverOrder = motionProfile != null ? Mathf.Max(3, motionProfile.hoverSortingOrder) : 100;
            int priority = !isActiveAndEnabled ? 0
                : pointerInside ? hoverOrder
                : selected ? hoverOrder - 1
                : hoverProgress > 0f || expandProgress > 0f ? 1 : 0;
            cardRow.SetDrawPriority(slot, priority);
        }

        // The card's place in the row: as wide as the card is open, so the row's Horizontal Layout Group
        // moves the neighbours apart. Whole pixels — the row adds these widths up into positions. The click
        // area grows over the open card's top, which stands above the slot: a click there must stay on the
        // card, not reach the island behind it.
        private void ApplySlot()
        {
            if (slot == null) return;

            float width = Mathf.Round(Mathf.LerpUnclamped(
                motionProfile.restingCardSize.x, motionProfile.expandedCardSize.x, expandProgress));
            Vector2 size = new(width, motionProfile.restingCardSize.y);

            if (button != null && button.targetGraphic != null)
            {
                float rise = motionProfile.expandedCardSize.y + motionProfile.expandedLift -
                             motionProfile.restingCardSize.y;
                Vector4 padding = restingRaycastPadding;
                padding.w -= Mathf.Round(Mathf.Max(0f, rise) * expandProgress);
                button.targetGraphic.raycastPadding = padding;
            }

            if (slot.sizeDelta == size) return;
            slot.sizeDelta = size;
            if (scroller != null && isActiveAndEnabled) scroller.Follow(slot, selected);
            else if (slot.parent is RectTransform row) LayoutRebuilder.MarkLayoutForRebuild(row);
        }

        private void StartShine()
        {
            if (motionProfile == null || shineTransform == null || shineImage == null) return;
            StopShine();

            RectTransform localShine = shineTransform;
            Vector2 shineStartPosition = motionProfile.shineStartPosition;
            Vector2 shineStopPosition = motionProfile.shineStopPosition;
            shineTransform.localScale = new Vector3(
                motionProfile.shineScale.x, motionProfile.shineScale.y, 1f);
            shineTransform.localRotation = Quaternion.Euler(0f, 0f, motionProfile.shineRotation);
            shineTransform.anchoredPosition = shineStartPosition;

            Color c = shineImage.color;
            c.a = motionProfile.shineAlpha;
            shineImage.color = c;
            shineImage.gameObject.SetActive(true);

            shineMotion = LMotion.Create(
                    shineStartPosition, shineStopPosition,
                    motionProfile.shineTravelDuration)
                .WithEase(Ease.Linear)
                .WithDelay(motionProfile.shineInterval, DelayType.EveryLoop)
                .WithLoops(-1, LoopType.Restart)
                .WithScheduler(MotionScheduler.InitializationIgnoreTimeScale)
                .Bind(position =>
                {
                    if (localShine != null)
                        localShine.anchoredPosition = position;
                });
        }

        private void ConfigureShineLocalSpace()
        {
            if (shineViewport == null || shineTransform == null) return;

            // Each instantiated card owns a self-contained shine coordinate system. Explicitly
            // normalize it so neither the horizontal layout nor the card's canvas position can
            // become part of the authored start/stop coordinates.
            shineViewport.anchorMin = Vector2.one * 0.5f;
            shineViewport.anchorMax = Vector2.one * 0.5f;
            shineViewport.pivot = Vector2.one * 0.5f;
            shineViewport.anchoredPosition = Vector2.zero;
            shineViewport.localRotation = Quaternion.identity;
            shineViewport.localScale = Vector3.one;

            if (shineTransform.parent != shineViewport)
                shineTransform.SetParent(shineViewport, false);
            shineTransform.anchorMin = Vector2.one * 0.5f;
            shineTransform.anchorMax = Vector2.one * 0.5f;
            shineTransform.pivot = Vector2.one * 0.5f;
        }

        private void StopShine()
        {
            Cancel(ref shineMotion);
            if (shineImage != null) shineImage.gameObject.SetActive(false);
        }

        private void RefreshArtworkAlpha()
        {
            if (artworkCanvasGroup == null || motionProfile == null) return;
            // The lock overlay greys out an opaque card, so the world cannot show through it.
            artworkCanvasGroup.alpha = IsLocked || affordable ? 1f : motionProfile.unaffordableAlpha;
        }

        // The first cost entry at the price the NEXT one costs — it grows with how many stand.
        private void RefreshCost()
        {
            bool hasCost = Def.cost != null && Def.cost.Count > 0 && Def.cost[0] != null;
            if (costUnit != null) costUnit.gameObject.SetActive(hasCost);
            if (!hasCost || costUnit == null) return;

            ResourceCost cost = Def.cost[0];
            costUnit.Bind(iconSet != null ? iconSet.IconFor(cost.resourceType) : null, Def.CostAt(0, standing));
        }

        private static Vector2 PixelSnap(Vector2 value) =>
            new(Mathf.Round(value.x), Mathf.Round(value.y));

        // Centres a rect (anchored to its parent's centre) on `centre` with its edges on whole pixels — an
        // odd-sized one has a half-pixel centre. A half-way value rounds up, so a .5 in the profile picks
        // the row above, not whichever happens to be even.
        private static void PlaceCentred(RectTransform rect, Vector2 centre)
        {
            Vector2 size = rect.rect.size;
            Vector2 min = centre - size * 0.5f;
            min = new Vector2(Mathf.Floor(min.x + 0.5f), Mathf.Floor(min.y + 0.5f));
            rect.anchoredPosition = min + Vector2.Scale(size, rect.pivot);
        }

        private void CancelAllMotions()
        {
            Cancel(ref hoverMotion);
            Cancel(ref expandMotion);
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
