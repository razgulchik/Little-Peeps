using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The end of an offering cycle: the gods demand their offering. Two answers — Pay the Offering (only
    // while the stock covers the quota) and Face the Wrath (always, held to confirm, since it ends the run)
    // — over what the gods want, every resource red while the stock is short of it. OfferingEndState asks
    // through Show and acts on the answer; this owns the CONTENT and the clicks, nothing else.
    //
    // A screen, not ConfirmDialog: a question with content of its own (icons, amounts) is a screen. So its
    // visibility is a row in UIVisibility (OfferingEnd) like every screen's, and the object must stay
    // ACTIVE for PerkSelectionUI's reason — the CanvasGroup is what hides it. Its backdrop swallows the
    // clicks meant for the world underneath.
    //
    // The answers keep ConfirmDialog's contract: exactly one call, made after the window has let go of both
    // callbacks, so a double click, or both buttons in the same frame, counts once, and a late click after
    // Hide reaches nobody.
    //
    // The quota is laid out here, in whole pixels: rows of `perRow` units, each row centred on its own, the
    // block centred in the cost area. A grid would leave a short last row hanging on the left.
    [RequireComponent(typeof(CanvasGroup))]
    public class OfferingWindow : MonoBehaviour, IUIElement
    {
        [Header("Answers")]
        [SerializeField] private Button payButton;

        [Tooltip("Optional. Dims the whole Pay button — frame and words — while the stock cannot pay.")]
        [SerializeField] private CanvasGroup payGroup;

        [Tooltip("Pay button's opacity while the stock cannot pay.")]
        [Range(0f, 1f)] [SerializeField] private float payDisabledAlpha = 0.5f;

        [SerializeField] private HoldButton wrathButton;

        [Header("Quota")]
        [Tooltip("The area the price units are laid out in. Its edges must sit on whole pixels.")]
        [SerializeField] private RectTransform costArea;

        [Tooltip("One unit per resource: icon + amount. Its root size is the cell.")]
        [SerializeField] private ResourceUnit costPrefab;
        [SerializeField] private ResourceIconSet iconSet;

        [Min(1)] [SerializeField] private int perRow = 3;

        [Tooltip("Gap between units in a row (x) and between rows (y), in Canvas pixels.")]
        [SerializeField] private Vector2Int spacing = new Vector2Int(6, 0);

        [Tooltip("Amount colour for a resource the stock is short of. The colour when it is covered is whatever " +
                 "the unit prefab authors.")]
        [SerializeField] private Color shortColor = new Color(0.6078f, 0.1294f, 0.1176f);

        private ResourceSystem resources;
        private readonly List<ResourceUnit> units = new();   // pooled, the extras switched off

        private Action onOffer;
        private Action onWrath;
        private bool canOffer;

        public bool IsOpen { get; private set; }

        // From UIRoot (UIServices). The stock is read only to colour the amounts — whether the quota can be
        // paid is the caller's word (canOffer), not this window's.
        public void Initialize(UIServices ui) => resources = ui.ResourceSystem;

        private void OnDisable() => Unwire();

        // Wiring tripwire, in Start so every Awake has had its chance first. A window that cannot take an
        // answer leaves OfferingEndState with the game frozen under it.
        private void Start()
        {
            if (payButton == null || wrathButton == null)
                Debug.LogError($"[OfferingWindow] '{name}' is missing a button — the end of a cycle cannot be " +
                               "answered through it.", this);
        }

        // Ask. The answer arrives as exactly one call to onOffer or onWrath. `canOffer` false greys Pay out —
        // the window still shows the offering, it just cannot be paid now. Every Show sets it afresh.
        //
        // Returns whether the window can actually take an answer. False when the object is inactive (the
        // CanvasGroup cannot show anything then) or a button is missing: a caller that freezes the game
        // around its question must then not freeze it.
        public bool Show(IReadOnlyList<ResourceCost> quota, bool canOffer, Action onOffer, Action onWrath)
        {
            if (!gameObject.activeInHierarchy || payButton == null || wrathButton == null)
            {
                Debug.LogError($"[OfferingWindow] '{name}' cannot ask — it is inactive or missing a button. Keep " +
                               "the object active (its CanvasGroup is what hides it) and assign both buttons.", this);
                return false;
            }

            if (IsOpen)
                Debug.LogWarning($"[OfferingWindow] '{name}' asked again while still open — the earlier question " +
                                 "is dropped unanswered.", this);

            this.onOffer = onOffer;
            this.onWrath = onWrath;
            this.canOffer = canOffer;

            // Here rather than in OnEnable, idempotently: the answers must reach this window even where
            // OnEnable never ran (Edit Mode), and a second Show must not add a second listener.
            Wire();

            payButton.interactable = canOffer;
            if (payGroup != null) payGroup.alpha = canOffer ? 1f : payDisabledAlpha;
            wrathButton.Interactable = true;

            FillCosts(quota);

            IsOpen = true;
            return true;
        }

        // Close without answering. Drops both callbacks, so a click that lands after this reaches nobody.
        // Safe when already closed: the state that asked calls it from its Exit, whichever way it is left.
        public void Hide()
        {
            onOffer = null;
            onWrath = null;
            IsOpen = false;
            if (wrathButton != null) wrathButton.Interactable = false;
        }

        private void Wire()
        {
            Unwire();
            payButton.onClick.AddListener(OnPay);
            wrathButton.Confirmed += OnWrath;
        }

        private void Unwire()
        {
            if (payButton != null) payButton.onClick.RemoveListener(OnPay);
            if (wrathButton != null) wrathButton.Confirmed -= OnWrath;
        }

        // A greyed Pay cannot be given by any route — the button is not interactable, and this checks too.
        private void OnPay()
        {
            if (canOffer) Answer(onOffer);
        }

        private void OnWrath() => Answer(onWrath);

        // Close FIRST, then call — ConfirmDialog.Answer's reasoning: the callback is the caller's continuation
        // and is free to leave its state, whose Exit calls Hide.
        private void Answer(Action choice)
        {
            if (!IsOpen) return;

            Hide();
            choice?.Invoke();
        }

        private void FillCosts(IReadOnlyList<ResourceCost> quota)
        {
            if (costArea == null || costPrefab == null) return;

            int count = quota != null ? quota.Count : 0;
            for (int i = 0; i < count; i++)
            {
                if (i == units.Count) units.Add(Instantiate(costPrefab, costArea));

                ResourceUnit unit = units[i];
                unit.gameObject.SetActive(true);
                unit.Bind(iconSet != null ? iconSet.IconFor(quota[i].resourceType) : null, quota[i].amount);

                bool covered = resources != null && resources.GetResource(quota[i].resourceType) >= quota[i].amount;
                if (covered) unit.ClearTint();
                else unit.Tint(shortColor);
            }

            // Switched off, not destroyed: the next cycle reuses them.
            for (int i = count; i < units.Count; i++) units[i].gameObject.SetActive(false);

            LayOut(count);
        }

        // Rows of perRow, each centred on its own; the block centred in the area. Floor keeps every unit on a
        // whole pixel whatever the parity of the leftover space.
        private void LayOut(int count)
        {
            if (count == 0) return;

            Vector2 cell = ((RectTransform)costPrefab.transform).rect.size;
            Rect area = costArea.rect;

            int rows = (count + perRow - 1) / perRow;
            float blockHeight = rows * cell.y + (rows - 1) * spacing.y;
            float top = Mathf.Floor((area.height - blockHeight) / 2f);

            for (int i = 0; i < count; i++)
            {
                int row = i / perRow;
                int inRow = Mathf.Min(perRow, count - row * perRow);
                float rowWidth = inRow * cell.x + (inRow - 1) * spacing.x;
                float left = Mathf.Floor((area.width - rowWidth) / 2f);

                var box = (RectTransform)units[i].transform;
                box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 1f);
                box.sizeDelta = cell;
                box.anchoredPosition = new Vector2(left + (i % perRow) * (cell.x + spacing.x),
                                                   -(top + row * (cell.y + spacing.y)));
            }
        }
    }
}
