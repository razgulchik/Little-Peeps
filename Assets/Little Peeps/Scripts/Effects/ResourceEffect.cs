// Grants a fixed resource amount on each hit by the required unit type
public class ResourceEffect : ICollisionEffect
{
    public ResourceType resourceType;
    public float amount;
    public UnitType requiredUnitType;

    private readonly ResourceSystem resourceSystem;

    public ResourceEffect(ResourceSystem resourceSystem)
    {
        this.resourceSystem = resourceSystem;
    }

    public void OnHit(Unit unit, CollisionTarget target)
    {
        // TODO: if unit.Type != requiredUnitType return; resourceSystem.AddHarvest(resourceType, unit.Type, amount)
        //       (AddHarvest routes the gain through the yield + production modifiers)
    }
}
