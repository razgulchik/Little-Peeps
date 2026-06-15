// Damages the building on each hit (obstacles that wear down over time)
public class DamageEffect : ICollisionEffect
{
    public float damageAmount;
    public UnitType requiredUnitType;

    public void OnHit(Unit unit, Building building)
    {
        // TODO: if unit.Type != requiredUnitType return; building.TakeDamage(damageAmount)
    }
}
