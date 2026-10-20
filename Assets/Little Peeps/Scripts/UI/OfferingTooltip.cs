using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The offering's tooltip on the time strip: how many years are left until the offering, and what the
    // gods want — every resource of the quota with its amount, red while the stock is short of it. TimePanel
    // shows it while the pointer is on the strip. It hangs under the offering's hands, its tail pointing up
    // at them, and follows them as they ride the belt.
    //
    // Live while up: the years count down, the colours follow the stock, a new cycle brings its quota.
    //
    // It sizes itself, HintView's way: the root carries a Vertical Layout Group (Control Child Size: width
    // and height on; Child Force Expand: width on) and NO Content Size Fitter — a fitter would fight the size
    // set here. The width is kept so the centred price grid lands on whole pixels; the header is left-aligned
    // for the same reason.
    //
    // It never takes a click — the CanvasGroup here stops raycasts — so the world under it stays clickable.
    [RequireComponent(typeof(CanvasGroup))]
    public class OfferingTooltip : MonoBehaviour, IUIElement
    {
        [Header("Parts")]
        [SerializeField] private TMP_Text header;

        [Tooltip("Parent of the price lines. Its Grid Layout Group sets the cell, the spacing and the columns.")]
        [SerializeField] private RectTransform costContainer;

        [Tooltip("What the tail points at: the offering's hands.")]
        [SerializeField] private RectTransform anchor;

        [Header("Price lines")]
        [Tooltip("The resource bar's unit: icon + amount, the same look as the wallet and the age price.")]
        [SerializeField] private ResourceUnit costPrefab;
        [SerializeField] private ResourceIconSet iconSet;

        [Tooltip("Amount colour for a resource the stock is short of. The colour when it is covered is whatever " +
                 "the ResourceUnit prefab authors.")]
        [SerializeField] private Color shortColor = new Color(0.6078f, 0.1294f, 0.1176f);

        [Header("Words")]
        [Tooltip("Colour of the word Offering in the header.")]
        [SerializeField] private Color highlight = new Color(1f, 0.8392f, 0.3882f);

        [Header("Place")]
        [Tooltip("Where the tooltip's top-right corner — the corner with the tail — stands, measured from the " +
                 "anchor's bottom-right corner, in Canvas pixels.")]
        [SerializeField] private Vector2 offset = new Vector2(-2f, -8f);

        private OfferingSystem offerings;
        private ResourceSystem resources;

        private RectTransform window;
        private CanvasGroup group;
        private LayoutGroup layout;
        private GridLayoutGroup grid;
        private readonly List<ResourceUnit> units = new();   // pooled price lines, the extras switched off
        private readonly Vector3[] corners = new Vector3[4];

        private bool shown;
        private bool stockChanged;
        private int shownYears = -1;
        private int shownCycle = -1;
        private Vector3 placedAt = new Vector3(float.NaN, 0f, 0f);

        // From UIRoot (UIServices).
        public void Initialize(UIServices ui)
        {
            offerings = ui.OfferingSystem;
            resources = ui.ResourceSystem;
        }

        private void Awake()
        {
            window = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            layout = GetComponent<LayoutGroup>();
            if (costContainer != null) grid = costContainer.GetComponent<GridLayoutGroup>();

            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;
        }

        private void OnEnable() => EventBus<ResourceChangedEvent>.Subscribe(OnResourceChanged);

        private void OnDisable()
        {
            EventBus<ResourceChangedEvent>.Unsubscribe(OnResourceChanged);
            Hide();
        }

        private void OnResourceChanged(ResourceChangedEvent _) => stockChanged = true;

        public void Show()
        {
            if (offerings == null || !offerings.IsActive || header == null) return;

            shown = true;
            shownYears = -1;
            shownCycle = -1;
            Refresh();
            Place();
            group.alpha = 1f;
        }

        public void Hide()
        {
            shown = false;
            if (group != null) group.alpha = 0f;
        }

        // After TimePanel's Update has moved the hands, so the tail stays on them in the same frame.
        private void LateUpdate()
        {
            if (!shown) return;
            if (!offerings.IsActive)
            {
                Hide();
                return;
            }

            Refresh();
            Place();
        }

        private void Refresh()
        {
            bool resize = false;

            int years = YearsLeft();
            if (years != shownYears)
            {
                shownYears = years;
                header.text = $"{years} {(years == 1 ? "year" : "years")} until " +
                              $"<color=#{ColorUtility.ToHtmlStringRGB(highlight)}>Offering</color>";
                resize = true;
            }

            if (offerings.Cycle != shownCycle)
            {
                shownCycle = offerings.Cycle;
                FillCosts();
                stockChanged = true;
                resize = true;
            }

            if (stockChanged)
            {
                stockChanged = false;
                TintCosts();
            }

            if (resize) Resize();
        }

        // Whole years left, counted up: the offering is "1 year" away until it arrives.
        private int YearsLeft()
        {
            float segment = offerings.SegmentSeconds;
            return segment > 0f ? Mathf.Max(0, Mathf.CeilToInt(offerings.TimeLeft / segment - 0.0001f)) : 0;
        }

        private void FillCosts()
        {
            if (costContainer == null) return;

            IReadOnlyList<ResourceCost> quota = offerings.Quota;
            int count = costPrefab != null ? quota.Count : 0;

            // On before spawning: a ResourceUnit reads its authored colour in Awake, which waits for an
            // active parent — a tint landing first would become its "authored" colour.
            costContainer.gameObject.SetActive(count > 0);

            for (int i = 0; i < count; i++)
            {
                if (i == units.Count) units.Add(Instantiate(costPrefab, costContainer));

                ResourceUnit unit = units[i];
                unit.gameObject.SetActive(true);
                unit.Bind(iconSet != null ? iconSet.IconFor(quota[i].resourceType) : null, quota[i].amount);
            }

            // Switched off, not destroyed: a layout group skips inactive children, and the next cycle reuses them.
            for (int i = count; i < units.Count; i++) units[i].gameObject.SetActive(false);
        }

        private void TintCosts()
        {
            IReadOnlyList<ResourceCost> quota = offerings.Quota;
            for (int i = 0; i < quota.Count && i < units.Count; i++)
            {
                bool covered = resources != null && resources.GetResource(quota[i].resourceType) >= quota[i].amount;
                if (covered) units[i].ClearTint();
                else units[i].Tint(shortColor);
            }
        }

        // As wide as the header or the price grid, whichever is wider — one pixel wider when that leaves the
        // centred grid on a half pixel — then as tall as the layout needs at that width.
        private void Resize()
        {
            float inner = Mathf.Ceil(header.GetPreferredValues(header.text).x);

            int count = costContainer != null && costContainer.gameObject.activeSelf ? ActiveUnits() : 0;
            if (count > 0 && grid != null)
            {
                int columns = grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                    ? Mathf.Min(grid.constraintCount, count)
                    : count;
                float gridWidth = columns * grid.cellSize.x + (columns - 1) * grid.spacing.x;
                inner = Mathf.Max(inner, gridWidth);
                if (Mathf.RoundToInt(inner - gridWidth) % 2 != 0) inner += 1f;
            }

            float padding = layout != null ? layout.padding.horizontal : 0f;
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, inner + padding);

            LayoutRebuilder.ForceRebuildLayoutImmediate(window);
            float height = Mathf.Ceil(LayoutUtility.GetPreferredHeight(window));
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(window);

            placedAt.x = float.NaN;   // the size moved the corner the place is measured by
        }

        private int ActiveUnits()
        {
            int count = 0;
            for (int i = 0; i < units.Count; i++)
                if (units[i].gameObject.activeSelf) count++;
            return count;
        }

        // The tail's corner at the anchor's bottom-right corner plus the offset. Moved only when that has moved.
        private void Place()
        {
            if (anchor == null || !(window.parent is RectTransform parent)) return;

            anchor.GetWorldCorners(corners);   // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
            Vector2 corner = (Vector2)parent.InverseTransformPoint(corners[3]) + offset;

            // localPosition is the pivot; rect.max is the top-right corner measured from the pivot.
            Vector3 position = corner - window.rect.max;
            if (position == placedAt) return;

            placedAt = position;
            window.localPosition = position;
        }
    }
}
