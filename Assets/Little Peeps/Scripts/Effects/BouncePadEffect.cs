using UnityEngine;

// Redirects a unit's velocity on contact, independent of unit type masking
public class BouncePadEffect : ICollisionEffect
{
    public Vector2 bounceDirection;
    public float forceMagnitude;

    public void OnHit(Unit unit, CollisionTarget target)
    {
        // TODO: unit.GetComponent<Rigidbody2D>().linearVelocity = bounceDirection.normalized * forceMagnitude
    }
}
