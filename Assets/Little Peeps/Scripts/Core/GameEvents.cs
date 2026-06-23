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

// Edge-placed structures (fences) have no single occupying cell, so they announce themselves by the
// Edge they sit on instead of a Cell. Parallel to StructurePlaced/RemovedEvent (the cell path).
public struct EdgeStructurePlacedEvent
{
    public Structure Structure;
    public Edge Edge;
}

public struct EdgeStructureRemovedEvent
{
    public Structure Structure;
    public Edge Edge;
}

// A spawner could not launch because every perimeter direction is blocked (map edge, a neighbouring
// structure, or a fence). Published ONCE on the transition into the blocked state; the resting unit
// stays inside and the spawner keeps retrying. Cleared by SpawnerUnblockedEvent on the next launch.
public struct SpawnerBlockedEvent
{
    public Structure Structure;
}

// A previously blocked spawner found an open direction again and resumed launching. Published once on
// the transition out of the blocked state (mirror of SpawnerBlockedEvent) so UI can drop its marker.
public struct SpawnerUnblockedEvent
{
    public Structure Structure;
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

// Published by the build-mode toggle button OR the build hotkey; handled by GameplayContainerState.
public struct BuildModeToggleRequestedEvent { }

// Published by the sell hotkey (GameHotkeys). BuildPanelUI toggles its Sell tool when open, so the
// button highlight and the PlacementController stay in sync; a no-op outside build mode (panel hidden).
public struct SellModeRequestedEvent { }

// Published by the exit-to-menu hotkey (GameHotkeys). GameBootstrap transitions the app FSM to MainMenu.
public struct ExitToMenuRequestedEvent { }

// Published by the info hotkey (GameHotkeys). Toggles the info window; a subscriber is added when that UI exists.
public struct InfoToggleRequestedEvent { }

// Pushed by GameplayContainerState so the toggle button reflects mode + cooldown.
public struct BuildModeUIStateEvent
{
    public bool InBuildMode;   // true → button shows the resume/play icon (click resumes)
    public bool Interactable;  // false while the 5s post-exit cooldown is running
}

// Published by PlacementController when the player tries to build on a valid cell but can't
// afford it; the BuildPanelUI plays a cue on the selected card.
public struct BuildDeniedEvent
{
    public StructureDef Def;
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
