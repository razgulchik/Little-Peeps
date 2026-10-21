using UnityEngine;

namespace LittlePeeps
{
    // An arrow in flight (ArcherAbility shoots it). It follows its target — re-aimed every frame, so a
    // walking animal is still hit — and on arrival lands the shooter's hit on it through the target's
    // CollisionTarget.HitFromRange: the payout and the depletion are the animal's ResourceSource's, as for
    // a hit by contact. No collider and no physics: the arrow cannot miss and cannot hit anything else.
    //
    // It lands only if the hit still means what it meant at the shot. The target gone (another arrow or a
    // hunter's body got it first, its den was sold) or the shooter no longer the same hunter (gone home,
    // back in the pool) — the arrow just vanishes, paying nobody.
    //
    // The art faces right (+X); the arrow turns along its flight.
    public class Arrow : MonoBehaviour
    {
        // An arrow that has not landed in this long is dropped whatever it chases — a safety net, not a
        // rule: at any sane arrowSpeed it lands in a fraction of a second.
        private const float MaxFlightSeconds = 5f;

        private Unit shooter;
        private ProfessionDef profession;   // the shooter's at the shot — the arrow pays only that hunter
        private CollisionTarget target;
        private Vector3 aimOffset;          // from the target's feet to the middle of its body
        private float speed;
        private float flight;

        // Set off from where it was instantiated toward `target`. Aims at the middle of the target's body
        // (its collider's bounds), measured once here and carried along — the animal's feet are its
        // transform and would read as a shot into the ground.
        public void Launch(Unit shooter, CollisionTarget target, float speed)
        {
            this.shooter = shooter;
            profession = shooter != null ? shooter.Profession : null;
            this.target = target;
            this.speed = speed;
            flight = 0f;

            if (target == null) return;   // Update drops it on the first frame

            var body = target.GetComponentInChildren<Collider2D>();
            aimOffset = body != null && body.enabled ? body.bounds.center - target.transform.position : Vector3.zero;
            aimOffset.z = 0f;

            Face(AimPoint - transform.position);
        }

        private Vector3 AimPoint => target.transform.position + aimOffset;

        private void Update()
        {
            flight += Time.deltaTime;
            if (target == null || flight > MaxFlightSeconds)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 to = AimPoint - transform.position;
            to.z = 0f;
            float step = speed * Time.deltaTime;
            if (to.sqrMagnitude <= step * step)
            {
                Land();
                return;
            }

            Vector3 dir = to.normalized;
            transform.position += dir * step;
            Face(dir);
        }

        private void Land()
        {
            // `!= null` on the shooter, not `?.`: a destroyed unit only reads as null through Unity's operator.
            if (shooter != null && shooter.isActiveAndEnabled && shooter.Profession == profession)
                target.HitFromRange(shooter);
            Destroy(gameObject);
        }

        private void Face(Vector3 dir)
        {
            if (dir.x * dir.x + dir.y * dir.y < 0.0001f) return;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
    }
}
