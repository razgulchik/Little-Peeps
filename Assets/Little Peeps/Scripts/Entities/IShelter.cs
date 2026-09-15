namespace LittlePeeps
{
    // The other side of ICollisionEffect: what a TIRED unit's hit reaches. CollisionTarget sends a hit
    // down exactly one of the two paths by the unit's state — a working unit gets the gates and the
    // effects, a tired one only the shelters — so "working units can't go home, tired units can't
    // work" is decided once, in HandleHit, and no effect or gate ever has to ask about fatigue.
    // Implemented by Spawner: a tired unit that reaches a house of its type is taken in to rest.
    public interface IShelter
    {
        void OnTiredHit(Unit unit);
    }
}
