// Despawns the hitting unit on contact (collector/absorber buildings)
public class ConsumeEffect : ICollisionEffect
{
    public UnitType requiredUnitType;

    private readonly SpawnSystem spawnSystem;

    public ConsumeEffect(SpawnSystem spawnSystem)
    {
        this.spawnSystem = spawnSystem;
    }

    public void OnHit(Unit unit, CollisionTarget target)
    {
        // TODO: if unit.Type != requiredUnitType return; spawnSystem.DespawnUnit(unit)
    }
}
