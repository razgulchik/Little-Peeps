using UnityEngine;

// Anything a bouncing unit can collide with that triggers effects — buildings, resource
// nodes, animals, etc. Owns the collision callbacks, dispatches to its ICollisionEffect
// components, and publishes the global CollisionEvent. Building derives from this; non-building
// objects (e.g. a tree) just add CollisionTarget + effect components like ResourceSource.
//
// The Rigidbody2D must sit on this (root) GameObject so the collision callbacks fire here;
// the collider itself may live on a child (fetched via GetComponentInChildren).
public class CollisionTarget : MonoBehaviour
{
    private Collider2D bodyCollider;
    private ICollisionEffect[] effects;

    protected virtual void Awake()
    {
        bodyCollider = GetComponentInChildren<Collider2D>();
        effects = GetComponents<ICollisionEffect>();
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

    private void HandleHit(Unit unit)
    {
        for (int i = 0; i < effects.Length; i++)
            effects[i].OnHit(unit, this);

        EventBus<CollisionEvent>.Publish(new CollisionEvent(unit, this));
    }

    // Enable/disable the collider (used during drag, and by ResourceSource on depletion).
    public void SetColliderEnabled(bool enabled)
    {
        if (bodyCollider != null) bodyCollider.enabled = enabled;
    }
}
