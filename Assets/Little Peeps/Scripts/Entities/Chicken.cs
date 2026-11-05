using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // A bird a coop has let out. It flies the island the way a unit does, by physics and at a held speed,
    // but it is NOT a Unit, and that is the point: CollisionTarget, TapSystem and every door in the game
    // pass over anything that is not a Unit, so to a bird every building is a plain wall.
    //
    // The prefab carries two colliders. The solid one sits on the Chicken layer, which collides with
    // walls and not with units or other birds. The trigger sits on Default, so units cross it without
    // turning, the way they cross a field, and that crossing is the harvest: an ordinary ResourceSource
    // (Despawn, one hit) next to a CollisionTarget pays and destroys the bird. Every way a bird leaves ends
    // in OnDestroy, so the coop puts it back in the pen the same way each time.
    //
    // A bird just let out cannot be caught for a moment: its trigger stays off, so the farmer who hit the
    // coop does not catch it on the doorstep. Switching back on, the trigger reports any unit already
    // inside it, so nobody flies through a bird unnoticed once the moment is over.
    [RequireComponent(typeof(Rigidbody2D))]
    public class Chicken : MonoBehaviour
    {
        [Tooltip("Flying speed in world units per second. Held every physics step, like a unit's.")]
        [SerializeField, Min(0.1f)] private float speed = 2f;

        [Tooltip("The solid circle that bounces off walls. Its layer must not collide with units.")]
        [SerializeField] private CircleCollider2D body;

        [Tooltip("The trigger units catch the bird by. Off while the bird is leaving the coop.")]
        [SerializeField] private CircleCollider2D hitbox;

        [Tooltip("Flipped to face the way the bird flies. The art must face right.")]
        [SerializeField] private SpriteRenderer model;

        [Tooltip("Seconds after leaving the coop during which the bird cannot be caught. 0 = off.")]
        [SerializeField, Min(0f)] private float releaseImmunity = 1.5f;

        [Header("Bounce")]
        [Tooltip("A bounce never leaves closer than this to the wall's perpendicular, so a bird cannot " +
                 "shuttle forever between two parallel walls. 0 = off.")]
        [SerializeField, Range(0f, 45f)] private float minBounceAngle = 10f;

        [Tooltip("Random turn added to a bounce, in degrees either way, so birds drift apart. 0 = off.")]
        [SerializeField, Range(0f, 30f)] private float bounceJitterDegrees = 3f;

        [Header("Stuck")]
        [Tooltip("A bird that never gets further than this (world units) from where it stood at the start " +
                 "of a window is stuck, and goes home unpaid so the flock does not lose it.")]
        [SerializeField, Min(0f)] private float stuckRadius = 1f;

        [Tooltip("Length of one such window in seconds. 0 = off.")]
        [SerializeField, Min(0f)] private float stuckWindow = 5f;

        // How far the speed may drift before it is written back: wider than the solver's float noise,
        // narrower than any real knock.
        private const float SpeedTolerance = 0.01f;

        private static readonly List<Chicken> alive = new();

        // Every bird in flight: a construction site sends home the ones on its spot, and a regrowing tree
        // waits for the ones on its cells.
        public static IReadOnlyList<Chicken> Alive => alive;

        private Rigidbody2D rb;
        private ChickenCoop owner;
        private StuckWatch stuckWatch;
        private float immunityLeft;
        private bool leaving;

        public Vector2 BodyCenter => body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
        public float BodyRadius => body != null ? body.radius : 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (body == null) Debug.LogError($"Chicken on '{name}' has no body collider assigned.", this);
            if (hitbox == null) Debug.LogError($"Chicken on '{name}' has no hitbox collider assigned.", this);
            if (model == null) Debug.LogError($"Chicken on '{name}' has no model assigned.", this);
        }

        private void OnEnable() => alive.Add(this);
        private void OnDisable() => alive.Remove(this);

        // Runtime injection (ChickenCoop calls this on spawn, since a prefab can't reference a scene object).
        public void Launch(ChickenCoop coop, Vector2 direction)
        {
            owner = coop;
            stuckWatch.Reset(transform.position);
            rb.linearVelocity = direction.normalized * speed;
            Face(direction);

            immunityLeft = releaseImmunity;
            if (hitbox != null) hitbox.enabled = immunityLeft <= 0f;
        }

        // Back to the pen unpaid: stuck, or the player built on top of the bird. Physics goes off at once,
        // so nothing can harvest it in the moment before the deferred Destroy.
        public void GoHome()
        {
            if (leaving) return;
            leaving = true;
            rb.simulated = false;

            if (owner != null) owner.Puff(transform.position);
            Destroy(gameObject);
        }

        // The coop hears about every way out here, so ResourceSource never has to know what a coop is.
        private void OnDestroy()
        {
            if (owner != null) owner.NotifyGone(this);
        }

        // Physics owns the direction, the speed is ours: a knock from a moving animal changes it, and a bird
        // pinned to a stop would otherwise never move again.
        private void FixedUpdate()
        {
            if (immunityLeft > 0f)
            {
                immunityLeft -= Time.fixedDeltaTime;
                if (immunityLeft <= 0f && hitbox != null) hitbox.enabled = true;
            }

            if (stuckWindow > 0f && stuckWatch.Tick(transform.position, Time.fixedDeltaTime, stuckRadius, stuckWindow))
            {
                GoHome();
                return;
            }

            Vector2 velocity = rb.linearVelocity;
            float current = velocity.magnitude;
            if (current < 0.0001f)
            {
                rb.linearVelocity = Random.insideUnitCircle.normalized * speed;
                return;
            }

            if (Mathf.Abs(current - speed) > SpeedTolerance)
                rb.linearVelocity = velocity * (speed / current);
        }

        // The same two corrections a unit's bounce gets (Unit.OnCollisionEnter2D), for the same reason.
        private void OnCollisionEnter2D(Collision2D collision)
        {
            Vector2 velocity = rb.linearVelocity;
            float current = velocity.magnitude;
            if (current < 0.0001f) return;   // wedged: FixedUpdate's kick handles it

            Vector2 dir = velocity / current;

            if (minBounceAngle > 0f && collision.contactCount > 0)
            {
                Vector2 normal = collision.GetContact(0).normal;
                if (Vector2.Dot(normal, dir) < 0f) normal = -normal;
                dir = Unit.BendAwayFromNormal(dir, normal, minBounceAngle, Random.value < 0.5f ? -1f : 1f);
            }

            if (bounceJitterDegrees > 0f)
                dir = Quaternion.Euler(0f, 0f, Random.Range(-bounceJitterDegrees, bounceJitterDegrees)) * dir;

            rb.linearVelocity = dir * current;
            Face(dir);
        }

        private void Face(Vector2 direction)
        {
            if (model != null && Mathf.Abs(direction.x) > 0.01f) model.flipX = direction.x < 0f;
        }
    }
}
