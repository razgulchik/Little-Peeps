using UnityEngine;

public struct CollisionEvent
{
    public Unit Unit;
    public Building Building;
    public CollisionEvent(Unit unit, Building building) { Unit = unit; Building = building; }
}

public struct ResourceChangedEvent
{
    public ResourceType ResourceType;
    public float NewValue;
}

public struct BuildingPlacedEvent
{
    public Building Building;
    public Vector2Int Cell;
}

public struct BuildingRemovedEvent
{
    public Building Building;
    public Vector2Int Cell;
}

public struct BuildingDamagedEvent
{
    public Building Building;
    public float Damage;
}

public struct BuildingDestroyedEvent
{
    public Building Building;
}

public struct AgeStartedEvent
{
    public int Age;
}

public struct UnitSpawnedEvent
{
    public Unit Unit;
}

public struct UnitDespawnedEvent
{
    public Unit Unit;
}

public struct UnitBoostedEvent
{
    public Unit Unit;
    public float SpeedMultiplier;
    public float Radius;
    public float Duration;
}

public struct PerkSelectedEvent
{
    public PerkDef Perk;
}

public struct PrestigeTriggeredEvent { }
