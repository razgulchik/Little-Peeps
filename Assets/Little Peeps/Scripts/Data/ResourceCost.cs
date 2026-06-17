using System;

// Serializable resource-amount pair used in StructureDef.cost and AgeDef.resourceCost
[Serializable]
public class ResourceCost
{
    public ResourceType resourceType;
    public float amount;
}
