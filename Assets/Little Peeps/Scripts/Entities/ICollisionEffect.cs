namespace LittlePeeps
{
    // Strategy interface for effects triggered when a WORKING unit collides with a CollisionTarget
    // (resource node, animal, …). A tired unit's hit never gets here, and neither does one an IEntrance
    // on the target took in — see CollisionTarget.HandleHit. Masking (unit type check) must be
    // implemented inside OnHit.
    public interface ICollisionEffect
    {
        void OnHit(Unit unit, CollisionTarget target);
    }
}
