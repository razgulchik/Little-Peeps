using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Marks what a bow can shoot at: the boar, the fox, the coop's bird — game, not livestock (the
    // alpaca). It is a mark of its own because nothing else says it — how the target moves (AnimalWander
    // walks, a Goose flies by physics) is beside the point, and being a ResourceSource is not enough: a wheat
    // field is one too.
    //
    // WHO shoots it stays the yield table's business: a hunter only aims at what pays a hunter (Accepts).
    // The hit is the shooter's ordinary hit on the CollisionTarget beside this (HitFromRange).
    //
    // Enabled = can be shot right now. Whoever makes the target untouchable for a while switches this off:
    // no bow sees it, and an arrow already on its way to it vanishes (Arrow).
    [RequireComponent(typeof(CollisionTarget), typeof(ResourceSource))]
    public class ArrowTarget : MonoBehaviour
    {
        private static readonly List<ArrowTarget> alive = new();

        // Everything that can be shot right now.
        public static IReadOnlyList<ArrowTarget> Alive => alive;

        private CollisionTarget host;
        private ResourceSource source;

        private void Awake()
        {
            host = GetComponent<CollisionTarget>();
            source = GetComponent<ResourceSource>();
        }

        private void OnEnable() => alive.Add(this);
        private void OnDisable() => alive.Remove(this);

        // Whether a worker of this type would be paid for the hit right now.
        public bool Accepts(UnitType type) => source.Accepts(type);

        // The arrow has landed.
        public void Hit(Unit shooter) => host.HitFromRange(shooter);
    }
}
