using UnityEngine;

public struct CollisionEvent
{
    public Unit Unit;
    public CollisionTarget Target;
    public CollisionEvent(Unit unit, CollisionTarget target) { Unit = unit; Target = target; }
}

public struct ResourceChangedEvent
{
    public ResourceType ResourceType;
    public float NewValue;
}

public struct StructurePlacedEvent
{
    public Structure Structure;
    public Vector2Int Cell;
}

public struct StructureRemovedEvent
{
    public Structure Structure;
    public Vector2Int Cell;
}

public struct StructureDamagedEvent
{
    public Structure Structure;
    public float Damage;
}

public struct StructureDestroyedEvent
{
    public Structure Structure;
}

public struct AgeStartedEvent
{
    public int Age;
}

// Published by the build-mode toggle button; handled by GameplayContainerState.
public struct BuildModeToggleRequestedEvent { }

// Pushed by GameplayContainerState so the toggle button reflects mode + cooldown.
public struct BuildModeUIStateEvent
{
    public bool InBuildMode;   // true → button shows the resume/play icon (click resumes)
    public bool Interactable;  // false while the 5s post-exit cooldown is running
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
