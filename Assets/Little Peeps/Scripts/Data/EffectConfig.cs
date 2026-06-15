using System;

// Serializable config attached to BuildingDef; converted to ICollisionEffect at runtime
[Serializable]
public class EffectConfig
{
    public string effectType;   // matches class name: "ResourceEffect", "BouncePadEffect", etc.
    public float value;
    public float duration;
    public UnitType unitMask;
    public ResourceType resourceType;
}
