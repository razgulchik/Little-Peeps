using UnityEngine;

namespace LittlePeeps
{
    // Anything a bouncing unit can collide with that triggers effects — structures, resource
    // nodes, animals, etc. Owns the collision callbacks and routes each hit by the unit's state:
    // a WORKING unit reaches the ICollisionEffect components, after every IHitGate on the target
    // has let the hit through; a TIRED unit reaches only the IShelter components, nothing else.
    // That one branch is the whole fatigue rule — working units can't go home, tired units can't
    // work — so no effect, gate or shelter has to check it. Structure derives from this; an object
    // can also use CollisionTarget directly + effect components like ResourceSource.
    //
    // The Rigidbody2D must sit on this (root) GameObject so the collision callbacks fire here;
    // the body colliders may live on children (fetched via GetComponentsInChildren). A VisitZone
    // child brings its own Rigidbody2D and so keeps its trigger events to itself — see there.
    public class CollisionTarget : MonoBehaviour
    {
        private Collider2D[] colliders;
        private ICollisionEffect[] effects;
        private IHitGate[] gates;
        private IShelter[] shelters;

        protected virtual void Awake()
        {
            colliders = GetComponentsInChildren<Collider2D>();
            effects = GetComponents<ICollisionEffect>();
            gates = GetComponentsInChildren<IHitGate>();
            shelters = GetComponents<IShelter>();
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

        // Tired: shelters only — a resource or a market is a plain wall to a unit that is done for
        // the day. The gates are not even asked on this path, so a tired unit can neither spend a
        // market visit nor heat the forge for nothing. Working: gates first (a refused hit is a plain
        // bounce with no effect), only then the effects.
        private void HandleHit(Unit unit)
        {
            if (unit.IsTired)
            {
                for (int i = 0; i < shelters.Length; i++)
                    shelters[i].OnTiredHit(unit);
                return;
            }

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
