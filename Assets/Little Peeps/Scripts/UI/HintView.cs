using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The one hint window on the Canvas. A UI element publishes HintShowEvent while the pointer rests on it
    // and HintHideEvent when it leaves; the window shows that content above the element (below it when
    // there is no room above), kept inside the Canvas. One window for every kind of hint: its sections —
    // title, body, price lines, footer — each switch off while their part of the HintContent is empty.
    //
    // The window sizes itself: as wide as its longest line up to Max Width (past it the text wraps), as tall
    // as what is shown. So the prefab root carries a Vertical Layout Group (Control Child Size: width and
    // height on; Child Force Expand: width on, height off) and NO Content Size Fitter — a fitter would
    // fight the width set here. Anchors on one point, not stretched.
    //
    // Place it as the LAST child of the Canvas root, outside Screens: drawn over everything, and no UI
    // mode's CanvasGroup can hide it. It never takes a click — the CanvasGroup here stops raycasts.
    [RequireComponent(typeof(CanvasGroup))]
    public class HintView : MonoBehaviour
    {
        [Header("Sections")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("Parent of the price lines, with any Layout Group: a row, a column or a grid.")]
        [SerializeField] private RectTransform costContainer;
        [SerializeField] private TMP_Text footerText;

        [Header("Price lines")]
        [Tooltip("The resource bar's unit: icon + amount, the same look as the wallet and the age price.")]
        [SerializeField] private ResourceUnit costPrefab;
        [SerializeField] private ResourceIconSet iconSet;
        [Tooltip("Amount colour for a price the player cannot pay yet. The affordable colour is whatever " +
                 "the ResourceUnit prefab authors.")]
        [SerializeField] private Color unaffordableColor = new Color(0.85f, 0.25f, 0.25f);

        [Header("Size and place")]
        [Tooltip("The window grows with its text up to this width (Canvas pixels); a longer text wraps.")]
        [SerializeField, Min(1)] private float maxWidth = 160f;
        [Tooltip("Space between the window and the element it describes.")]
        [SerializeField] private float gap = 4f;
        [Tooltip("Smallest distance kept between the window and the Canvas edge.")]
        [SerializeField, Min(0)] private float screenMargin = 2f;

        [Header("Timing")]
        [Tooltip("Seconds the pointer rests on an element before its hint appears. Unscaled time: build " +
                 "mode is paused.")]
        [SerializeField, Min(0)] private float showDelay = 0.25f;
        [Tooltip("A hint asked for within this many seconds of the last one closing appears at once, so " +
                 "sliding along the cards does not wait out the delay again on each.")]
        [SerializeField, Min(0)] private float chainWindow = 0.3f;

        private RectTransform window;
        private CanvasGroup group;
        private LayoutGroup layout;
        private readonly List<ResourceUnit> units = new();   // pooled price lines, the extras switched off
        private readonly Vector3[] corners = new Vector3[4];

        private object owner;        // whose hint is up or waiting out the delay; null = none
        private HintContent content;
        private RectTransform anchor;
        private bool shown;
        private float showAt;        // unscaled time the waiting hint appears
        private float hiddenAt = float.NegativeInfinity;   // unscaled time the last shown hint closed

        private void Awake()
        {
            window = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            layout = GetComponent<LayoutGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            if (layout == null)
                Debug.LogWarning("[HintView] no Vertical Layout Group on the window — the hint has nothing " +
                                 "to stack its sections or measure its height with.", this);
        }

        private void OnEnable()
        {
            EventBus<HintShowEvent>.Subscribe(OnShow);
            EventBus<HintHideEvent>.Subscribe(OnHide);
        }

        private void OnDisable()
        {
            EventBus<HintShowEvent>.Unsubscribe(OnShow);
            EventBus<HintHideEvent>.Unsubscribe(OnHide);
            Close();
        }

        private void OnShow(HintShowEvent e)
        {
            if (e.Owner == null || e.Content == null || e.Anchor == null) return;

            bool sameOwner = e.Owner == owner;
            owner = e.Owner;
            content = e.Content;
            anchor = e.Anchor;

            // A new source starts its own delay — unless a hint closed a moment ago, then it shows at once.
            if (!shown && !sameOwner)
            {
                float now = Time.unscaledTime;
                showAt = now - hiddenAt <= chainWindow ? now : now + showDelay;
            }

            // Already up (a refresh, or straight from one source to the next) or no wait: show it now.
            if (shown || Time.unscaledTime >= showAt) Present();
        }

        private void OnHide(HintHideEvent e)
        {
            if (owner != null && e.Owner == owner) Close();
        }

        private void Update()
        {
            if (owner == null) return;

            // A source that went away without saying so takes its hint with it.
            if (anchor == null || !anchor.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }

            if (!shown && Time.unscaledTime >= showAt) Present();
        }

        private void Present()
        {
            shown = true;
            Fill();
            Resize();
            Place();
            group.alpha = 1f;
        }

        private void Close()
        {
            if (shown) hiddenAt = Time.unscaledTime;
            owner = null;
            content = null;
            anchor = null;
            shown = false;
            if (group != null) group.alpha = 0f;
        }

        private void Fill()
        {
            SetText(titleText, content.title);
            SetText(bodyText, content.body);
            SetText(footerText, content.footer);
            FillCosts();
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text == null) return;
            bool on = !string.IsNullOrEmpty(value);
            text.gameObject.SetActive(on);
            if (on) text.text = value;
        }

        private void FillCosts()
        {
            if (costContainer == null) return;

            int count = costPrefab != null ? content.costs.Count : 0;
            // On before spawning: a ResourceUnit reads its authored colour in Awake, which waits for an
            // active parent — a tint landing first would become its "authored" colour.
            costContainer.gameObject.SetActive(count > 0);

            for (int i = 0; i < count; i++)
            {
                if (i == units.Count) units.Add(Instantiate(costPrefab, costContainer));

                HintCost line = content.costs[i];
                ResourceUnit unit = units[i];
                unit.gameObject.SetActive(true);
                unit.Bind(iconSet != null ? iconSet.IconFor(line.resourceType) : null, line.amount);
                if (line.affordable) unit.ClearTint();
                else unit.Tint(unaffordableColor);
            }

            // Switched off, not destroyed: a layout group skips inactive children, and the next hint reuses them.
            for (int i = count; i < units.Count; i++) units[i].gameObject.SetActive(false);
        }

        // Width first — the longest line, capped — then the height the layout needs at that width.
        private void Resize()
        {
            float widest = Mathf.Max(PreferredWidth(titleText), PreferredWidth(bodyText), PreferredWidth(footerText));
            if (costContainer != null && costContainer.gameObject.activeSelf)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(costContainer);
                widest = Mathf.Max(widest, LayoutUtility.GetPreferredWidth(costContainer));
            }

            float padding = layout != null ? layout.padding.horizontal : 0f;
            float width = Mathf.Ceil(Mathf.Min(widest + padding, maxWidth));
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            // The first pass wraps the text at the new width, which is what decides the height.
            LayoutRebuilder.ForceRebuildLayoutImmediate(window);
            float height = Mathf.Ceil(LayoutUtility.GetPreferredHeight(window));
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(window);
        }

        private static float PreferredWidth(TMP_Text text) =>
            text != null && text.gameObject.activeSelf ? Mathf.Ceil(text.GetPreferredValues(text.text).x) : 0f;

        // Centred above the anchor; below it when the top of the Canvas is too close; then pushed inside
        // the Canvas edges. Whole Canvas pixels, so the pixel font stays crisp.
        private void Place()
        {
            if (!(window.parent is RectTransform parent)) return;

            anchor.GetWorldCorners(corners);   // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
            Vector2 low = parent.InverseTransformPoint(corners[0]);
            Vector2 high = parent.InverseTransformPoint(corners[2]);
            Rect area = parent.rect;
            Vector2 size = window.rect.size;

            float x = (low.x + high.x - size.x) * 0.5f;
            float y = high.y + gap;
            if (y + size.y > area.yMax - screenMargin) y = low.y - gap - size.y;

            x = Mathf.Clamp(x, area.xMin + screenMargin, Mathf.Max(area.xMin + screenMargin, area.xMax - screenMargin - size.x));
            y = Mathf.Clamp(y, area.yMin + screenMargin, Mathf.Max(area.yMin + screenMargin, area.yMax - screenMargin - size.y));

            // localPosition is the pivot; rect.min is the bottom-left corner measured from the pivot.
            var bottomLeft = new Vector2(Mathf.Round(x), Mathf.Round(y));
            window.localPosition = bottomLeft - window.rect.min;
        }
    }
}
