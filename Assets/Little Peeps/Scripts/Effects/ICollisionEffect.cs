// Strategy interface for effects triggered when a unit collides with a CollisionTarget
// (building, resource node, …). Masking (unit type check) must be implemented inside OnHit.
public interface ICollisionEffect
{
    void OnHit(Unit unit, CollisionTarget target);
}
