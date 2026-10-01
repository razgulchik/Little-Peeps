using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The rice paddy's crop: a walk-through field that grows on its own and gives all of it to the first
    // working farmer who walks in once it is ripe. Three phases, round and round:
    //   Growing   — water comes in, the crop comes up. Only while NOBODY is on the field: any unit inside,
    //               working or tired, drunk, any profession, holds the growth where it is (never resets
    //               it). That is the whole tradeoff — the field wants visits with empty time between
    //               them, and a crowd, or a slow tired unit dawdling across, costs it food.
    //   Ripe      — waits for a farmer. The first working unit the def lists to ENTER the field harvests
    //               it whole, one payout; anyone else walks through and the crop stays.
    //   Harvested — dead ground until the def's regrow time is up, whatever happens on it: nothing grows
    //               there yet, so there is nothing for units to hold up.
    // A newly built field starts Growing from nothing.
    //
    // The resource itself is the ResourceSource next to this, like every source's: who may harvest and how
    // much (the def's worker yields), the payout and its pickup effect, and Harvested → regrow time → back,
    // with the run's regrow modifier. Its def must be Regrow with ONE hit — the hit that harvests is the hit
    // that uses it up. This component adds only the growth in between, and is the source's gate: entering
    // the field is a hit (the collider is a trigger), CollisionTarget stops a tired unit before the gates —
    // the doc's "non-tired" — and the gate lets the rest through only while the crop is ripe.
    //
    // Who is on the field is asked of the physics every frame (Collider2D.Overlap on the field's own
    // colliders), never kept as an enter/exit list: a unit that leaves the field from inside it — into a
    // house, the tavern, the pool — has its body switched off rather than its collider, and a list that
    // missed that exit would hold the field still forever. A body that isn't simulated is simply not in
    // the query. Only asked while growing, and only while the game runs.
    //
    // Growth time is here on the component (one kind of paddy; a perk that wants it makes it a stat, the
    // Mill way). Game time: a paused game (build mode, a perk pick) grows nothing. What the phases LOOK like
    // is RicePaddyView.
    [RequireComponent(typeof(ResourceSource))]
    public class RicePaddy : MonoBehaviour, IHitGate
    {
        public enum Phase { Growing, Ripe, Harvested }

        [Tooltip("Seconds of EMPTY field it takes to grow from bare ground to ripe. Any unit on the field " +
                 "stops the clock; it goes on from where it stood once the field is empty again.")]
        [SerializeField, Min(0.01f)] private float growTime = 30f;

        // Shared by every paddy: Update runs one at a time, and the list only lives inside one query.
        private static readonly List<Collider2D> overlaps = new();

        private ResourceSource source;
        private Collider2D[] area;               // the field's own colliders — what "on the field" means
        private ContactFilter2D bodies;          // solid colliders only: a unit's body, never someone's trigger
        private float grown;                      // seconds grown toward ripe since the ground came back

        // For presentation.
        public Phase Current => source == null || !source.IsReady ? Phase.Harvested
                              : grown >= growTime ? Phase.Ripe
                              : Phase.Growing;
        public float GrowthProgress => Current == Phase.Growing ? Mathf.Clamp01(grown / growTime) : 1f;

        private void Awake()
        {
            area = GetComponentsInChildren<Collider2D>();
            bodies = new ContactFilter2D { useTriggers = false };

            // RequireComponent only adds the source when this component is added; a prefab made before
            // that can still lack it, and without it there is no crop to grow.
            source = GetComponent<ResourceSource>();
            if (source == null)
            {
                Debug.LogError($"RicePaddy on '{name}' needs a ResourceSource on the same object.", this);
                enabled = false;
            }
        }

        private void OnEnable() { if (source != null) source.Regrown += OnRegrown; }
        private void OnDisable() { if (source != null) source.Regrown -= OnRegrown; }

        private void Start()
        {
            var def = source.Def;
            if (def != null && (def.depletion != Depletion.Regrow || def.hitsToDeplete != 1))
                Debug.LogError($"RicePaddy on '{name}': '{def.name}' must be Regrow with 1 hit to deplete — " +
                               "the harvest has to use the crop up, or a ripe field pays every farmer who " +
                               "walks in.", this);
            if (area.Length == 0)
                Debug.LogError($"RicePaddy on '{name}' has no collider — units can neither enter nor hold it.", this);
            for (int i = 0; i < area.Length; i++)
                if (!area[i].isTrigger)
                    Debug.LogError($"RicePaddy on '{name}': collider '{area[i].name}' must be Is Trigger — units " +
                                   "walk through the field, they don't bounce off it.", area[i]);
        }

        // IHitGate — a plain look, nothing spent: the source uses the crop up itself when it pays.
        public bool TryConsume(Unit unit) => Current == Phase.Ripe;

        // The ground is back: the crop starts again from bare earth.
        private void OnRegrown() => grown = 0f;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Current != Phase.Growing || IsOccupied()) return;
            grown += dt;
        }

        // Any unit's body overlapping any of the field's colliders. Animals and everything else that isn't
        // a Unit don't count — the doc's occupancy is units.
        private bool IsOccupied()
        {
            for (int a = 0; a < area.Length; a++)
            {
                int n = area[a].Overlap(bodies, overlaps);
                for (int i = 0; i < n; i++)
                {
                    var body = overlaps[i].attachedRigidbody;
                    if (body != null && body.TryGetComponent<Unit>(out _)) return true;
                }
            }
            return false;
        }
    }
}
