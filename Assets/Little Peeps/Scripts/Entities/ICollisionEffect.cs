namespace LittlePeeps
{
    // Strategy interface for effects triggered when a WORKING unit collides with a CollisionTarget
    // (resource node, animal, …). A tired unit's hit never gets here — CollisionTarget routes it to
    // the target's IShelters instead. Masking (unit type check) must be implemented inside OnHit.
    public interface ICollisionEffect
    {
        void OnHit(Unit unit, CollisionTarget target);
    }
}
