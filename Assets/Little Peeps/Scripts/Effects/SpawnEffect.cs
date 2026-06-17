// Spawns a new unit at the building's position when hit by the required type
public class SpawnEffect : ICollisionEffect
{
    public UnitDef unitToSpawn;
    public UnitType requiredUnitType;

    private readonly SpawnSystem spawnSystem;

    public SpawnEffect(SpawnSystem spawnSystem)
    {
        this.spawnSystem = spawnSystem;
    }

    public void OnHit(Unit unit, CollisionTarget target)
    {
        // TODO: if unit.Type != requiredUnitType return; spawnSystem.SpawnUnit(unitToSpawn, target.transform.position)
    }
}
