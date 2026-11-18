using UnityEngine;

namespace LittlePeeps
{
    // The sawmill at work, drawn. Presentation only: reads Sawmill (Awaited, Cut) and never writes to it, so
    // removing this component leaves the mechanic untouched. Sits on the root next to Sawmill, like every
    // view; the renderers it drives are wired in.
    //
    // What it shows:
    //   the blade  — always heading for the end the sawmill waits for (the doc's "the carriage moves to the
    //                end that must be struck next"), at a steady speed, spinning through its frames —
    //                faster while it travels than while it stands;
    //   a cut      — every paid cut sends the blade across the log; when it reaches the far end the log
    //                comes apart: the whole log goes, its two halves take its place, the back one rolls off
    //                backwards and the front one forwards, both fading out;
    //   a new log  — a moment later the next log drops onto the bench from a few pixels up.
    // A cut that comes while the bench is empty (the next log not down yet) puts the log down at once, so
    // the blade always has something to saw. Two cuts in a row while the blade is still on its way turn it
    // round — it heads for the end the sawmill waits for — and the log splits once, when the blade arrives.
    // The pickup effect still shows every payout; this only keeps the picture honest about which end is next.
    //
    // Cold-state rule (see ForgeHeatView): everything is authored at rest — the blade at the LEFT end (where
    // a new sawmill waits), the log whole, the halves' renderers OFF — because the placement ghost is this
    // prefab with every behaviour disabled and shows exactly what is authored. The ends are where the blade
    // is authored and its mirror image across the bench's centre.
    //
    // Only the halves fade, and they are this view's own: the log never changes colour (it drops in instead
    // of fading in), so the sell-hover tint, which captures and restores every renderer's colour, can never be
    // put back half-faded on it. Every move is in whole art pixels, measured from the authored pose.
    // Game time: a paused game freezes the saw mid-cut.
    [RequireComponent(typeof(Sawmill))]
    public class SawmillView : MonoBehaviour
    {
        [Header("Blade")]
        [Tooltip("The saw blade, authored at the LEFT end. The right end is its mirror across the bench's centre.")]
        [SerializeField] private SpriteRenderer blade;

        [Tooltip("The blade's spin, in order. Cycled faster while it travels than while it stands.")]
        [SerializeField] private Sprite[] bladeFrames;

        [Tooltip("Seconds the blade takes from one end to the other.")]
        [SerializeField, Min(0.05f)] private float travelTime = 0.6f;

        [Tooltip("Spin frames per second while the blade travels.")]
        [SerializeField, Min(0f)] private float spinFps = 16f;

        [Tooltip("Spin frames per second while it stands at an end. 0 = it stops.")]
        [SerializeField, Min(0f)] private float idleSpinFps = 6f;

        [Header("Log")]
        [Tooltip("The whole log, its back and front parts — the blade runs between them.")]
        [SerializeField] private SpriteRenderer logBack;
        [SerializeField] private SpriteRenderer logFront;

        [Tooltip("Seconds between the split and the next log dropping in.")]
        [SerializeField, Min(0f)] private float newLogDelay = 0.3f;

        [Tooltip("Seconds the new log takes to drop onto the bench.")]
        [SerializeField, Min(0.01f)] private float dropTime = 0.2f;

        [Tooltip("Pixels above the bench the new log drops from.")]
        [SerializeField, Min(0)] private int dropPixels = 4;

        [Header("Halves")]
        [Tooltip("The halves the log splits into, authored where they lie the moment it comes apart, renderers OFF.")]
        [SerializeField] private SpriteRenderer halfBack;
        [SerializeField] private SpriteRenderer halfFront;

        [Tooltip("Seconds the halves roll off and fade.")]
        [SerializeField, Min(0.01f)] private float splitTime = 0.45f;

        [Tooltip("Pixels the back half rolls backwards (up the screen).")]
        [SerializeField, Min(0)] private int backRollPixels = 5;

        [Tooltip("Pixels the front half rolls forwards (down the screen).")]
        [SerializeField, Min(0)] private int frontRollPixels = 8;

        private enum Log { Whole, Gone, Dropping }

        private Sawmill sawmill;
        private float pixelsPerUnit = 16f;

        // Authored poses — every move is measured from these.
        private Vector3 bladeHome, logBackHome, logFrontHome, halfBackHome, halfFrontHome;
        private Color halfBackColor, halfFrontColor;
        private float leftX, rightX;

        private float bladeX;              // where the blade is, unsnapped
        private float spin;                // frames turned, fractional
        private bool sawing;               // a cut is paid and the blade has not reached its end yet
        private Log log = Log.Whole;
        private float logTimer;            // seconds into Gone / Dropping
        private float splitTimer = -1f;    // seconds into the halves' roll; < 0 = no halves out

        private void Awake()
        {
            sawmill = GetComponent<Sawmill>();
            bool wired = blade != null && logBack != null && logFront != null && halfBack != null && halfFront != null;
            if (!wired)
            {
                Debug.LogError($"SawmillView on '{name}' is missing a renderer (blade, log back/front, half back/front).", this);
                enabled = false;
                return;
            }

            if (blade.sprite != null) pixelsPerUnit = blade.sprite.pixelsPerUnit;
            bladeHome = blade.transform.localPosition;
            logBackHome = logBack.transform.localPosition;
            logFrontHome = logFront.transform.localPosition;
            halfBackHome = halfBack.transform.localPosition;
            halfFrontHome = halfFront.transform.localPosition;
            halfBackColor = halfBack.color;
            halfFrontColor = halfFront.color;

            leftX = bladeHome.x;
            rightX = -bladeHome.x;
            if (leftX >= 0f)
                Debug.LogError($"SawmillView on '{name}': author the blade at the LEFT end (x < 0) — a new sawmill " +
                               "waits for the left end, and the right end is its mirror.", this);
            bladeFrames ??= new Sprite[0];
        }

        private void OnEnable() { if (sawmill != null) sawmill.Cut += OnCut; }
        private void OnDisable() { if (sawmill != null) sawmill.Cut -= OnCut; }

        private void Start()
        {
            // Whatever end the sawmill waits for when it starts, the blade is already there.
            bladeX = EndX(sawmill.Awaited);
            halfBack.enabled = false;
            halfFront.enabled = false;
            Apply();
        }

        private void OnCut(Sawmill.End struck)
        {
            sawing = true;
            if (log != Log.Whole) PutLogDown();   // nothing on the bench yet: the blade needs a log to saw
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // The blade: toward the awaited end, at one steady speed.
            float target = EndX(sawmill.Awaited);
            float speed = (rightX - leftX) / travelTime;
            bool moving = !Mathf.Approximately(bladeX, target);
            bladeX = Mathf.MoveTowards(bladeX, target, speed * dt);
            if (sawing && Mathf.Approximately(bladeX, target))
            {
                sawing = false;
                Split();
            }
            spin += (moving ? spinFps : idleSpinFps) * dt;
            if (bladeFrames.Length > 0) spin %= bladeFrames.Length;

            // The log: gone after a split, then dropping in, then whole again.
            if (log != Log.Whole)
            {
                logTimer += dt;
                if (log == Log.Gone && logTimer >= newLogDelay) { log = Log.Dropping; logTimer = 0f; }
                if (log == Log.Dropping && logTimer >= dropTime) PutLogDown();
            }

            if (splitTimer >= 0f)
            {
                splitTimer += dt;
                if (splitTimer >= splitTime) splitTimer = -1f;
            }

            Apply();
        }

        // The log comes apart where it lies: the halves take its place and roll off.
        private void Split()
        {
            if (log != Log.Whole) return;
            log = Log.Gone;
            logTimer = 0f;
            splitTimer = 0f;
        }

        private void PutLogDown()
        {
            log = Log.Whole;
            logTimer = 0f;
        }

        private float EndX(Sawmill.End end) => end == Sawmill.End.Left ? leftX : rightX;

        // Everything drawn from the state — the only place a renderer is touched.
        private void Apply()
        {
            // Blade: whole pixels from its authored pose (an odd-width blade sits on a half pixel there).
            blade.transform.localPosition = bladeHome + new Vector3(Pixels(bladeX - leftX), 0f, 0f);
            if (bladeFrames.Length > 0)
                blade.sprite = bladeFrames[(int)spin % bladeFrames.Length];

            // Log: hidden while gone, dropping from dropPixels up, then resting where it was authored.
            bool logShown = log != Log.Gone;
            logBack.enabled = logShown;
            logFront.enabled = logShown;
            float lift = 0f;
            if (log == Log.Dropping)
            {
                float t = Mathf.Clamp01(logTimer / dropTime);
                lift = Pixels(dropPixels * (1f - t) * (1f - t) / pixelsPerUnit);   // eases into the bench
            }
            logBack.transform.localPosition = logBackHome + new Vector3(0f, lift, 0f);
            logFront.transform.localPosition = logFrontHome + new Vector3(0f, lift, 0f);

            // Halves: rolling apart and fading, written every frame while they are out.
            bool halvesOut = splitTimer >= 0f;
            halfBack.enabled = halvesOut;
            halfFront.enabled = halvesOut;
            if (!halvesOut) return;

            float s = Mathf.Clamp01(splitTimer / splitTime);
            float roll = s * s * (3f - 2f * s);
            halfBack.transform.localPosition = halfBackHome + new Vector3(0f, Pixels(backRollPixels * roll / pixelsPerUnit), 0f);
            halfFront.transform.localPosition = halfFrontHome - new Vector3(0f, Pixels(frontRollPixels * roll / pixelsPerUnit), 0f);
            halfBack.color = Faded(halfBackColor, 1f - s);
            halfFront.color = Faded(halfFrontColor, 1f - s);
        }

        // A length in world units, rounded to whole art pixels.
        private float Pixels(float units) => Mathf.Round(units * pixelsPerUnit) / pixelsPerUnit;

        private static Color Faded(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }
    }
}
