using UnityEngine;

namespace LittlePeeps
{
    // Heat on the Forge. Every paying hit adds heatPerHit; reaching maxHeat trips OVERHEAT, during
    // which a hit is a plain bounce — no metal, and no heat either — until the forge has cooled all
    // the way back to ZERO, not merely below the cap. That full cool-down is the penalty and the whole
    // point of the mechanic: hammer a hot forge and you gamble the next coolingTime seconds of output.
    // Cooling never stops. It runs at maxHeat / coolingTime per second while the forge is being hit
    // too, so a trickle of workers keeps it warm indefinitely and only a crowd tips it over.
    //
    // Two roles on one component because they read the same number:
    //   IHitGate    — WHETHER a hit pays (CollisionTarget.HandleHit asks before any effect runs);
    //   IYieldScale — HOW MUCH extra it pays (ResourceSource asks once the gate has let it through).
    //
    // Hot yield is a locked ability with no flag behind it. ForgeHotYield is a pure multiplier stat
    // that reads ×1 until a perk adds to it, so a fresh run pays exactly the base metal at any heat.
    // With the stat at ×1.5 a hit at full heat pays ×1.5, and `hotness` says how much of that bonus
    // applies lower down — linear by default, so half heat pays ×1.25; bend it in the prefab for a
    // "hot zone" and no code moves. Sampled AFTER this hit's heat is added, so the hit that trips
    // overheat is the most valuable one. That is the risk/reward, not an off-by-one.
    //
    // Only hits the source actually pays for heat the forge. The gate runs before ResourceSource gets
    // to match the worker against the def, so it asks the source itself: a Lumberjack bouncing off the
    // forge must not warm it for nothing, or a busy village would overheat it with hits that never
    // paid a single bar of metal.
    //
    // Base values live here, on the component, not in a def — there is one forge. All four numbers
    // are stats, resolved at the point of use and never cached: the sheet belongs to the run, and a
    // forge placed in one run outlives it (same rule as ResourceSource.ResolveRespawnTime). The sheet
    // comes through the sibling ResourceSource, which StructureSystem already injects — one plumbing
    // point instead of two.
    [RequireComponent(typeof(ResourceSource))]
    public class ForgeHeat : MonoBehaviour, IHitGate, IYieldScale
    {
        [Tooltip("Heat one paying hit adds.")]
        [Min(0f)] [SerializeField] private float heatPerHit = 1f;

        [Tooltip("Heat at which the forge overheats and stops paying until it has cooled to zero.")]
        [Min(0.01f)] [SerializeField] private float maxHeat = 10f;

        [Tooltip("Seconds to cool from Max Heat to zero. Cooling runs at this rate at all times, hit or not.")]
        [Min(0.01f)] [SerializeField] private float coolingTime = 20f;

        [Tooltip("How much of the Forge Hot Yield bonus applies at a given heat: x = heat 0..1, " +
                 "y = share of the bonus 0..1. Default is linear — the full bonus only at full heat.")]
        [SerializeField] private AnimationCurve hotness = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private ResourceSource source;
        private float heat;
        private bool overheated;

        // For presentation (a bar, a tint). Against the CURRENT cap, so a perk that raises it shows the
        // bar dropping rather than the number lying.
        public float Heat01 => Mathf.Clamp01(heat / ResolveMaxHeat());
        public bool IsOverheated => overheated;

        // Raised for every hit that heats the forge — i.e. every hit that is about to pay metal — after
        // the heat is in, so a listener reading Heat01 sees the state the hit produced. Presentation
        // hangs off this (sparks at the anvil); the metal itself goes through ResourceSource as always.
        public event System.Action HitPaid;

        private void Awake()
        {
            source = GetComponent<ResourceSource>();
        }

        // IHitGate — CollisionTarget asks this before dispatching the hit to ResourceSource.
        public bool TryConsume(Unit unit)
        {
            if (overheated) return false;

            // Not a paying worker: let the hit through untouched. Whether it pays is the def's
            // business and ResourceSource settles it downstream; heat only follows metal.
            if (unit == null || !source.Accepts(unit.Type)) return true;

            float max = ResolveMaxHeat();
            heat = Mathf.Min(heat + Resolve(heatPerHit, StatId.ForgeHeatPerHit), max);
            if (heat >= max) overheated = true;
            HitPaid?.Invoke();
            return true;
        }

        // IYieldScale — asked by ResourceSource after the gate let the hit through, so `heat` already
        // includes this hit. Clamped both ways: a curve overshoot must not turn a locked ability on.
        public float Factor(Unit unit)
        {
            var stats = source.Stats;
            float multiplier = stats != null ? stats.Multiplier(StatId.ForgeHotYield) : 1f;
            return 1f + (multiplier - 1f) * Mathf.Clamp01(hotness.Evaluate(Heat01));
        }

        private void Update()
        {
            if (heat <= 0f) return;

            // A perk can only ever drag coolingTime toward zero, never past it, but a −100% is a valid
            // authoring value and must read as "cools instantly" rather than divide by zero.
            float time = Resolve(coolingTime, StatId.ForgeCoolingTime);
            heat = time > 0f
                ? Mathf.Max(0f, heat - ResolveMaxHeat() / time * Time.deltaTime)
                : 0f;

            // Hysteresis lives here and nowhere else: overheat is lifted only by cooling to zero.
            if (heat <= 0f) overheated = false;
        }

        private float ResolveMaxHeat() => Resolve(maxHeat, StatId.ForgeMaxHeat);

        private float Resolve(float baseValue, StatId id)
        {
            var stats = source.Stats;
            return stats != null ? stats.Apply(baseValue, id) : baseValue;
        }
    }
}
