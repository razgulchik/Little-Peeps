using UnityEngine;

namespace LittlePeeps
{
    // The watermill's store: grinds Food by itself, one sack at a time, into a store that fills up to a cap,
    // and a WORKING worker that hits it carries the whole store off — every sack at once. Nothing has to keep
    // it running: it grinds whether anyone comes or not, and a full mill stands idle until someone empties
    // it. That is the whole tradeoff — enough visits that it never sits full, and past that, more traffic
    // adds nothing.
    //
    // The resource itself is the ResourceSource next to this, like every source's: its def (Never — the
    // mill never runs out, it only runs empty) says who may collect and what ONE SACK is worth to them,
    // and it credits the payout and fires the pickup effect. This component is its add-on, the forge's way:
    //   IHitGate    — a hit on an empty mill is a plain bounce;
    //   IYieldScale — a paying hit pays the per-sack amount × the sacks in store;
    //   Paid        — and only then is the store emptied. A worker the def doesn't list bounces off without
    //                 paying, so the store stays for someone who may take it (the doc's "any worker" is the
    //                 def listing every profession). A tired worker never gets here: CollisionTarget stops
    //                 it before the gates, which is the doc's "non-tired" rule.
    //
    // The store counts WHOLE sacks, not a running amount: a worker bouncing off twice in a row never
    // collects "+0.03", and a sack you see by the mill is a sack in the store, one to one. While the store
    // is full the clock stands at zero — nothing is ground in advance — so the first sack after a
    // collection takes a whole secondsPerSack.
    //
    // The clock and the cap live here, on the component, not in a def or the stats — one kind of mill; a
    // perk that wants one of them turns it into a stat then (the ForgeHeat way). Game time, like every
    // production clock: a paused game (build mode, a perk pick) grinds nothing. The store belongs to this
    // object, so it rides along on a move and is gone with the run.
    [RequireComponent(typeof(ResourceSource))]
    public class Mill : MonoBehaviour, IHitGate, IYieldScale
    {
        [Header("Production")]
        [Tooltip("Seconds to grind one sack. The clock only runs while the store has room.")]
        [SerializeField, Min(0.01f)] private float secondsPerSack = 4f;

        [Tooltip("Sacks the store holds. A full mill grinds nothing until a worker empties it.")]
        [SerializeField, Min(1)] private int capacity = 5;

        private ResourceSource source;
        private int stored;                       // whole sacks waiting
        private float progress;                   // seconds ground toward the next sack

        // For presentation (sacks around the mill, the full-storage animation).
        public int Stored => stored;
        public int Capacity => capacity;
        public bool IsFull => stored >= capacity;

        private void Awake()
        {
            // RequireComponent only adds the source when this component is added; a prefab made before that
            // can still lack it, and then there is nobody to pay.
            source = GetComponent<ResourceSource>();
            if (source == null)
                Debug.LogError($"Mill on '{name}' needs a ResourceSource on the same object — it pays nothing.", this);
        }

        private void OnEnable() { if (source != null) source.Paid += OnPaid; }
        private void OnDisable() { if (source != null) source.Paid -= OnPaid; }

        private void Start()
        {
            var def = source != null ? source.Def : null;
            if (def != null && def.depletion != Depletion.Never)
                Debug.LogError($"Mill on '{name}': '{def.name}' must be Never — the mill runs empty, never out; " +
                               "used up, it would stop taking hits (and maybe lose its body) for the regrow time.", this);
        }

        // IHitGate — a plain look, nothing spent: the store is emptied on Paid, once the hit has really paid.
        public bool TryConsume(Unit unit) => stored > 0;

        // IYieldScale — the def's amount is per sack.
        public float Factor(Unit unit) => stored;

        private void OnPaid() => stored = 0;

        // A long frame may grind more than one sack, never past the cap; reaching the cap drops the part-ground
        // sack, so a full mill banks nothing for later.
        private void Update()
        {
            if (stored >= capacity) return;

            progress += Time.deltaTime;
            while (progress >= secondsPerSack && stored < capacity)
            {
                progress -= secondsPerSack;
                stored++;
            }
            if (stored >= capacity) progress = 0f;
        }
    }
}
