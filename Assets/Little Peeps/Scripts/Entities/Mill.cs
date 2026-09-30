using UnityEngine;

namespace LittlePeeps
{
    // The watermill: grinds Food by itself, one sack at a time, into a store that fills up to a cap, and a
    // WORKING worker that hits it carries the whole store off — every sack at once. Who may collect, and how
    // much a sack is worth to them, is the def's worker yields, as for every source: a listed worker gets
    // its amount per sack, anyone else bounces and leaves the store where it is (the doc's "any worker"
    // is the def listing every profession). A tired worker never gets here (CollisionTarget stops it
    // before the effects), which is the doc's "non-tired" rule; a hit on an empty mill is a plain bounce.
    // Nothing has to keep it running: it grinds whether anyone comes or not, and a full mill stands idle
    // until someone empties it. That is the whole tradeoff — enough visits that it never sits full, and
    // past that, more traffic adds nothing.
    //
    // The store counts WHOLE sacks, not a running amount: a worker bouncing off twice in a row never
    // collects "+0.03", and a sack you see by the mill is a sack in the store, one to one. While the store
    // is full the clock stands at zero — nothing is ground in advance — so the first sack after a
    // collection takes a whole secondsPerSack.
    //
    // The clock and the cap live here, on the component, not in a def or the stats — one kind of mill; a
    // perk that wants one of them turns it into a stat then (the ForgeHeat way). Game time, like every production
    // clock: a paused game (build mode, a perk pick) grinds nothing. Only reachable through StructureSystem,
    // which injects the ResourceSystem; the store belongs to this object, so it rides along on a move and
    // is gone with the run.
    [RequireComponent(typeof(CollisionTarget))]
    public class Mill : MonoBehaviour, ICollisionEffect
    {
        [Header("Production")]
        [Tooltip("Seconds to grind one sack. The clock only runs while the store has room.")]
        [SerializeField, Min(0.01f)] private float secondsPerSack = 4f;

        [Tooltip("Sacks the store holds. A full mill grinds nothing until a worker empties it.")]
        [SerializeField, Min(1)] private int capacity = 5;

        [Tooltip("The mill's source: Food as its resource, the pickup effect, and the worker yields — who may " +
                 "collect and what ONE SACK is worth to them. A hit pays sacks × that amount, credited through " +
                 "the production gateway like any harvest: bonuses, prestige and the pickup effect come with " +
                 "it. Also the scope a 'more food from mills' perk would name.")]
        [SerializeField] private ResourceSourceDef foodSource;

        [Tooltip("Where the pickup effect and the floating number leave from. Empty = this transform.")]
        [SerializeField] private Transform fxAnchor;

        private ResourceSystem resourceSystem;   // injected by StructureSystem on placement, before Start
        private int stored;                       // whole sacks waiting
        private float progress;                   // seconds ground toward the next sack

        // For presentation (sacks around the mill, the full-storage animation).
        public int Stored => stored;
        public int Capacity => capacity;
        public bool IsFull => stored >= capacity;

        public void Initialize(ResourceSystem resources)
        {
            resourceSystem = resources;
        }

        private void Start()
        {
            if (foodSource == null)
                Debug.LogError($"Mill on '{name}' has no food source (ResourceSourceDef) — it pays nothing.", this);
            if (resourceSystem == null)
                Debug.LogError($"Mill on '{name}' has no ResourceSystem — it must be built through StructureSystem, " +
                               "and until then it pays nothing.", this);
        }

        // ICollisionEffect — only working units get here. A worker the def doesn't list bounces off and the
        // store stays for someone who may take it; so does everything when there is nothing to pay into.
        // unit.Type still names the worker's profession, so the yield modifiers key on it the same way they
        // do for a field.
        public void OnHit(Unit unit, CollisionTarget target)
        {
            if (stored <= 0 || unit == null) return;
            if (resourceSystem == null || foodSource == null) return;
            if (!foodSource.TryGetYield(unit.Type, out float perSack)) return;

            resourceSystem.AddHarvest(foodSource, unit.Type, stored * perSack, FxOrigin);
            stored = 0;
        }

        private Vector3 FxOrigin => fxAnchor != null ? fxAnchor.position : transform.position;

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
