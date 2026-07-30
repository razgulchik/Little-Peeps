using TMPro;
using UnityEngine;

namespace LittlePeeps
{
    // The FEEL of a floating harvest number: how it travels, swells and fades across its life.
    // Split out of HarvestNumbers so exactly one thing owns the motion maths, and so the game and the
    // Edit Mode authoring tool can both drive it without either knowing the other exists. The tool
    // used to reach into HarvestNumbers through an editor-only back door; now both sides simply run
    // this asset, which is a stronger guarantee that the preview and the real popup cannot diverge.
    //
    // A ScriptableObject rather than fields on the component, for three reasons. The tuning lives in
    // its own small asset instead of inside SampleScene.unity, which several people edit and whose
    // every merge is paid for by hand. Values tweaked during Play Mode survive leaving it, unlike
    // fields on a scene component, and Play Mode is exactly when feel is worth judging. And presets
    // can sit side by side once a second kind of number is wanted.
    //
    // Read live and never cached: the tool re-reads on every editor tick, so dragging a curve moves
    // the preview under the cursor.
    [CreateAssetMenu(menuName = "LittlePeeps/Harvest Number Motion")]
    public class HarvestNumberMotionDef : ScriptableObject
    {
        [Tooltip("Seconds from spawn to gone.")]
        [Min(0.05f)] public float lifetime = 0.9f;

        [Tooltip("Total travel in world units, reached at the end of the curve below. Straight up by " +
                 "default: the number reads as a readout, and letting it drift sideways only makes it " +
                 "compete with the pickup, which is the thing that IS supposed to fly off diagonally. " +
                 "X is kept tunable in case a lean is wanted later.")]
        public Vector2 travel = new Vector2(0f, 0.8f);

        [Tooltip("How far along `travel` the number is, across its life. A curve that flattens out " +
                 "reads as the number shooting out and settling.")]
        public AnimationCurve travelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Scale across the life. A short spike above 1 at the start gives it a pop.")]
        public AnimationCurve scaleCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Alpha across the life.")]
        public AnimationCurve alphaCurve =
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));

        [Tooltip("Shifts EVERY number, in world units, on top of whatever the source's Fx Anchor says. " +
                 "The division of labour: the anchor is per-object and answers 'where does this thing's " +
                 "feedback come from' (a tree's top edge is not a field's); this is the global nudge, " +
                 "for lifting all numbers a little without editing prefabs one at a time.")]
        public Vector2 spawnOffset = Vector2.zero;

        [Tooltip("Random spawn offset in world units, so numbers from the same spot don't stack into " +
                 "one unreadable blob. Every hit still gets its own number.")]
        public Vector2 spawnJitter = new Vector2(0.15f, 0.1f);

        // Where the number starts. The jitter is handed in rather than rolled here so the caller
        // decides: the game rolls it fresh per hit, while the tool can pin it to zero and replay the
        // identical flight over and over, which is the only way to judge a change to the motion.
        public Vector3 ResolveOrigin(Vector3 harvestPosition, Vector2 normalizedJitter)
        {
            return harvestPosition + new Vector3(
                spawnOffset.x + normalizedJitter.x * spawnJitter.x,
                spawnOffset.y + normalizedJitter.y * spawnJitter.y,
                0f);
        }

        // Positions, scales and fades `view` for normalized life `k` (0 = spawn, 1 = gone).
        //
        // `baseScale` is the prefab's own scale, which scaleCurve multiplies rather than replaces. A
        // world-space TMP is enormous next to a one-unit tile, so the prefab is always scaled right
        // down — writing a raw curve value into localScale would throw that away and blow every
        // number up to tile size.
        public void Apply(TextMeshPro view, Vector3 origin, float k, Vector3 baseScale)
        {
            var tr = view.transform;
            tr.position = origin + (Vector3)(travel * travelCurve.Evaluate(k));
            tr.localScale = baseScale * scaleCurve.Evaluate(k);

            // TMP_Text.alpha recolours the existing vertices; assigning .color or .text would instead
            // mark the mesh dirty and rebuild it, which is the one thing worth avoiding per frame.
            view.alpha = alphaCurve.Evaluate(k);
        }
    }
}
