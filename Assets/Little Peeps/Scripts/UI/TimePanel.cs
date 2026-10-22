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
    // On the belt: the line, a year tick on every segment, and where a cycle ends an offering tick in the
    // year tick's place — the big diamond with a piece of line either side. The offering tick is drawn in
    // two: its line ends go on Belt with the line and the year ticks, whose clip ends them at the circle's
    // edge and at the dots; its diamond goes on Riders, whose clip reaches from inside the circle to the
    // right end.
    //
    // At the right end is the stand, where the offering waits: the big diamond (the prefab's own) with the
    // hands (the offering icon) under it. When the belt brings the offering's place there, the big diamond
    // leaves with the hands — the belt's copy of it takes over on the same column, so nothing jumps — and
    // rides on into the circle: the offering has arrived when the diamond is centred on the circle, the
    // moment its cycle runs out. Once it is off the stand a small diamond comes in from behind the right
    // edge and holds the place until the offering is made; then the next offering's big diamond and the
    // hands stand there again.
    //
    // Between the belt and the stand, the dots: the way on to the offering waiting there. Just before it
    // leaves, the line comes out over them to meet it, at the belt's own speed — it starts when the
    // offering's place is as far from the stand as the dots are long, and reaches the diamond as it sets
    // off. A big diamond leaving the stand crosses them; they show only right of it, where it has passed,
    // never through it.
    //
    // Under the big diamond on the stand, three more dots, hidden while it stands: they show behind it as
    // it leaves, until the small diamond comes in over them.
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
    // The moving pieces are made here — the ticks and the line from the three sprites, the big diamonds
    // on their way from the stand's, the numbers from a template label — and reused for good; the prefab
    // holds only the fixed parts.
    public class TimePanel : MonoBehaviour, IUIElement, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Parts")]
        [Tooltip("The circle — NOW. An offering has arrived when its diamond is centred on it.")]
        [SerializeField] private RectTransform now;

        [Tooltip("Where the line, the year ticks and the offering ticks' line ends are made. Its RectMask2D " +
                 "runs from the circle's edge to the dots; its right edge is moved over them as the line comes " +
                 "out to meet an offering about to leave the stand, and behind a big diamond leaving it.")]
        [SerializeField] private RectTransform belt;

        [Tooltip("Where the offering ticks' big diamonds and the small diamond on the stand are made. Its clip " +
                 "reaches from inside the circle to the right end, so the small diamond comes in from behind it.")]
        [SerializeField] private RectTransform riders;

        [Tooltip("The offering icon. Where it stands in the prefab is where it waits — under the big diamond, " +
                 "centred on it — and it leaves with the big diamond.")]
        [SerializeField] private RectTransform hands;

        [Tooltip("The big diamond on the stand, where the offering waits. Hidden while the offering is on its " +
                 "way — the belt carries a copy of it then — until the next offering stands there.")]
        [SerializeField] private Image bigDiamond;

        [Tooltip("The dots between the belt and the stand. Made Filled here: they show only right of the line " +
                 "coming out over them, and right of a big diamond leaving the stand across them.")]
        [SerializeField] private Image dots;

        [Tooltip("Three more dots under the big diamond on the stand, hidden while it stands. Made Filled here: " +
                 "they show behind it as it leaves, until the small diamond comes in over them. Optional.")]
        [SerializeField] private Image standDots;

        [Header("Art")]
        [SerializeField] private Sprite lineSprite;
        [SerializeField] private Sprite yearSprite;

        [Tooltip("The offering tick: the small diamond with a piece of line either side. On the belt its line " +
                 "ends go round the big diamond; the small diamond is the one that holds the stand.")]
        [SerializeField] private Sprite offeringSprite;

        [Header("Layout")]
        [Tooltip("One segment — one year — in pixels. Fixed: the belt moves this many pixels per Segment " +
                 "Seconds of the Offering Schedule.")]
        [SerializeField, Min(1)] private int segmentWidth = 58;

        [Tooltip("Width of the small diamond in the middle of the offering tick, in pixels: the one that holds " +
                 "the stand. What is either side of it — the gaps and the line ends — goes on the belt.")]
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

        // Measured once, in this panel's own space: the circle's centre, where the belt starts and ends, the
        // strip's edges, where the hands wait, the stand's left edge, the edges of both dots, the gap the dots
        // keep from the stand, where the numbers' parent starts.
        private RectTransform panel;
        private float nowX, beltLeft, beltRight, stripLeft, stripRight, handsHomeX, standLeft, dotsLeft, dotsRight,
                      standDotsLeft, standDotsRight, standGap, yearsLeft, yearY;
        private Vector2 handsHome;
        private bool ready;

        // The belt's own right offset as the prefab has it — its right edge at the dots.
        private float beltOffsetRight;

        // Screen pixels per canvas unit live from here — the CanvasScaler changes it with the window.
        private Canvas rootCanvas;

        // The offering tick cut in three at Start: what is left of the diamond, the diamond, what is right of it.
        private Sprite offeringLeft, offeringDiamond, offeringRight;

        private readonly List<Image> lines = new();
        private readonly List<Image> ticks = new();
        private readonly List<Image> leftEnds = new();
        private readonly List<Image> rightEnds = new();
        private readonly List<Image> diamonds = new();
        private readonly List<Image> stand = new();   // the small diamond holding the stand: a pool of one
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
        private float shownMoved = -1f;

        // From UIRoot (UIServices).
        public void Initialize(UIServices ui) => offerings = ui.OfferingSystem;

        private void Start()
        {
            if (now == null || belt == null || riders == null || hands == null || bigDiamond == null ||
                dots == null || lineSprite == null || yearSprite == null || offeringSprite == null ||
                bigDiamond.sprite == null)
            {
                Debug.LogWarning("[TimePanel] a part or a sprite is not assigned — the time strip stays empty.",
                                 this);
                return;
            }

            if (!CutOffering()) return;

            panel = (RectTransform)transform;
            nowX = PanelX(now, now.rect.center.x);
            beltLeft = PanelX(belt, belt.rect.xMin);
            beltRight = PanelX(belt, belt.rect.xMax);
            beltOffsetRight = belt.offsetMax.x;
            stripLeft = PanelX(riders, riders.rect.xMin);
            stripRight = PanelX(riders, riders.rect.xMax);
            handsHome = hands.anchoredPosition;
            handsHomeX = PanelX(hands, hands.rect.center.x);
            standLeft = PanelX(bigDiamond.rectTransform, bigDiamond.rectTransform.rect.xMin);
            dotsLeft = PanelX(dots.rectTransform, dots.rectTransform.rect.xMin);
            dotsRight = PanelX(dots.rectTransform, dots.rectTransform.rect.xMax);
            standGap = Mathf.Max(0f, standLeft - dotsRight);

            // Filled from the right: only the part right of the line or of a big diamond over them shows.
            dots.type = Image.Type.Filled;
            dots.fillMethod = Image.FillMethod.Horizontal;
            dots.fillOrigin = (int)Image.OriginHorizontal.Right;

            // Filled too, from whichever side is uncovered (LayoutStandDots).
            if (standDots != null)
            {
                standDotsLeft = PanelX(standDots.rectTransform, standDots.rectTransform.rect.xMin);
                standDotsRight = PanelX(standDots.rectTransform, standDots.rectTransform.rect.xMax);
                standDots.type = Image.Type.Filled;
                standDots.fillMethod = Image.FillMethod.Horizontal;
            }

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

            // Time's up: the belt has moved the whole cycle, to the pixel — the offering's diamond sits right in
            // the circle while the end-of-cycle window is up, whatever the screen-pixel steps would round to.
            float moved = active && offerings.IsOver ? Segments(cycle) * segmentWidth : steps / scale;
            if (cycle == shownCycle && moved == shownMoved) return;

            shownCycle = cycle;
            shownMoved = moved;
            Layout(cycle, moved);
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

            // The offering waiting on the stand: the first one the belt hasn't brought there yet. The one before
            // it is the last to have left — or tick 0, where this cycle started, when none of this cycle's has:
            // far behind, it changes nothing on the stand.
            int goneTick = 0, waitingCycle = cycle;
            int waitingTick = Segments(cycle);   // 0 = no offering ahead
            while (waitingTick > 0 && nowX + waitingTick * segmentWidth - moved <= handsHomeX)
            {
                goneTick = waitingTick;
                waitingTick = NextOffering(ref waitingCycle, waitingTick);
            }

            // The line comes out over the dots to meet the waiting offering, as fast as the belt brings its place
            // to the stand: its end is short of the dots' right end by just as much as that place is short of
            // leaving, so the two meet as the diamond sets off. Short of the dots' length, it hasn't started.
            float lineEnd = beltRight;
            if (waitingTick > 0)
            {
                float toGo = nowX + waitingTick * segmentWidth - moved - handsHomeX;
                lineEnd = Mathf.Clamp(dotsRight - toGo, beltRight, dotsRight);
            }

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
                if (isOffering) PlaceOffering(ref endCount, ref diamondCount, left, centre, moved);
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

            // The hands wait under the big diamond and leave with it: from the moment this cycle's offering is
            // on its way, its diamond stands on their column, so they set off from where they are.
            float offeringStart = nowX + Segments(cycle) * segmentWidth;
            bool riding = cycle > 0 && offeringStart - moved <= handsHomeX;
            hands.anchoredPosition = riding
                ? handsHome + new Vector2(Mathf.Round(offeringStart - handsHomeX) - moved, 0f)
                : handsHome;

            LayoutStand(moved, riding, goneTick, lineEnd);
        }

        // An offering tick: its line ends on the belt, with the line (clipped where the line is); its big
        // diamond on the riders — once the belt has brought it to the stand, where it leaves from.
        private void PlaceOffering(ref int endCount, ref int diamondCount, float left, float centre, float moved)
        {
            float diamondLeft = left + (offeringLeft != null ? offeringLeft.rect.width : 0f);
            if (offeringLeft != null) Place(leftEnds, endCount, belt, offeringLeft, left - beltLeft);
            if (offeringRight != null)
                Place(rightEnds, endCount, belt, offeringRight, diamondLeft + offeringDiamond.rect.width - beltLeft);
            endCount++;

            if (centre - moved <= handsHomeX)
                Place(diamonds, diamondCount++, riders, bigDiamond.sprite, BigLeft(centre, moved) - stripLeft);
        }

        // The stand and the dots: `goneTick` is the offering that left the stand last, `lineEnd` how far the
        // line has come out over the dots (both from Layout).
        private void LayoutStand(float moved, bool riding, int goneTick, float lineEnd)
        {
            float goneLeft = BigLeft(nowX + goneTick * segmentWidth, moved);
            float goneRight = goneLeft + bigDiamond.sprite.rect.width;

            // The belt ends at the dots — over them as far as the line has come out, and at the left tip of a
            // big diamond leaving over them, so its line end goes with it.
            float end = Mathf.Max(Mathf.Max(beltRight, goneLeft), lineEnd);
            float offsetRight = beltOffsetRight + end - beltRight;
            if (belt.offsetMax.x != offsetRight) belt.offsetMax = new Vector2(offsetRight, belt.offsetMax.y);

            // The dots show only right of the line and of a big diamond leaving over them: nothing of them
            // through it, nor left of it, where its line is.
            float shown = Mathf.Max(Mathf.Max(dotsLeft, goneRight), lineEnd);
            dots.fillAmount = (dotsRight - shown) / (dotsRight - dotsLeft);

            // On the stand: the big diamond until this cycle's offering leaves, then the small one — in from
            // behind the right edge once the big one is off the stand, at the belt's speed.
            bigDiamond.enabled = !riding;
            float smallLeft = stripRight;
            if (riding)
            {
                float home = Mathf.Round(handsHomeX - offeringDiamond.rect.width / 2f);
                smallLeft = Mathf.Max(home, stripRight + goneRight - standLeft);
                Place(stand, 0, riders, offeringDiamond, smallLeft - stripLeft);
            }
            HideFrom(stand, riding ? 1 : 0);

            if (standDots != null) LayoutStandDots(riding, goneRight, smallLeft);
        }

        // The dots under the stand show between the big diamond leaving and the small one coming in — none
        // while the big one stands on them — and stop short of the small one by the gap the dots keep from
        // the big one on the stand. The small one follows the big one a fixed distance behind (the right
        // edge's from the stand's left: the big one's width), so dots narrower than what is left between them
        // are cut on one side at a time: the big one's while it is over them, then the small one's.
        private void LayoutStandDots(bool riding, float goneRight, float smallLeft)
        {
            float from = Mathf.Max(standDotsLeft, goneRight);
            float to = Mathf.Min(standDotsRight, smallLeft - standGap);
            bool smallOver = to < standDotsRight;   // then what shows runs from their left end

            standDots.fillOrigin = (int)(smallOver ? Image.OriginHorizontal.Left : Image.OriginHorizontal.Right);
            standDots.fillAmount = !riding || to <= from ? 0f
                : (smallOver ? to - standDotsLeft : standDotsRight - from) / (standDotsRight - standDotsLeft);
        }

        // Where a big diamond centred on `centre` has its left edge, the belt `moved` along — whole pixels, as
        // the belt lays out its ticks.
        private float BigLeft(float centre, float moved) =>
            Mathf.Round(centre - bigDiamond.sprite.rect.width / 2f) - moved;

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

        // x of a point of `rt` (in rt's own space) from this panel's left edge, snapped to the half pixel: the
        // strip is laid out in whole pixels from that edge, so edges come out whole and centres of odd sizes on
        // the half. From the edge, not the pivot: a centred pivot on an odd width sits between pixels, and
        // everything rounded from it would land half a pixel off. The edge itself must be on a whole pixel —
        // where the panel is placed (an odd width centred needs a half-pixel anchored position).
        private float PanelX(RectTransform rt, float localX)
        {
            float x = panel.InverseTransformPoint(rt.TransformPoint(new Vector3(localX, 0f, 0f))).x - panel.rect.xMin;
            return Mathf.Round(x * 2f) / 2f;
        }
    }
}
