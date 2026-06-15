// Strategy interface for effects triggered when a unit collides with a building.
// Masking (unit type check) must be implemented inside OnHit.
public interface ICollisionEffect
{
    void OnHit(Unit unit, Building building);
}
