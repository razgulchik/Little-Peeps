using UnityEngine;

namespace LittlePeeps
{
    // Anything a bouncing unit can collide with that triggers effects — structures, resource
    // nodes, animals, etc. Owns the collision callbacks and routes each hit in two steps. First every
    // IEntrance on the target, whatever the unit's state: a building that lets the unit in ends the
    // hit there, and whom it lets in is its own door rule (a house takes the tired and the drunk).
    // Then, for a WORKING unit only, the ICollisionEffect components, after every IHitGate on the target
    // has let the hit through — a drunk unit still works. A tired unit nobody let in stops between the
    // two — that one check is the whole "tired units can't work" rule, so no effect or gate has to check
    // stamina. Structure derives from this; an object can also use CollisionTarget directly + effect
    // components like ResourceSource.
    //
    // The Rigidbody2D must sit on this (root) GameObject so the collision callbacks fire here;
    // the body colliders may live on children (fetched via GetComponentsInChildren). A VisitZone
    // child brings its own Rigidbody2D and so keeps its trigger events to itself — see there.
    public class CollisionTarget : MonoBehaviour
    {
        [Header("Sound")]
        [Tooltip("What a unit bouncing off this sounds like: wood, stone, something soft. Empty = the " +
                 "SoundSystem's default bounce. A hit that pays or takes the unit in plays its own sound " +
                 "instead (SoundSystem.PlayHit).")]
        [SerializeField] private SoundDef bounceSound;

        public SoundDef BounceSound => bounceSound;

        private Collider2D[] colliders;
        private ICollisionEffect[] effects;
        private IHitGate[] gates;
        private IEntrance[] entrances;

        protected virtual void Awake()
        {
            colliders = GetComponentsInChildren<Collider2D>();
            effects = GetComponents<ICollisionEffect>();
            gates = GetComponentsInChildren<IHitGate>();
            entrances = GetComponents<IEntrance>();
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

        // Entrances first, for every unit: one that lets the unit in has taken it off the field, so the
        // hit ends there. Past them a tired unit stops — a resource or a market is a plain wall to a
        // unit that is done for the day, and the gates are not even asked, so it can neither spend a
        // market visit nor heat the forge for nothing. Working: gates first (a refused hit is a plain
        // bounce with no effect), only then the effects.
        private void HandleHit(Unit unit)
        {
            for (int i = 0; i < entrances.Length; i++)
                if (entrances[i].TryEnter(unit)) return;

            if (unit.IsTired) return;

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
