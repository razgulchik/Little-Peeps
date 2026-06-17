// Damages the structure on each hit (obstacles that wear down over time)
public class DamageEffect : ICollisionEffect
{
    public float damageAmount;
    public UnitType requiredUnitType;

    public void OnHit(Unit unit, CollisionTarget target)
    {
        // TODO: if unit.Type != requiredUnitType return; (target as Structure)?.TakeDamage(damageAmount)
    }
}
