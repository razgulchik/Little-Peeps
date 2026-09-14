using UnityEngine;

namespace LittlePeeps
{
    // Anything a bouncing unit can collide with that triggers effects — structures, resource
    // nodes, animals, etc. Owns the collision callbacks and dispatches to its ICollisionEffect
    // components, after every IHitGate on the target has let the hit through. Structure derives
    // from this; an object can also use CollisionTarget directly + effect components like
    // ResourceSource.
    //
    // The Rigidbody2D must sit on this (root) GameObject so the collision callbacks fire here;
    // the body colliders may live on children (fetched via GetComponentsInChildren). A VisitZone
    // child brings its own Rigidbody2D and so keeps its trigger events to itself — see there.
    public class CollisionTarget : MonoBehaviour
    {
        private Collider2D[] colliders;
        private ICollisionEffect[] effects;
        private IHitGate[] gates;

        protected virtual void Awake()
        {
            colliders = GetComponentsInChildren<Collider2D>();
            effects = GetComponents<ICollisionEffect>();
            gates = GetComponentsInChildren<IHitGate>();
        }

        // Obstacle path: unit bounces off (collider isTrigger = false)
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.TryGetComponent<Unit>(out var unit))
                HandleHit(unit);
        }

        // Interactable path: unit passes through (collider isTrigger = true). Here `other` is the
        // raw collider, which on a unit sits on a CHILD of the Unit root — so search up the
        // hierarchy (unlike OnCollisionEnter2D, where collision.gameObject is the Rigidbody root).
        private void OnTriggerEnter2D(Collider2D other)
        {
            var unit = other.GetComponentInParent<Unit>();
            if (unit != null) HandleHit(unit);
        }

        // Gates first: a refused hit is a plain bounce with no effect. Only then do effects run.
        private void HandleHit(Unit unit)
        {
            for (int i = 0; i < gates.Length; i++)
                if (!gates[i].TryConsume(unit)) return;
            for (int i = 0; i < effects.Length; i++)
                effects[i].OnHit(unit, this);
        }

        // Enable/disable every collider on the target (ResourceSource uses it on depletion). Includes a
        // VisitZone's trigger: with Physics2D.callbacksOnDisable on, disabling it exits every unit inside.
        public void SetColliderEnabled(bool enabled)
        {
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = enabled;
        }
    }
}
