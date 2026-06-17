using System;

// Serializable config attached to StructureDef; converted to ICollisionEffect at runtime
[Serializable]
public class EffectConfig
{
    public string effectType;   // matches class name: "ResourceEffect", "BouncePadEffect", etc.
    public float value;
    public float duration;
    public UnitType unitMask;
    public ResourceType resourceType;
}
