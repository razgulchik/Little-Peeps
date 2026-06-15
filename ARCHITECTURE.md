# Little Peeps — Architecture

## Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│  UI  (ResourceUI · AgeUI · PerkSelectionUI)                │
│  Subscribes to ReactiveValue<T>.OnChanged and EventBus<T>  │
└──────────────────────────┬──────────────────────────────────┘
                           │ reads / calls
┌──────────────────────────▼──────────────────────────────────┐
│  States  (AppState FSM / GameplayState FSM)                 │
│  Commands  (PlaceBuilding · MoveBuilding · etc.)            │
└──────────────────────────┬──────────────────────────────────┘
                           │ calls
┌──────────────────────────▼──────────────────────────────────┐
│  Systems  (IslandSystem · ResourceSystem · BuildingSystem   │
│           DragController · TapSystem · AgeSequencer · etc.) │
└──────┬────────────────────────────────────────┬────────────-┘
       │ uses                                   │ uses
┌──────▼──────────┐                  ┌──────────▼──────────────┐
│  Entities       │                  │  Effects                │
│  Unit·Building  │                  │  ICollisionEffect + impls│
│  Spawner·Pier   │                  └─────────────────────────┘
└──────┬──────────┘
       │ uses
┌──────▼────────────────────────────────────────────────────--┐
│  Core  (EventBus<T> · StateMachine · ReactiveValue<T>)      │
│  Core/Context  (RunContext · MetaContext · SessionContext)   │
└──────┬──────────────────────────────────────────────────────┘
       │ defined by
┌──────▼──────────────────────────────────────────────────────┐
│  Data  (ScriptableObjects + Enums)                          │
│  BuildingDef · UnitDef · PerkDef · AgeDef · etc.            │
└─────────────────────────────────────────────────────────────┘
```

Rule: layers only depend downward. UI never writes to Systems directly — it reads ReactiveValues and fires events.

---

## Three Contexts

### RunContext — resets on prestige
**Owner:** RunManager creates it in `StartNewRun()`; most systems receive it at init.

| Field | Type | Purpose |
|-------|------|---------|
| `resources` | `Dictionary<ResourceType, float>` | Current resource amounts |
| `buildings` | `Dictionary<Vector2Int, BuildingInstance>` | Live building registry |
| `currentAge` | `int` | Age index (0-based) |
| `perksChosen` | `List<PerkDef>` | Perks already applied this run |

**Lifecycle:** Created → populated by RunManager (with GlobalUpgrade multipliers) → mutated by ResourceSystem / BuildingSystem / PerkSystem → discarded on `ExecutePrestige()`.

### MetaContext — persists to disk (JSON)
**Owner:** SaveSystem loads it; PrestigeSystem writes it; RunManager reads multipliers from it.

| Field | Type | Purpose |
|-------|------|---------|
| `prestigePoints` | `int` | Currency for global upgrades |
| `globalUpgrades` | `Dictionary<UpgradeId, int>` | Level of each purchased upgrade |

**Lifecycle:** Loaded on Boot → mutated by MetaUpgradesState (spend points) and PrestigeSystem (earn points) → saved to disk whenever either mutates it.

**Serialization note:** `Dictionary<>` is not supported by `JsonUtility`. SaveSystem must convert `globalUpgrades` to/from a `List<UpgradeLevelPair>` for JSON round-trips.

### SessionContext — runtime only, never saved
**Owner:** GameBootstrap creates it; DragController, TapSystem, and others read/write it.

| Field | Type | Purpose |
|-------|------|---------|
| `unitPool` | `UnitPool` | Reference for any system that needs to spawn/despawn units |
| `draggedBuilding` | `Building` | Building currently following the cursor in BuildMode |
| `hoveredCell` | `Vector2Int?` | Grid cell under the cursor (for hover highlights) |

**Lifecycle:** Created on Awake → fields set/cleared as the player interacts → discarded when the scene reloads.

---

## EventBus

`EventBus<T>` is a generic static publish-subscribe bus. Each event type gets its own static subscriber list.

```csharp
// Subscribe
EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);

// Publish
EventBus<AgeStartedEvent>.Publish(new AgeStartedEvent { Age = 2 });

// Unsubscribe (always do this in OnDisable to avoid phantom callbacks)
EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);
```

**Event catalogue:**

| Struct | Published by | Consumed by |
|--------|-------------|-------------|
| `CollisionEvent` | `Building.OnCollisionEnter2D` | debug/analytics |
| `ResourceChangedEvent` | `ResourceSystem.AddResource` | ResourceUI |
| `BuildingPlacedEvent` | `BuildingSystem.PlaceBuilding` | RunContext, AgeUI |
| `BuildingRemovedEvent` | `BuildingSystem.RemoveBuilding` | RunContext |
| `BuildingDamagedEvent` | `Building.TakeDamage` | UI health bars |
| `BuildingDestroyedEvent` | `Building.TakeDamage` | BuildingSystem cleanup |
| `AgeStartedEvent` | `AgeSequencer.ShowAgeTitle` | IslandSystem, AgeUI |
| `UnitSpawnedEvent` | `SpawnSystem.SpawnUnit` | UnitSystem |
| `UnitDespawnedEvent` | `SpawnSystem.DespawnUnit` | UnitSystem |
| `UnitBoostedEvent` | `TapSystem.Update` | analytics/VFX |
| `PerkSelectedEvent` | `PerkSystem.ApplyPerk` | AgeSequencer (unblock coroutine), PerkSelectionUI |
| `PrestigeTriggeredEvent` | `Pier.OnTriggerEnter2D` | PlayingState (opens PrestigeMenu) |

---

## State Machines

### App FSM  (`appStateMachine` in GameBootstrap)

```
Boot ──load complete──▶ MainMenu
MainMenu ──play──▶ GameplayContainer
MainMenu ──meta──▶ MetaUpgrades
MetaUpgrades ──back──▶ MainMenu
GameplayContainer ──prestige complete──▶ MainMenu
```

### Gameplay FSM  (`innerFsm` inside GameplayContainerState)

```
Playing ──build button──▶ BuildMode
BuildMode ──close / Escape──▶ Playing
Playing ──age condition met──▶ AgeTransition
AgeTransition ──sequencer done──▶ Playing  (PerkSelection is internal to AgeSequencer)
Playing ──PrestigeTriggeredEvent──▶ PrestigeMenu
PrestigeMenu ──confirm──▶ (RunManager.StartNewRun → back to Playing via GameplayContainer.Enter)
PrestigeMenu ──cancel──▶ Playing
```

---

## Physics Setup

| Object | Collider | Rigidbody | Notes |
|--------|----------|-----------|-------|
| Unit | CircleCollider2D | Rigidbody2D (Dynamic) | Bounces off obstacles; passes through interactables |
| Building (obstacle) | BoxCollider2D `isTrigger=false` | none (Static) | Unit bounces — `OnCollisionEnter2D` fires |
| Building (interactable) | BoxCollider2D `isTrigger=true` | none (Static) | Unit passes through — `OnTriggerEnter2D` fires; `SetColliderEnabled(false)` during drag |
| Island boundary | TilemapCollider2D (Composite Operation: Merge) + CompositeCollider2D | Rigidbody2D (Static) | Auto-updates when tiles are added on island expansion |
| Pier | CircleCollider2D (isTrigger) | none | Own physics layer so units ignore it; player clicks to trigger prestige |

**Obstacle vs Interactable** — the distinction is set via `isTrigger` on the prefab's collider. `Building.OnCollisionEnter2D` and `Building.OnTriggerEnter2D` both call the same `HandleHit(unit)` → `effects.ForEach(...)` + `CollisionEvent`. No separate CollisionSystem.

**No CompositeCollider2D on buildings** — each has its own BoxCollider2D so they can be enabled/disabled individually during drag.

**Pier click** — detected by `TapSystem` via `Physics2D.OverlapPoint` raycast; Pier is on a separate physics layer excluded from unit collision.

---

## Systems Overview

| System | Type | Responsibility |
|--------|------|---------------|
| `IslandGrid` | Plain C# | Grid data: cells, placement validation, world↔grid conversion |
| `IslandGenerator` | Plain C# | Procedural terrain fill and expansion per age |
| `IslandSystem` | MB | Owns Grid + Generator; reacts to AgeStarted |
| `UnitPool` | MB | `ObjectPool<Unit>` per UnitDef; Get / Release |
| `UnitSystem` | MB | Active-unit registry; driven by Spawn/Despawn events |
| `SpawnSystem` | MB | Bridges Spawner components ↔ UnitPool; publishes Spawn/Despawn |
| `ResourceSystem` | MB | `ReactiveValue<float>` per resource; AddResource / GetResource |
| `BuildingSystem` | MB | Place / Remove / Move via IslandGrid; publishes building events |
| `DragController` | MB | Drag-and-drop in BuildMode; writes to SessionContext |
| `TapSystem` | MB | Click-on-unit → Boost via New Input System |
| `AgeSequencer` | MB | Explicit 6-step coroutine chain for age transitions |
| `PerkSystem` | MB | Weighted random roll of 3 perks; ApplyPerk |
| `PrestigeSystem` | MB | Points formula; resets run via RunManager |
| `RunManager` | MB | Creates RunContext; applies MetaContext multipliers |
| `SaveSystem` | MB | JSON serialization of MetaContext to persistentDataPath |
