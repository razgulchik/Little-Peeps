using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The FEEL of the island rising out of the sea when an age grows it: when each tile of the new zone
    // moves (`timing`, which IslandRiseSchedule turns into start times) and how it looks while it does
    // (the curves below). The game and the tuning window both run this one asset, so what the window
    // shows is what the game plays — the arrangement HarvestNumberMotionDef has, for the same reasons:
    // the tuning lives outside SampleScene, survives leaving Play Mode, and is read live.
    //
    // Nothing here moves anything. It answers "what does a tile, or a pop, look like at this point of its
    // life", and the player applies the answer to its sprites. A tile's life, in phases:
    //   under water — small, dark and faint in its own cell, growing and clearing as it nears the surface;
    //   hop         — breaks the surface, jumps a little and springs in size;
    //   squash      — touches down and flattens for a moment;
    //   settled     — a real tilemap tile.
    // Wetness runs across the last three: the tile comes out tinted and dries back to white. No shader —
    // every look is SpriteRenderer.color and scale. Around a tile's life: its standing neighbours bounce as
    // it touches down, and at the finale the whole new zone breathes.
    [CreateAssetMenu(menuName = "LittlePeeps/Island Rise Profile")]
    public class IslandRiseProfile : ScriptableObject
    {
        public IslandRiseTiming timing = new();

        [Header("Under water")]
        [Tooltip("Size across the rise from the depth: 0 = starts rising, 1 = breaks the surface.")]
        public AnimationCurve depthScale = new(new Keyframe(0f, 0.5f, 0f, 0f), new Keyframe(1f, 0.95f, 0.9f, 0.9f));

        [Tooltip("SpriteRenderer colour across the rise from the depth, alpha included: dark and faint deep " +
                 "down, clearer near the surface.")]
        public Gradient depthTint = TwoKeyGradient(new Color(0.22f, 0.38f, 0.46f, 0.2f), new Color(0.55f, 0.68f, 0.75f, 0.75f));

        [Header("Hop")]
        [Tooltip("Height of the hop, in world units (1 = one cell).")]
        public float hopHeight = 0.38f;

        [Tooltip("Share of the hop height across the hop: 0 = breaks the surface, 1 = touches down.")]
        public AnimationCurve hopLift = Bump();

        [Tooltip("Size across the hop. Rising past 1 and settling back reads as a spring.")]
        public AnimationCurve hopScale = new(new Keyframe(0f, 0.85f, 0f, 0.78f), new Keyframe(0.54f, 1.023f, 0f, 0f),
                                             new Keyframe(1f, 1f, 0f, 0f));

        [Header("Squash")]
        [Tooltip("Stretch at the height of the squash, added to a scale of 1: x goes wider, y lower.")]
        public Vector2 squashAmount = new(0.08f, -0.14f);

        [Tooltip("Share of the squash across it: 0 = touches down, 1 = settled.")]
        public AnimationCurve squashCurve = Bump();

        [Header("Wet")]
        [Tooltip("Colour a tile comes out of the water with. It dries back to white.")]
        public Color wetTint = new(0.55f, 0.62f, 0.7f, 1f);

        [Tooltip("Seconds from breaking the surface to dry. Longer than the hop and squash together = the " +
                 "tile is still drying after it has become a real tile.")]
        [Min(0f)] public float dryTime = 0.9f;

        [Header("Neighbours bounce")]
        [Tooltip("How high a tile already standing jumps when a new one touches down next to it, in world " +
                 "units (1/16 = one art pixel). 0 = no bounce.")]
        [Min(0f)] public float bounceHeight = 0.125f;

        [Tooltip("Seconds one bounce lasts.")]
        [Min(0.01f)] public float bounceTime = 0.25f;

        [Tooltip("Share of the bounce height across the bounce.")]
        public AnimationCurve bounceCurve = Bump();

        [Tooltip("Off: the four side neighbours bounce. On: the diagonal ones too.")]
        public bool bounceDiagonals;

        [Header("Finale breath")]
        [Tooltip("How high the new zone lifts as the breath runs across it, in world units; its structures " +
                 "ride along. 0 = no breath. Its timing is in Timing > Finale.")]
        [Min(0f)] public float breathHeight = 0.125f;

        [Tooltip("Share of the breath height across one cell's breath.")]
        public AnimationCurve breathCurve = Bump();

        [Header("Pixel snap")]
        [Tooltip("Every lift, hop, bounce and breath moves in whole art pixels, crisp like the art. Off = " +
                 "smooth motion between pixels. Sizes are never snapped.")]
        public bool snapToPixels = true;

        [Tooltip("Art pixels per world unit (the Pixel Perfect Camera's Assets PPU).")]
        [Min(1)] public int pixelsPerUnit = 16;

        [Header("Content pop (act 2)")]
        [Tooltip("Seconds a structure takes to pop up, unless it has an override below.")]
        [Min(0f)] public float popDuration = 0.35f;

        [Tooltip("Scale across the pop: 0 = starts, 1 = done. Past 1 and back reads as a spring.")]
        public AnimationCurve popScale = PopSpring();

        [Tooltip("A pop of their own for particular structures — a mountain grows slower than a bush. " +
                 "Anything not listed uses the pop above.")]
        public List<IslandRisePopOverride> popOverrides = new();

        [Header("Water")]
        [Tooltip("A tile in the air pushes the water the way land does, from breaking the surface until " +
                 "it settles (the package's Obstructor on the stand-in).")]
        public bool obstructOnProxy = true;

        [Header("Effects")]
        [Tooltip("Bubbles over a tile's cell until it breaks the surface. Every effect here is a " +
                 "ParticleSystem prefab run as one shared emitter: Simulation Space = World, Looping on, " +
                 "Rate over Time 0 — the rise moves it and emits by hand. Empty = none.")]
        public ParticleSystem bubblesFx;
        [Tooltip("Bubbles per second over each cell whose tile is still under the water.")]
        [Min(0f)] public float bubblesPerSecond = 4f;

        [Tooltip("Splash where a tile breaks the surface.")]
        public ParticleSystem splashFx;
        [Min(0)] public int splashCount = 6;

        [Tooltip("Where a tile touches down: a ring of foam, a puff of spray.")]
        public ParticleSystem touchDownFx;
        [Min(0)] public int touchDownCount = 4;

        [Tooltip("Dust where a structure pops up.")]
        public ParticleSystem popFx;
        [Min(0)] public int popCount = 5;

        [Tooltip("Sparkles over the whole zone at the finale.")]
        public ParticleSystem finaleFx;
        [Tooltip("Sparkles per cell of the zone.")]
        [Min(0)] public int finaleCountPerCell = 1;

        [Header("Sound")]
        [Tooltip("Played as each tile breaks the surface, one step up a pentatonic scale each time: the " +
                 "first tile on the lowest note, the last on the highest. Empty = silent.")]
        public AudioClip breachClip;
        [Range(0f, 1f)] public float breachVolume = 0.6f;

        [Tooltip("Pitch of the first note; 1 = the clip as recorded.")]
        [Range(0.25f, 2f)] public float basePitch = 0.5f;

        [Tooltip("Steps of the pentatonic scale the whole zone climbs, first tile to last. 5 steps = one " +
                 "octave.")]
        [Range(0, 15)] public int noteSteps = 10;

        [Tooltip("Least seconds between two notes. Closer ones are dropped, so a fast wave (or the tap's " +
                 "speed-up) rattles instead of smearing into noise.")]
        [Min(0f)] public float minNoteGap = 0.03f;

        [Tooltip("Played as a structure pops up.")]
        public AudioClip popClip;
        [Range(0f, 1f)] public float popVolume = 0.5f;

        [Tooltip("Played at the finale.")]
        public AudioClip finaleClip;
        [Range(0f, 1f)] public float finaleVolume = 0.8f;

        [Header("Shake")]
        [Tooltip("Camera kick as each tile breaks the surface, in world units: 1/16 = one art pixel, and less " +
                 "may not show under the pixel-perfect camera. Needs a CinemachineImpulseListener on the " +
                 "virtual camera. 0 = no shake.")]
        [Min(0f)] public float shakePerBreach = 0.0625f;

        [Tooltip("Most kick at once: a rush of tiles adds up to this and no further.")]
        [Min(0f)] public float shakeCap = 0.2f;

        [Tooltip("Seconds one kick lasts.")]
        [Min(0.01f)] public float shakeDuration = 0.2f;

        [Header("Tap")]
        [Tooltip("How fast the rest of the rise plays after the first tap.")]
        [Min(1f)] public float tapSpeedUp = 3f;

        // Semitones of the major pentatonic scale within one octave.
        private static readonly int[] Pentatonic = { 0, 2, 4, 7, 9 };

        // Pitch of the note for the `note`-th tile to break the surface out of `noteCount`: spread evenly
        // over `steps` steps of the pentatonic scale, starting at `basePitch`. Static and plain so it can
        // be tested without an asset.
        public static float NotePitch(int note, int noteCount, int steps, float basePitch)
        {
            int step = noteCount > 1 ? Mathf.RoundToInt((float)note / (noteCount - 1) * Mathf.Max(0, steps)) : 0;
            int semitones = Pentatonic[step % 5] + 12 * (step / 5);
            return basePitch * Mathf.Pow(2f, semitones / 12f);
        }

        public IslandRiseTilePose EvaluateTile(IslandRiseTileState state)
        {
            float p = state.progress;
            switch (state.phase)
            {
                case IslandRisePhase.Waiting:
                    return default;

                case IslandRisePhase.Underwater:
                {
                    float size = depthScale.Evaluate(p);
                    return new IslandRiseTilePose
                    {
                        visible = true, underwater = true, scale = new Vector2(size, size), tint = depthTint.Evaluate(p)
                    };
                }

                case IslandRisePhase.Hop:
                {
                    float size = hopScale.Evaluate(p);
                    return new IslandRiseTilePose
                    {
                        visible = true, lift = hopHeight * hopLift.Evaluate(p), scale = new Vector2(size, size),
                        tint = WetTint(state.sinceBreach)
                    };
                }

                case IslandRisePhase.Squash:
                    return new IslandRiseTilePose
                    {
                        visible = true, scale = Vector2.one + squashAmount * squashCurve.Evaluate(p),
                        tint = WetTint(state.sinceBreach)
                    };

                default:
                    return new IslandRiseTilePose
                    {
                        visible = true, settled = true, scale = Vector2.one, tint = WetTint(state.sinceBreach)
                    };
            }
        }

        // A lift in world units, in whole art pixels when snapping.
        public float Snap(float worldUnits) =>
            snapToPixels ? Mathf.Round(worldUnits * pixelsPerUnit) / pixelsPerUnit : worldUnits;

        public Vector3 Snap(Vector3 worldUnits) => new(Snap(worldUnits.x), Snap(worldUnits.y), worldUnits.z);

        // How far a standing tile is lifted `since` seconds into a bounce.
        public float BounceLift(float since) =>
            since >= 0f && since < bounceTime ? bounceHeight * bounceCurve.Evaluate(since / bounceTime) : 0f;

        public float PopDurationOf(StructureDef def)
        {
            var own = OverrideFor(def);
            return own != null ? own.duration : popDuration;
        }

        // Scale of a structure's root at `progress` through its pop (0..1).
        public float PopScaleOf(StructureDef def, float progress)
        {
            var own = OverrideFor(def);
            return (own != null ? own.scale : popScale).Evaluate(progress);
        }

        private Color WetTint(float sinceBreach)
        {
            float wet = dryTime > 0f ? 1f - Mathf.Clamp01(sinceBreach / dryTime) : 0f;
            return Color.Lerp(Color.white, wetTint, wet);
        }

        private IslandRisePopOverride OverrideFor(StructureDef def)
        {
            if (def == null) return null;
            foreach (var own in popOverrides)
                if (own != null && own.def == def) return own;
            return null;
        }

        // 0 → 1 → 0, shaped like half a sine.
        private static AnimationCurve Bump() =>
            new(new Keyframe(0f, 0f, 0f, Mathf.PI), new Keyframe(0.5f, 1f, 0f, 0f), new Keyframe(1f, 0f, -Mathf.PI, 0f));

        // 0 → overshoot → 1.
        internal static AnimationCurve PopSpring() =>
            new(new Keyframe(0f, 0f, 0f, 5.4f), new Keyframe(0.53f, 1.18f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));

        private static Gradient TwoKeyGradient(Color from, Color to)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(from.a, 0f), new GradientAlphaKey(to.a, 1f) });
            return gradient;
        }
    }

    [Serializable]
    public class IslandRisePopOverride
    {
        public StructureDef def;
        [Min(0f)] public float duration = 0.6f;
        public AnimationCurve scale = IslandRiseProfile.PopSpring();
    }

    // How one tile looks at one moment of the rise. The player lays it on the tile's stand-in sprite, or,
    // once settled, on the tilemap cell (only the tint is left to show by then).
    public struct IslandRiseTilePose
    {
        public bool visible;       // false before the tile starts rising: only water there
        public bool underwater;    // under the water surface: the depth phase
        public bool settled;       // the real tile has taken over from the stand-in
        public float lift;         // world units above its cell
        public Vector2 scale;
        public Color tint;         // SpriteRenderer.color
    }
}
