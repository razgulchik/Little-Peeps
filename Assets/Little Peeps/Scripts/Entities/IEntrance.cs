namespace LittlePeeps
{
    // A building a unit can go INTO: a house to rest, a tavern for a drink. CollisionTarget asks every
    // entrance on the target first, whatever the unit's state, and a unit that is let in is done with the
    // hit — it is inside now, nothing else on the target sees it. WHO may come in is the building's own
    // door rule, because every building has a different one (a house takes the tired and the drunk; a
    // tavern anyone sober), so an entrance checks the unit's state itself. What stays central is the
    // other half: a unit no entrance took goes on to the gates and effects only while it is working
    // (CollisionTarget.HandleHit), so no ICollisionEffect or IHitGate ever has to ask about stamina.
    //
    // TryEnter decides and takes the unit in one step, like IHitGate.TryConsume: true = the unit is off the
    // field by the time it returns. A refusal is a plain bounce. Implemented by Spawner (the house) and
    // Tavern.
    public interface IEntrance
    {
        bool TryEnter(Unit unit);
    }
}
