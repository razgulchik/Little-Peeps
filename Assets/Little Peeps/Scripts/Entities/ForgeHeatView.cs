using UnityEngine;

namespace LittlePeeps
{
    // The forge's heat bar, drawn in the window of the building. Presentation only: reads ForgeHeat
    // every frame and never writes to it, so removing this component leaves the mechanic untouched.
    // Sits on the bar's own object under the forge and finds ForgeHeat up the hierarchy.
    //
    // Layers, back to front, all authored in the prefab: a black Back filling the window, the gradient
    // Fill at its FULL length (the art carries the colour — yellow at the start, red at the end), a
    // black Cover the same length on top of it, and the Model with the window cut out. The view drives
    // ONE thing: the cover's size. As heat rises the cover shrinks from the start edge toward the end,
    // uncovering the gradient in place — the fill itself never moves, stretches or changes sprite, so
    // the colours stay exactly where the artist put them. Covered gradient and empty window are the
    // same black, which is why a cold forge needs no hiding: the cover simply spans the whole bar.
    //
    // Every renderer the view drives is authored in the COLD state: cover on at full length, fill and
    // overlays off. The build-mode ghost is this same prefab with every behaviour disabled, drawn at
    // 60% alpha, so whatever is authored on is what it shows, and what it shows must be a cold forge —
    // an enabled fill would bleed through the translucent cover. The view flips the switches from there.
    //
    // The cover is a Sliced (or Tiled) sprite of one flat colour, so setting `size` is safe — there is
    // no picture to distort. Its authored size along `fillAxis` is the full bar, and its PIVOT must sit
    // on the END edge (right for Horizontal, top for Vertical): a sliced sprite grows around its pivot,
    // and pinning the end edge there is what keeps the uncovered part growing from the start with the
    // transform never touched. The view checks the pivot and says what to set. Sliding the transform
    // to compensate a centred pivot was tried on an earlier version and jittered under the Pixel
    // Perfect Camera — half a pixel per step snaps to a different row every other step.
    //
    // Sizes are whole sprite pixels, and any heat at all uncovers at least one, so a warm forge never
    // reads as cold.
    //
    // Overheat shows in two more ways, both colour only. The fill is darkened by overheatedTint — the
    // bar is full then either way, and the darkening is what says "locked". And the overheat overlays
    // (a red glow over the door, fire over the chimney — any number of sprites the art lays over the
    // model) light up at full alpha the moment the forge trips, then fade as it cools: their alpha
    // follows the HEAT, not a clock, so they die out exactly when the forge unlocks, and a perk that
    // changes the cooling time keeps them honest for free. overlayAlpha shapes that fade.
    public class ForgeHeatView : MonoBehaviour
    {
        public enum FillAxis { Horizontal, Vertical }

        [Tooltip("Horizontal uncovers from the left edge to the right; Vertical from the bottom up.")]
        [SerializeField] private FillAxis fillAxis = FillAxis.Horizontal;

        [Tooltip("Flat-colour Sliced/Tiled sprite laid over the whole fill, pivot on the END edge " +
                 "(Right / Top). Its size along Fill Axis is driven.")]
        [SerializeField] private SpriteRenderer cover;

        [Tooltip("The gradient bar under the cover. Author its renderer DISABLED — the view turns it on " +
                 "once any of it is uncovered, so the build-mode ghost shows only the cover.")]
        [SerializeField] private SpriteRenderer fill;

        [Tooltip("Multiplied into the fill's colour while overheated. White = no change.")]
        [SerializeField] private Color overheatedTint = new(0.45f, 0.45f, 0.45f);

        [Header("Overheat overlays")]
        [Tooltip("Sprites shown only while overheated (door glow, chimney fire). Author them disabled; " +
                 "the view switches them on and fades them with the heat.")]
        [SerializeField] private SpriteRenderer[] overheatOverlays;

        [Tooltip("Overlay alpha against heat while overheated: x = heat 0..1, y = alpha. Default is " +
                 "linear — full at the moment of overheat, gone when the forge has cooled to zero.")]
        [SerializeField] private AnimationCurve overlayAlpha = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Overheat smoke")]
        [Tooltip("Looping ParticleSystem at the chimney with Play On Awake OFF. Played the moment the forge " +
                 "overheats, told to stop emitting when it unlocks (puffs in flight finish on their own), " +
                 "and its emission rate is scaled by Smoke Rate in between.")]
        [SerializeField] private ParticleSystem smoke;

        [Tooltip("Emission rate against heat while overheated: x = heat 0..1, y = share of the authored " +
                 "rate. Default is linear — thick at the moment of overheat, thinning to nothing as it cools.")]
        [SerializeField] private AnimationCurve smokeRate = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Sparks")]
        [Tooltip("Looping ParticleSystem at the anvil with Rate over Time 0 and Play On Awake OFF. Fed by " +
                 "hand: a burst of Sparks Per Hit on every hit that pays metal. Separate from the pickup " +
                 "effect on purpose — the ingot leaves the door, the sparks leave the anvil.")]
        [SerializeField] private ParticleSystem sparks;

        [Tooltip("Particles per paying hit.")]
        [Min(1)] [SerializeField] private int sparksPerHit = 6;

        private ForgeHeat heat;
        private float pixelsPerUnit;
        private int fullPixels;          // the cover's authored length = the bar at 100%
        private int coveredPixels = -1;  // last applied, so the renderer is only touched on change
        private Color fillColor;         // the fill's authored colour, restored after an overheat
        private Color[] overlayColors;   // each overlay's authored colour; the fade scales its alpha
        private float smokeBaseRate;     // the smoke's authored rate over time; the curve scales it
        private bool smoking;            // whether the smoke was last told to play, so Play/Stop fire once

        private void Awake()
        {
            heat = GetComponentInParent<ForgeHeat>();
            if (heat == null)
                Debug.LogError($"ForgeHeatView on '{name}' has no ForgeHeat above it in the hierarchy.", this);

            if (fill != null) fillColor = fill.color;

            overheatOverlays ??= new SpriteRenderer[0];
            overlayColors = new Color[overheatOverlays.Length];
            for (int i = 0; i < overheatOverlays.Length; i++)
                if (overheatOverlays[i] != null) overlayColors[i] = overheatOverlays[i].color;

            if (smoke != null) PrepareSmoke();
            if (sparks != null) PrepareSparks();

            if (cover == null)
            {
                Debug.LogError($"ForgeHeatView on '{name}' has no cover renderer assigned.", this);
                return;
            }
            if (cover.drawMode == SpriteDrawMode.Simple)
                Debug.LogError($"ForgeHeatView on '{name}': the cover's Draw Mode must be Sliced or Tiled — " +
                               "size is ignored in Simple.", this);

            var sprite = cover.sprite;
            if (sprite == null)
            {
                Debug.LogError($"ForgeHeatView on '{name}': the cover renderer has no sprite.", this);
                return;
            }

            bool horizontal = fillAxis == FillAxis.Horizontal;
            pixelsPerUnit = sprite.pixelsPerUnit;
            fullPixels = Mathf.RoundToInt((horizontal ? cover.size.x : cover.size.y) * pixelsPerUnit);

            // Pivot share along the axis, 1 = end edge. Anything else and the cover would shrink around
            // its pivot; say so now, with the fix, rather than let it look almost right.
            float pivotShare = horizontal ? sprite.pivot.x / sprite.rect.width : sprite.pivot.y / sprite.rect.height;
            if (pivotShare < 0.999f)
                Debug.LogError($"ForgeHeatView on '{name}': the cover sprite '{sprite.name}' has its pivot at " +
                               $"{pivotShare:0.##} along the {fillAxis} axis; set it to {(horizontal ? "Right" : "Top")} " +
                               "in the sprite import settings so the bar uncovers from the other edge.", this);
        }

        private void Update()
        {
            if (heat == null || cover == null || cover.sprite == null) return;

            float t = heat.Heat01;
            int uncovered = Mathf.RoundToInt(t * fullPixels);
            if (t > 0f) uncovered = Mathf.Max(1, uncovered);
            ApplyCover(fullPixels - uncovered);

            bool overheated = heat.IsOverheated;
            ApplyTint(overheated);
            ApplyOverlays(overheated, t);
            ApplySmoke(overheated, t);
        }

        // The smoke is part of the cold-state rule like every renderer here: authored silent, started by
        // the view. A system left playing in the prefab would also play on the build-mode ghost — a
        // ParticleSystem is not a MonoBehaviour, so the ghost does not switch it off — so it is both
        // reported and silenced here rather than left to the first overheat to sort out. Non-looping
        // would stop itself after its Duration, long before a 20-second cool-down is over.
        private void PrepareSmoke()
        {
            var main = smoke.main;
            if (main.playOnAwake)
                Debug.LogError($"ForgeHeatView on '{name}': the smoke '{smoke.name}' has Play On Awake on; " +
                               "turn it off — the view starts it on overheat, and the ghost must not smoke.", smoke);
            if (!main.loop)
                Debug.LogError($"ForgeHeatView on '{name}': the smoke '{smoke.name}' must be Looping — a " +
                               "one-shot system stops itself after its Duration, mid cool-down.", smoke);

            smokeBaseRate = smoke.emission.rateOverTimeMultiplier;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Same shape as HarvestVfxSystem's emitters: a looping system with no rate of its own, started
        // once and fed by Emit. Started here, not by Play On Awake, so the ghost never runs it — with a
        // zero rate that would be harmless, but the cold-state rule is easier to keep when it has no
        // exceptions. Non-looping would stop itself after its Duration and swallow every Emit after.
        private void PrepareSparks()
        {
            if (!sparks.main.loop)
                Debug.LogError($"ForgeHeatView on '{name}': the sparks '{sparks.name}' must be Looping — a " +
                               "one-shot system stops after its Duration and ignores Emit from then on.", sparks);
            sparks.Play();
        }

        private void OnEnable()
        {
            if (heat != null) heat.HitPaid += OnHitPaid;
        }

        private void OnDisable()
        {
            if (heat != null) heat.HitPaid -= OnHitPaid;
        }

        private void OnHitPaid()
        {
            if (sparks == null) return;
            if (!sparks.isPlaying) sparks.Play();   // a Stop from elsewhere must not mute the anvil
            sparks.Emit(sparksPerHit);
        }

        private void ApplyCover(int pixels)
        {
            if (pixels == coveredPixels) return;
            coveredPixels = pixels;

            // The fill is authored OFF and the cover ON at full length — the cold state — so the
            // build-mode ghost, which runs none of this, shows a black window and nothing under it.
            // The ghost is drawn at 60% alpha, so anything enabled under the cover would bleed through.
            // The view owns both switches from here: the fill exists as soon as a pixel of it shows.
            if (fill != null) fill.enabled = pixels < fullPixels;

            // A zero-length sliced sprite has nothing to draw; switching the renderer off is the
            // honest way to say so rather than leaning on how Unity handles a degenerate size.
            cover.enabled = pixels > 0;
            if (pixels == 0) return;

            var size = cover.size;
            if (fillAxis == FillAxis.Horizontal) size.x = pixels / pixelsPerUnit;
            else size.y = pixels / pixelsPerUnit;
            cover.size = size;
        }

        // Written every frame, unlike the cover: PlacementVisuals captures and restores every renderer's
        // colour around a build-mode hover, and a restore landing mid-overheat would otherwise put the
        // untinted colour back and leave it there. The fill simply ignores the hover tint instead.
        private void ApplyTint(bool overheated)
        {
            if (fill == null) return;
            fill.color = overheated ? fillColor * overheatedTint : fillColor;
        }

        // Play and Stop fire once per transition; the rate is written every frame in between. Stop with
        // StopEmitting, not Clear: the last puffs drift off on their own, and the system stays alive
        // for the next overheat.
        private void ApplySmoke(bool overheated, float heat01)
        {
            if (smoke == null) return;

            if (overheated != smoking)
            {
                smoking = overheated;
                if (overheated) smoke.Play();
                else smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!overheated) return;

            var emission = smoke.emission;
            emission.rateOverTimeMultiplier = smokeBaseRate * Mathf.Max(0f, smokeRate.Evaluate(heat01));
        }

        // Off outright when not overheated — not alpha 0, so a hidden overlay costs nothing to draw and
        // the ghost, which never runs this, shows exactly what the prefab authored. While on, the alpha
        // is written every frame for the same reason the fill tint is.
        private void ApplyOverlays(bool overheated, float heat01)
        {
            float alpha = overheated ? Mathf.Clamp01(overlayAlpha.Evaluate(heat01)) : 0f;
            for (int i = 0; i < overheatOverlays.Length; i++)
            {
                var overlay = overheatOverlays[i];
                if (overlay == null) continue;

                overlay.enabled = overheated;
                if (!overheated) continue;

                var color = overlayColors[i];
                color.a *= alpha;
                overlay.color = color;
            }
        }
    }
}
