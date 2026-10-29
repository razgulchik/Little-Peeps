using UnityEngine;

namespace LittlePeeps
{
    // The bow: a unit with this ability shoots the nearest animal within `radius` that pays its
    // profession right now, then waits `cooldown` seconds before the next shot. The arrow (an Arrow
    // prefab) flies to the animal and lands as an ordinary hit of the shooter's — the animal's
    // ResourceSource pays and depletes exactly as if the unit had run into it (CollisionTarget.
    // HitFromRange). The unit keeps walking and bouncing the whole time; shooting never stops it.
    //
    // WHO is a target is the yield table, not a list here: an animal whose ResourceSource accepts the
    // shooter's profession (ResourceSource.Accepts — paid by that profession, and ready). For the hunter
    // that is the boar and the fox today; give the alpaca a hunter yield and hunters shoot alpacas.
    //
    // Numbers and mechanics live here, the per-unit state (the cooldown) in Bow — see ProfessionAbility.
    [CreateAssetMenu(menuName = "LittlePeeps/Abilities/Archer")]
    public class ArcherAbility : ProfessionAbility
    {
        [Tooltip("How far the unit sees a target, in world units, from its feet to the animal's.")]
        [SerializeField, Min(0f)] private float radius = 3f;

        [Tooltip("Seconds between two shots of one unit. A fresh bow is ready at once: the first target " +
                 "in range is shot the moment it shows up.")]
        [SerializeField, Min(0.05f)] private float cooldown = 2f;

        [Header("Arrow")]
        [Tooltip("What flies. An Arrow component on the root; the art faces right (+X), the arrow turns " +
                 "along its flight.")]
        [SerializeField] private Arrow arrowPrefab;

        [Tooltip("Arrow speed, world units per second. Keep it well above the animals' own speed: the " +
                 "arrow follows its target and lands only once it catches up.")]
        [SerializeField, Min(0.1f)] private float arrowSpeed = 10f;

        public override IAbilityState CreateState(Unit unit)
        {
            if (arrowPrefab == null)
                Debug.LogError($"ArcherAbility '{name}' has no arrow prefab — its units will not shoot.", this);
            return new Bow(this, unit);
        }

        // The nearest animal within radius that pays this unit's profession right now; false = none.
        // Measured feet to feet, from the transforms. Distance first, the components only for the few in
        // range — the list is every animal on the island. The radius is the run's ShootRadius on top of
        // the asset's, resolved on every search and never written back: this asset is shared by every
        // hunter, and a Play Mode write would stay in it.
        private bool TryFindTarget(Unit unit, out CollisionTarget target)
        {
            target = null;
            Vector2 from = unit.transform.position;
            var stats = unit.Stats;
            float range = stats != null ? stats.Apply(radius, StatId.ShootRadius, unit.Type) : radius;
            float best = range * range;

            var animals = AnimalWander.Alive;
            for (int i = 0; i < animals.Count; i++)
            {
                var animal = animals[i];
                float d = ((Vector2)animal.transform.position - from).sqrMagnitude;
                if (d > best) continue;
                if (!animal.TryGetComponent(out ResourceSource source) || !source.Accepts(unit.Type)) continue;
                if (!animal.TryGetComponent(out CollisionTarget hit)) continue;

                best = d;
                target = hit;
            }
            return target != null;
        }

        // Loose the arrow from the unit's body toward the target. False when there is no arrow to shoot
        // (no prefab) — the cooldown is then not spent.
        private bool Fire(Unit unit, CollisionTarget target)
        {
            if (arrowPrefab == null) return false;

            var arrow = Instantiate(arrowPrefab, unit.BodyCenter, Quaternion.identity);
            arrow.Launch(unit, target, arrowSpeed);
            return true;
        }

        // One unit's bow: only the cooldown is its own.
        private sealed class Bow : IAbilityState
        {
            private readonly ArcherAbility def;
            private readonly Unit unit;
            private float cooldownLeft;   // 0 = ready

            public Bow(ArcherAbility def, Unit unit)
            {
                this.def = def;
                this.unit = unit;
            }

            public void Tick(float deltaTime)
            {
                if (cooldownLeft > 0f)
                {
                    cooldownLeft -= deltaTime;
                    if (cooldownLeft > 0f) return;
                }

                if (!def.TryFindTarget(unit, out var target)) return;
                if (def.Fire(unit, target)) cooldownLeft = def.cooldown;
            }
        }
    }
}
