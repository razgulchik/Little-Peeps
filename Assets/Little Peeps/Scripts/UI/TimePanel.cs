using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The time strip at the top of the HUD: the offering clock drawn as a belt of years crawling left
    // toward NOW, the circle. A segment of the belt is one year of the schedule (Segment Seconds) and is
    // always the same length on screen, so the belt always moves at one speed — a longer cycle is just
    // more years between now and its offering.
    //
    // On the belt: the line, a year tick on every segment, and where a cycle ends an offering tick (the
    // small diamond) in the year tick's place. The offering tick is drawn in three: its line ends go on
    // Belt with the line and the year ticks, whose clip ends them at the circle's edge and at the dots; its
    // diamond goes on Riders, whose clip reaches from inside the circle to the big diamond. So the small
    // diamond starts INSIDE the big one — it shows once the belt has brought its place there — slides out
    // over the dots, and rides on into the circle: the offering has arrived when its diamond is centred on
    // the circle, the moment its cycle runs out. The hands (the offering icon) wait under the big diamond
    // and leave with the small one, under it all the way — they are centred on the same column, so they
    // set off from where they stand.
    //
    // Under every tick its year: the segments gone since the run's start, so the numbers run on across
    // cycles and start again with the next run. The tick the hands ride to has none — the hands stand
    // where its number would.
    //
    // While the pointer is on the strip (its own Image catches it), the offering's tooltip is up.
    //
    // Nothing here keeps time of its own: the belt is laid out from the offering clock, so it stands still
    // wherever the clock does (perk pick, age transition, the end-of-cycle window) and cannot drift from
    // it. It moves in whole SCREEN pixels, not the art's: one art pixel is several screen pixels, and
    // stepping by those made the belt jerk. Every piece still moves by whole screen pixels, so the art
    // stays crisp — nothing is stretched or blurred. Laid out again only when it has moved one.
    //
    // The moving pieces are made here — the ticks and the line from the three sprites, the numbers from a
    // template label — and reused for good; the prefab holds only the fixed parts.
    public class TimePanel : MonoBehaviour, IUIElement, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Parts")]
        [Tooltip("The circle — NOW. An offering has arrived when its diamond is centred on it.")]
        [SerializeField] private RectTransform now;

        [Tooltip("Where the line, the year ticks and the offering ticks' line ends are made. Its RectMask2D " +
                 "runs from the circle's edge to the dots.")]
        [SerializeField] private RectTransform belt;

        [Tooltip("Where the offering ticks' diamonds are made. Its clip reaches from inside the circle to " +
                 "the big diamond, so a diamond starts in the big one and rides on into the circle.")]
        [SerializeField] private RectTransform riders;

        [Tooltip("The offering icon. Where it stands in the prefab is where it waits — under the big diamond, " +
                 "centred on it: a small diamond starts from that column, and the hands leave with it.")]
        [SerializeField] private RectTransform hands;

        [Header("Art")]
        [SerializeField] private Sprite lineSprite;
        [SerializeField] private Sprite yearSprite;

        [Tooltip("The offering tick: the small diamond with a piece of line either side.")]
        [SerializeField] private Sprite offeringSprite;

        [Header("Layout")]
        [Tooltip("One segment — one year — in pixels. Fixed: the belt moves this many pixels per Segment " +
                 "Seconds of the Offering Schedule.")]
        [SerializeField, Min(1)] private int segmentWidth = 58;

        [Tooltip("Width of the diamond in the middle of the offering tick, in pixels: the part drawn on the " +
                 "riders. What is either side of it — the gaps and the line ends — goes on the belt.")]
        [SerializeField, Min(1)] private int diamondWidth = 5;

        [Header("Years")]
        [Tooltip("The year number under a tick — the template every number is copied from; it stays switched " +
                 "off itself. The numbers are made beside it, so its parent's RectMask2D clips them like the " +
                 "belt; their height is the template's. Centred under the tick here, on whole pixels.")]
        [SerializeField] private TMP_Text yearTemplate;

        [Header("Tooltip")]
        [Tooltip("The offering's tooltip, up while the pointer is on the strip.")]
        [SerializeField] private OfferingTooltip tooltip;

        private OfferingSystem offerings;

        // Measured once, in this panel's own space: the circle's centre, where the belt starts, the strip's
        // edges, where the hands wait, where the numbers' parent starts.
        private RectTransform panel;
        private float nowX, beltLeft, stripLeft, stripRight, handsHomeX, yearsLeft, yearY;
        private Vector2 handsHome;
        private bool ready;

        // Screen pixels per canvas unit live from here — the CanvasScaler changes it with the window.
        private Canvas rootCanvas;

        // The offering tick cut in three at Start: what is left of the diamond, the diamond, what is right of it.
        private Sprite offeringLeft, offeringDiamond, offeringRight;

        private readonly List<Image> lines = new();
        private readonly List<Image> ticks = new();
        private readonly List<Image> leftEnds = new();
        private readonly List<Image> rightEnds = new();
        private readonly List<Image> diamonds = new();
        private readonly List<YearLabel> labels = new();

        // A year number on the belt, with what it shows: its text is rebuilt only when the number changes.
        private sealed class YearLabel
        {
            public TMP_Text text;
            public int year = -1;
            public float width;   // the number's advance width, whole pixels
        }

        // What the belt shows now; laid out again only when one of these changes.
        private int shownCycle = -1;
        private int shownSteps = -1;
        private float shownScale;

        // From UIRoot (UIServices).
        public void Initialize(UIServices ui) => offerings = ui.OfferingSystem;

        private void Start()
        {
            if (now == null || belt == null || riders == null || hands == null ||
                lineSprite == null || yearSprite == null || offeringSprite == null)
            {
                Debug.LogWarning("[TimePanel] a part or a sprite is not assigned — the time strip stays empty.",
                                 this);
                return;
            }

            if (!CutOffering()) return;

            panel = (RectTransform)transform;
            nowX = PanelX(now, now.rect.center.x);
            beltLeft = PanelX(belt, belt.rect.xMin);
            stripLeft = PanelX(riders, riders.rect.xMin);
            stripRight = PanelX(riders, riders.rect.xMax);
            handsHome = hands.anchoredPosition;
            handsHomeX = PanelX(hands, hands.rect.center.x);

            if (yearTemplate != null && yearTemplate.transform.parent is RectTransform yearsParent)
            {
                yearsLeft = PanelX(yearsParent, yearsParent.rect.xMin);
                yearY = yearTemplate.rectTransform.anchoredPosition.y;
                yearTemplate.gameObject.SetActive(false);
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) rootCanvas = canvas.rootCanvas;
            ready = true;
        }

        // The offering sprite in three, side by side in its own texture: everything left of the diamond, the
        // diamond (Diamond Width, in the middle), everything right of it. Needs the sprite whole in a texture
        // — a tightly packed atlas sprite has no rectangle to cut.
        private bool CutOffering()
        {
            if (offeringSprite.packed && offeringSprite.packingMode == SpritePackingMode.Tight)
            {
                Debug.LogWarning("[TimePanel] the offering sprite is tightly packed in an atlas and cannot be " +
                                 "cut — the time strip stays empty. Pack it as a rectangle.", this);
                return false;
            }

            Rect r = offeringSprite.textureRect;
            float diamond = Mathf.Min(diamondWidth, r.width);
            float side = Mathf.Floor((r.width - diamond) / 2f);
            offeringLeft = Cut(r.x, side);
            offeringDiamond = Cut(r.x + side, diamond);
            offeringRight = Cut(r.x + side + diamond, r.width - side - diamond);
            return true;

            Sprite Cut(float x, float width) => width <= 0f ? null :
                Sprite.Create(offeringSprite.texture, new Rect(x, r.y, width, r.height), new Vector2(0.5f, 0.5f),
                              offeringSprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private void OnDestroy()
        {
            if (offeringLeft != null) Destroy(offeringLeft);
            if (offeringDiamond != null) Destroy(offeringDiamond);
            if (offeringRight != null) Destroy(offeringRight);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltip != null) tooltip.Show();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip != null) tooltip.Hide();
        }

        private void OnDisable()
        {
            if (tooltip != null) tooltip.Hide();
        }

        private void Update()
        {
            if (!ready) return;

            float scale = rootCanvas != null && rootCanvas.scaleFactor > 0f ? rootCanvas.scaleFactor : 1f;

            // No cycle (no schedule, no run) = cycle 0: the belt stands at its start, no offering on it.
            bool active = offerings != null && offerings.IsActive;
            int cycle = active ? offerings.Cycle : 0;
            int steps = active ? Steps(offerings.Elapsed, offerings.SegmentSeconds, segmentWidth * scale) : 0;
            if (cycle == shownCycle && steps == shownSteps && scale == shownScale) return;

            shownCycle = cycle;
            shownSteps = steps;
            shownScale = scale;
            Layout(cycle, steps / scale);
        }

        // How far the belt has moved this cycle, in whole screen pixels. A cycle is a whole number of
        // segments, so it always starts on a tick: this is all the belt needs to know where its ticks are.
        private static int Steps(float elapsed, float segmentSeconds, float segmentOnScreen) =>
            segmentSeconds > 0f ? Mathf.RoundToInt(elapsed / segmentSeconds * segmentOnScreen) : 0;

        // Ticks are counted from the current cycle's start: tick k is k segments of time from it, so it
        // stands at nowX + k × width − moved. The current cycle's offering is tick "its length", the next
        // one a next cycle's length further on. Where a piece stands at the cycle's start is whole canvas
        // pixels; `moved` is whole screen pixels — together, every piece lands on a screen pixel.
        private void Layout(int cycle, float moved)
        {
            int lineCount = 0, tickCount = 0, endCount = 0, diamondCount = 0, labelCount = 0;

            // From the tick at or just left of the strip's start, so the line reaches the edge. Ticks of a
            // cycle already gone are behind the circle and never show.
            int k = Mathf.Max(0, Mathf.FloorToInt((stripLeft - nowX + moved) / segmentWidth));

            int offeringCycle = cycle;
            int offeringTick = Segments(cycle);   // 0 = no offering ahead
            while (offeringTick > 0 && offeringTick < k) offeringTick = NextOffering(ref offeringCycle, offeringTick);

            // Tick k's year: the segments of the cycles gone, plus k. The current offering's tick has the hands.
            int firstYear = cycle > 0 ? offerings.StartSegmentOf(cycle) : 0;
            int handsTick = Segments(cycle);

            float lastRight = 0f;
            for (int i = 0; i < 64; i++, k++)
            {
                bool isOffering = offeringTick > 0 && k == offeringTick;
                if (isOffering) offeringTick = NextOffering(ref offeringCycle, offeringTick);

                Sprite sprite = isOffering ? offeringSprite : yearSprite;
                float width = sprite.rect.width;
                float centre = nowX + k * segmentWidth;
                float left = Mathf.Round(centre - width / 2f) - moved;

                if (i > 0) PlaceLine(ref lineCount, lastRight, left);
                if (isOffering) PlaceOffering(ref endCount, ref diamondCount, left, centre - moved);
                else Place(ticks, tickCount++, belt, yearSprite, left - beltLeft);

                lastRight = left + width;
                if (left >= stripRight) break;   // past the big diamond: this tick only closes the line

                if (k != handsTick) PlaceYear(labelCount++, firstYear + k, centre, moved);
            }

            HideFrom(lines, lineCount);
            HideFrom(ticks, tickCount);
            HideFrom(leftEnds, endCount);
            HideFrom(rightEnds, endCount);
            HideFrom(diamonds, diamondCount);
            for (int i = labelCount; i < labels.Count; i++)
                if (labels[i].text.gameObject.activeSelf) labels[i].text.gameObject.SetActive(false);

            // The hands wait under the big diamond and leave with this cycle's small one: from the moment it
            // shows it stands on their column, so they set off from where they are.
            float offeringStart = nowX + Segments(cycle) * segmentWidth;
            bool riding = cycle > 0 && offeringStart - moved <= handsHomeX;
            hands.anchoredPosition = riding
                ? handsHome + new Vector2(Mathf.Round(offeringStart - handsHomeX) - moved, 0f)
                : handsHome;
        }

        // An offering tick: its line ends on the belt, with the line (clipped where the line is); its diamond
        // on the riders — once the belt has brought it to the big diamond, where it starts from.
        private void PlaceOffering(ref int endCount, ref int diamondCount, float left, float centreNow)
        {
            float diamondLeft = left + (offeringLeft != null ? offeringLeft.rect.width : 0f);
            if (offeringLeft != null) Place(leftEnds, endCount, belt, offeringLeft, left - beltLeft);
            if (offeringRight != null)
                Place(rightEnds, endCount, belt, offeringRight, diamondLeft + offeringDiamond.rect.width - beltLeft);
            endCount++;

            if (centreNow <= handsHomeX)
                Place(diamonds, diamondCount++, riders, offeringDiamond, diamondLeft - stripLeft);
        }

        private int Segments(int cycle) => cycle > 0 && offerings != null ? offerings.SegmentsOf(cycle) : 0;

        // The tick of the offering after `tick`, one cycle's length further on; 0 = none.
        private int NextOffering(ref int cycle, int tick)
        {
            int length = Segments(++cycle);
            return length > 0 ? tick + length : 0;
        }

        // The line between two ticks — whatever lies between `from` and `to` — on the belt.
        private void PlaceLine(ref int count, float from, float to)
        {
            if (to <= from) return;

            Image line = Place(lines, count++, belt, lineSprite, from - beltLeft);
            line.rectTransform.sizeDelta = new Vector2(to - from, lineSprite.rect.height);
        }

        // A year under its tick. Centred on whole pixels: the tick's centre sits on a half pixel, so a number
        // of odd width centres exactly and an even one half a pixel to the left — never between pixels.
        private void PlaceYear(int index, int year, float centre, float moved)
        {
            if (yearTemplate == null) return;

            YearLabel label = index < labels.Count ? labels[index] : MakeLabel();
            if (!label.text.gameObject.activeSelf) label.text.gameObject.SetActive(true);
            if (label.year != year)
            {
                label.year = year;
                label.text.SetText("{0}", year);
                label.width = Mathf.Round(label.text.GetPreferredValues().x);
            }

            float left = Mathf.Floor(centre + 0.5f - label.width / 2f) - moved;
            label.text.rectTransform.anchoredPosition = new Vector2(left - yearsLeft, yearY);
        }

        // A copy of the template beside it, held by its parent's left edge and left-aligned: the centring is
        // done above, whatever the template's own anchors and alignment.
        private YearLabel MakeLabel()
        {
            TMP_Text text = Instantiate(yearTemplate, yearTemplate.transform.parent);
            text.gameObject.name = "Year";
            text.horizontalAlignment = HorizontalAlignmentOptions.Left;

            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(0f, rt.anchorMax.y);
            rt.pivot = new Vector2(0f, rt.pivot.y);

            var label = new YearLabel { text = text };
            labels.Add(label);
            return label;
        }

        private Image Place(List<Image> pool, int index, RectTransform parent, Sprite sprite, float x)
        {
            Image image = index < pool.Count ? pool[index] : Make(pool, parent, sprite);
            if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
            image.rectTransform.anchoredPosition = new Vector2(x, 0f);
            return image;
        }

        // A piece on the parent's left edge, centred on its middle row — the line's row. One sprite pixel is
        // one canvas unit: the HUD's sprites and its Canvas are both at 1 pixel per unit.
        private Image Make(List<Image> pool, RectTransform parent, Sprite sprite)
        {
            var go = new GameObject(sprite.name, typeof(RectTransform), typeof(Image)) { layer = gameObject.layer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = sprite.rect.size;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;

            pool.Add(image);
            return image;
        }

        private static void HideFrom(List<Image> pool, int count)
        {
            for (int i = count; i < pool.Count; i++)
                if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }

        // x of a point of `rt` (in rt's own space) in this panel's space, snapped to the half pixel: the
        // strip is laid out in whole pixels, so edges come out whole and centres of odd sizes on the half.
        private float PanelX(RectTransform rt, float localX)
        {
            float x = panel.InverseTransformPoint(rt.TransformPoint(new Vector3(localX, 0f, 0f))).x;
            return Mathf.Round(x * 2f) / 2f;
        }
    }
}
