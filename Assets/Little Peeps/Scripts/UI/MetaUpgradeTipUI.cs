using TMPro;
using UnityEngine;

namespace LittlePeeps
{
    // The tip of the Ancestral Wisdom screen: a meta perk's name and description, attached to the right side of
    // its cell, up at once while the pointer rests there. Its own window rather than HintView, which is shared
    // by the rest of the game: this sprite has no left edge — it grows out of the cell — and the cell asks it
    // directly, so there is no delay to skip.
    //
    // It sizes itself, HintView's way: as wide as its longest line up to Max Width (past it the text wraps), as
    // tall as what is shown. The padding is the text's own rect: stretched inside this one, its offsets are the
    // margins of the sprite.
    //
    // It never takes a click — the CanvasGroup here stops raycasts — so the cells under it stay clickable.
    [RequireComponent(typeof(CanvasGroup))]
    public class MetaUpgradeTipUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        [Tooltip("Colour of the name on the first line. The description keeps the label's own colour.")]
        [SerializeField] private Color titleColor = new Color(0.6588f, 0.7059f, 0.8392f);

        [Tooltip("The tip grows with its text up to this width (Canvas pixels); a longer text wraps.")]
        [SerializeField, Min(1)] private float maxWidth = 83f;

        [Tooltip("The tip's top-left corner against the cell's top-right corner.")]
        [SerializeField] private Vector2 offset = new Vector2(0f, -2f);

        [Tooltip("Smallest distance kept between the tip and the edge of the screen.")]
        [SerializeField, Min(0)] private float screenMargin = 2f;

        private RectTransform window;
        private CanvasGroup group;
        private readonly Vector3[] corners = new Vector3[4];

        private void Awake()
        {
            window = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;
        }

        public void Show(string title, string body, RectTransform cell)
        {
            if (text == null) return;

            string heading = $"<color=#{ColorUtility.ToHtmlStringRGB(titleColor)}>{title}</color>";
            text.text = string.IsNullOrEmpty(body) ? heading : $"{heading}\n{body}";

            Resize();
            Place(cell);
            group.alpha = 1f;
        }

        public void Hide()
        {
            if (group != null) group.alpha = 0f;
        }

        // Width first — the longest line, capped — then the height the text needs at that width.
        private void Resize()
        {
            Vector2 padding = -text.rectTransform.sizeDelta;   // a stretched rect's size delta: minus its margins

            float textWidth = Mathf.Min(Mathf.Ceil(text.GetPreferredValues(text.text).x), maxWidth - padding.x);
            float textHeight = Mathf.Ceil(text.GetPreferredValues(text.text, textWidth, Mathf.Infinity).y);

            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth + padding.x);
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight + padding.y);
        }

        // Pushed inside the screen edges, on whole Canvas pixels, so the pixel font stays crisp.
        private void Place(RectTransform cell)
        {
            if (!(window.parent is RectTransform parent)) return;

            cell.GetWorldCorners(corners);   // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
            Vector2 topRight = parent.InverseTransformPoint(corners[2]);
            Rect area = parent.rect;
            Vector2 size = window.rect.size;

            float x = topRight.x + offset.x;
            float y = topRight.y + offset.y - size.y;

            x = Mathf.Clamp(x, area.xMin + screenMargin, Mathf.Max(area.xMin + screenMargin, area.xMax - screenMargin - size.x));
            y = Mathf.Clamp(y, area.yMin + screenMargin, Mathf.Max(area.yMin + screenMargin, area.yMax - screenMargin - size.y));

            // localPosition is the pivot; rect.min is the bottom-left corner measured from the pivot.
            var bottomLeft = new Vector2(Mathf.Round(x), Mathf.Round(y));
            window.localPosition = bottomLeft - window.rect.min;
        }
    }
}
