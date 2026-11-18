namespace LittlePeeps
{
    // Optional gate between a collision and its effects. CollisionTarget asks every gate found on the
    // target (root + children) before dispatching a hit to its ICollisionEffects, and drops the hit if
    // any gate refuses. The unit still bounces — a refused hit only means "no effect this time".
    //
    // TryConsume decides and spends in one step on purpose: a gate that rations hits (VisitZone) must
    // not hand out a budget it can't then debit. Gates are asked in order and a gate that consumed
    // before a later one refused has spent a hit for nothing — keep one rationing gate per target.
    //
    // `hit` is the hit as it arrived — how (Contact), where (Point) and on which face (Side) — for a gate
    // whose answer depends on WHERE it was struck (the sawmill takes only the end it waits for). Its
    // Unit, Verdict and Final are not settled yet: the verdict is what the gates and effects decide.
    public interface IHitGate
    {
        bool TryConsume(Unit unit, in HitReport hit);
    }
}
