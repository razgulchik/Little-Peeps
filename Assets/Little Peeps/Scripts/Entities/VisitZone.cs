using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // A trigger region on a CollisionTarget that rations hits per visit: a unit inside the zone gets
    // hitsPerVisit counted hits, then nothing until it leaves and comes back. A unit outside the zone
    // never gets a hit counted, so the zone also decides WHICH surfaces pay — for the Market that's
    // the inner walls of the passage, since a unit inside the passage can't touch anything else.
    //
    // Lives on its own child with the trigger collider AND its own Rigidbody2D (Static). That second
    // body is not optional: Unity delivers trigger callbacks to the collider's GameObject and to the
    // GameObject of the Rigidbody2D it is attached to. Hanging off the root body, the zone's
    // OnTriggerEnter2D would also reach CollisionTarget, which treats a trigger entry as a hit — so
    // entering the passage would pay a coin by itself. The own body keeps the events here.
    //
    // Sizing: across the passage the trigger overlaps the walls by a pixel or two (a unit can't be
    // inside a wall, so the overlap changes nothing and only guards against a seam); along it the
    // trigger stops one unit radius short of each mouth, so a unit sliding along the outside of the
    // building never counts as inside.
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class VisitZone : MonoBehaviour, IHitGate
    {
        [Tooltip("Counted hits a unit gets per visit. It has to leave the zone and re-enter for more. " +
                 "This is the BASE: the run's MarketVisitHits modifier is applied on top.")]
        [Min(1)] [SerializeField] private int hitsPerVisit = 3;

        // Units currently inside → counted hits they have left this visit. Exit removes the entry;
        // Physics2D.callbacksOnDisable (on in this project) makes a despawning unit exit too, so one
        // that goes home from inside the passage doesn't leak.
        private readonly Dictionary<Unit, int> visits = new();

        // The run's stat sheet comes through the ResourceSource on the structure root — the route
        // ForgeHeat takes too: StructureSystem already injects the sheet there, so the zone needs no
        // wiring of its own. Null on a zone that is not part of a source; the base then stands.
        private ResourceSource source;

        // Editor-only: runs when the component is added, so the required collider and body come out
        // in the only configuration the zone works in.
        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        }

        private void Awake()
        {
            if (!GetComponent<Collider2D>().isTrigger)
                Debug.LogError($"VisitZone on '{name}' needs its collider set to Is Trigger.", this);

            source = GetComponentInParent<ResourceSource>();
        }

        // Hits one visit is worth, with the run modifier applied. Resolved on ENTRY and never cached:
        // the sheet belongs to the run while the market outlives it (same rule as every other read),
        // and a perk bought mid-run pays from the next visit on — a unit already inside finishes the
        // budget it came in with. Never below one hit, the same floor the field's [Min(1)] declares.
        private int ResolveHitsPerVisit()
        {
            var stats = source != null ? source.Stats : null;
            return Mathf.Max(1, stats != null
                ? stats.ApplyCount(hitsPerVisit, StatId.MarketVisitHits)
                : hitsPerVisit);
        }

        // IHitGate — CollisionTarget asks this before paying out a hit.
        public bool TryConsume(Unit unit)
        {
            if (!visits.TryGetValue(unit, out int left) || left <= 0) return false;
            visits[unit] = left - 1;
            return true;
        }

        // `other` is the raw collider, which on a unit sits on a child of the Unit root — search up.
        private void OnTriggerEnter2D(Collider2D other)
        {
            var unit = other.GetComponentInParent<Unit>();
            if (unit != null) visits[unit] = ResolveHitsPerVisit();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var unit = other.GetComponentInParent<Unit>();
            if (unit != null) visits.Remove(unit);
        }
    }
}
