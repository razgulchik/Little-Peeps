namespace LittlePeeps
{
    // Strategy interface for effects triggered when a WORKING unit collides with a CollisionTarget
    // (resource node, animal, …). A tired unit's hit never gets here, and neither does one an IEntrance
    // on the target took in — see CollisionTarget.HandleHit. Masking (unit type check) must be
    // implemented inside OnHit, which says what it did (HitOutcome).
    //
    // Serves is the other question the hit's verdict needs (CollisionTarget.Resolve): whether this effect
    // is for `unit` at all, the moment aside — a tree is for a lumberjack whether it is grown or not, and
    // whether the lumberjack is tired or not. It decides nothing in the game; it only tells a refused hit
    // that could have worked (Blocked) from one that never could (Useless).
    public interface ICollisionEffect
    {
        bool Serves(Unit unit);
        HitOutcome OnHit(Unit unit, CollisionTarget target);
    }
}
