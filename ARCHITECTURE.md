# Little Peeps — Architecture

> Status note: the architecture skeleton is in place; several systems are still stubs.
> Build mode is implemented through **Phase 1** (mode toggle / pause / despawn-respawn).
> Placement, age flow, prestige, save, and the main-menu states are not wired yet — these
> are called out with **(stub)** / **(planned)** / **(Phase N)** below.

## Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│  UI  (ResourceUI · AgeUI · PerkSelectionUI · BuildModeButton)│
│  Subscribes to ReactiveValue<T>.OnChanged and EventBus<T>    │
└──────────────────────────┬──────────────────────────────────┘
                           │ reads / fires events
┌──────────────────────────▼──────────────────────────────────┐
│  States  (AppState FSM / GameplayState FSM)                  │
│  Commands  (PlaceStructure · MoveStructure · DestroyStructure)│
└──────────────────────────┬──────────────────────────────────┘
                           │ calls
┌──────────────────────────▼──────────────────────────────────┐
│  Systems  (IslandSystem · ResourceSystem · StructureSystem · │
│   SpawnSystem · UnitSystem · TapSystem · AgeSequencer · …)    │
└──────┬────────────────────────────────────────┬─────────────┘
       │ uses                                   │ dispatches to
┌──────▼────────────────────────┐    ┌──────────▼──────────────┐
│  Entities                     │    │  Effects                │
│  CollisionTarget (base)       │    │  ICollisionEffect + impls│
│   ├ Structure                 │    │  (Spawner, ResourceSource│
│  Unit · Spawner               │    │   are MB impls; the      │
│  ResourceSource · Pier        │    │   Effects/ classes are   │
└──────┬────────────────────────┘    │   legacy plain-class stubs)│
       │ uses                        └─────────────────────────┘
┌──────▼──────────────────────────────────────────────────────┐
│  Core  (EventBus<T> · StateMachine · ReactiveValue<T>)       │
│  Core/Context  (RunContext · MetaContext · SessionContext)   │
└──────┬───────────────────────────────────────────────────────┘
       │ defined by
┌──────▼───────────────────────────────────────────────────────┐
│  Data  (ScriptableObjects + Enums)                           │
│  StructureDef · UnitDef · ResourceSourceDef · PerkDef · AgeDef│
└──────────────────────────────────────────────────────────────┘
```

Rule: layers only depend downward. UI never writes to Systems directly — it reads ReactiveValues and fires events.

---

## Unified "Structure" model

Everything that stands on the grid is a **Structure**; behaviour lives in components:

- `CollisionTarget` (base MonoBehaviour) — owns the collision callbacks, dispatches hits to its
  `ICollisionEffect` components, and publishes the global `CollisionEvent`.
- `Structure : CollisionTarget` — adds placement identity: `def` (`StructureDef`) + health.
- A structure carries one or both behaviour components:
  - `Spawner` (produces units) — `[RequireComponent(typeof(Structure))]`.
  - `ResourceSource` (produces resources) — `[RequireComponent(typeof(CollisionTarget))]`.
- Examples: House/Hut = Structure + Spawner · Forge/Church = Structure + ResourceSource (infinite) ·
  Tree/Wheat/Stone = Structure + ResourceSource (natural; **adding the `Structure` component to
  these prefabs is Phase 2 editor work**).

Units split into gatherers (Farmer/Lumberjack/Hunter/Miner) and source-creatures (Alpaca/Boar/Fox,
which run as units but are sources — harvest needs Unit×Unit collision, **deferred**); military
(Swordsman) is a later third type.

---

## Three Contexts

### RunContext — resets on prestige
**Owner:** RunManager creates it in `StartNewRun()`; most systems receive it at init.

| Field | Type | Purpose |
|-------|------|---------|
| `resources` | `Dictionary<ResourceType, float>` | Current resource amounts |
| `structures` | `Dictionary<Vector2Int, StructureInstance>` | Live structure registry (cell → instance) |
| `currentAge` | `int` | Age index (0-based) |
| `perksChosen` | `List<PerkDef>` | Perks already applied this run |

`StructureInstance` = `Def` (`StructureDef`) + `RuntimeObject` (`Structure`) + `Cell`.

**Lifecycle:** Created → populated by RunManager (with GlobalUpgrade multipliers) → mutated by ResourceSystem / StructureSystem / PerkSystem → discarded on `ExecutePrestige()`.

### MetaContext — persists to disk (JSON)
**Owner:** SaveSystem loads it; PrestigeSystem writes it; RunManager reads multipliers from it.

| Field | Type | Purpose |
|-------|------|---------|
| `prestigePoints` | `int` | Currency for global upgrades |
| `globalUpgrades` | `Dictionary<UpgradeId, int>` | Level of each purchased upgrade |

**Lifecycle:** Loaded on Boot → mutated by MetaUpgradesState (spend points) and PrestigeSystem (earn points) → saved to disk whenever either mutates it. *(SaveSystem.Load currently returns a fresh MetaContext — real JSON persistence is planned.)*

**Serialization note:** `Dictionary<>` is not supported by `JsonUtility`. SaveSystem must convert `globalUpgrades` to/from a `List<UpgradeLevelPair>` for JSON round-trips.

### SessionContext — runtime only, never saved
**Owner:** GameBootstrap creates it; DragController / placement and others read/write it.

| Field | Type | Purpose |
|-------|------|---------|
| `unitPool` | `UnitPool` | Reference for any system that needs to spawn/despawn units |
| `draggedStructure` | `Structure` | Structure currently following the cursor in BuildMode |
| `hoveredCell` | `Vector2Int?` | Grid cell under the cursor (for hover highlights) |

**Lifecycle:** Created on Awake → fields set/cleared as the player interacts → discarded when the scene reloads.

---

## EventBus

`EventBus<T>` is a generic static publish-subscribe bus, allocation-free on `Publish` (a cached
subscriber array is rebuilt only when the subscriber set changes). Each event type gets its own
static subscriber list.

```csharp
EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);
EventBus<AgeStartedEvent>.Publish(new AgeStartedEvent { Age = 2 });
EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);   // always unsubscribe in OnDisable/Exit
```

**Event catalogue:**

| Struct | Published by | Consumed by |
|--------|-------------|-------------|
| `CollisionEvent` (carries `CollisionTarget Target`) | `CollisionTarget.HandleHit` | none yet — kept for future *global* listeners. Per-hit effects (Spawner/ResourceSource) are dispatched **locally** by CollisionTarget, not via this event. |
| `ResourceChangedEvent` | `ResourceSystem.AddResource` | ResourceUI |
| `StructurePlacedEvent` | `StructureSystem.PlaceStructure` (stub) | RunContext, AgeUI |
| `StructureRemovedEvent` | `StructureSystem.RemoveStructure` (stub) | RunContext |
| `StructureDamagedEvent` | `Structure.TakeDamage` (stub) | UI health bars |
| `StructureDestroyedEvent` | `Structure.TakeDamage` (stub) | StructureSystem cleanup |
| `AgeStartedEvent` | `AgeSequencer` (planned) | IslandSystem, AgeUI |
| `UnitBoostedEvent` | `TapSystem` | analytics / VFX |
| `PerkSelectedEvent` | `PerkSystem.ApplyPerk` | AgeSequencer, PerkSelectionUI |
| `PrestigeTriggeredEvent` | `TapSystem` (Pier click, planned) | gameplay coordinator → PrestigeMenu |
| `BuildModeToggleRequestedEvent` | `BuildModeButton` (click) | `GameplayContainerState` |
| `BuildModeUIStateEvent` (`InBuildMode`, `Interactable`) | `GameplayContainerState` | `BuildModeButton` (icon swap + interactable) |

> **Unit spawn/despawn use NO events.** `SpawnSystem` keeps `UnitSystem`'s live registry in sync
> via direct `unitSystem.Add` / `Remove` calls (single consumer, rare population changes — building
> enter/exit is rest/launch, not spawn/despawn). The old `UnitSpawnedEvent`/`UnitDespawnedEvent`
> were removed.

---

## State Machines

### App FSM  (`appStateMachine` in GameBootstrap)

```
Boot ──load complete──▶ MainMenu        (MainMenu/MetaUpgrades planned, not wired)
MainMenu ──play──▶ GameplayContainer
MainMenu ──meta──▶ MetaUpgrades
MetaUpgrades ──back──▶ MainMenu
GameplayContainer ──prestige complete──▶ MainMenu
```

**Current path:** GameBootstrap.Awake pushes `Boot`, then transitions straight to
`GameplayContainer` (MainMenu is skipped until its UI exists).

### Gameplay FSM  (`innerFsm` inside `GameplayContainerState`)

```
Playing ──BuildModeToggleRequestedEvent──▶ BuildMode
BuildMode ──BuildModeToggleRequestedEvent──▶ Playing  (then 5s re-entry cooldown)
Playing ──age condition met──▶ AgeTransition          (planned)
AgeTransition ──sequencer done──▶ Playing             (planned)
Playing ──PrestigeTriggeredEvent──▶ PrestigeMenu      (planned)
```

**`GameplayContainerState` is the build-mode coordinator:** it subscribes to
`BuildModeToggleRequestedEvent`, owns the **5s re-entry cooldown** (unscaled time, blocks
re-entering build mode right after leaving — anti-respawn-abuse), switches the inner FSM between
`PlayingState` and `BuildModeState`, and pushes `BuildModeUIStateEvent` so the button reflects
mode + cooldown.

**`BuildModeState`:** on `Enter` sets `Time.timeScale = 0` and calls
`SpawnSystem.DespawnAllAndResetSpawners()` (all units returned to the pool); on `Exit` calls
`SpawnSystem.WarmupAllSpawners()` (units respawn from their structures) and restores
`Time.timeScale = 1`. Placement / ghost / grid overlay arrive in Phase 2.

---

## Physics Setup

| Object | Collider | Rigidbody | Notes |
|--------|----------|-----------|-------|
| Unit | CircleCollider2D | Rigidbody2D (Dynamic) | Bounces off obstacles; passes through interactables |
| Structure (obstacle) | BoxCollider2D `isTrigger=false` | none (Static) | Unit bounces — `OnCollisionEnter2D` fires on the `CollisionTarget` |
| Structure (interactable) | BoxCollider2D `isTrigger=true` | none (Static) | Unit passes through — `OnTriggerEnter2D` fires; `SetColliderEnabled(false)` during drag / on source depletion |
| Island boundary | TilemapCollider2D (Composite Operation: Merge) + CompositeCollider2D | Rigidbody2D (Static) | Auto-updates when tiles are added on island expansion |
| Pier | CircleCollider2D (isTrigger) | none | Own physics layer so units ignore it; player clicks to trigger prestige |

**Collision dispatch lives in `CollisionTarget`** (the base). Both `OnCollisionEnter2D` (obstacle
path) and `OnTriggerEnter2D` (interactable path) call the same `HandleHit(unit)` →
`effect.OnHit(...)` for each `ICollisionEffect` component on the target → publish `CollisionEvent`.
`Structure : CollisionTarget` adds `def`/health. The Rigidbody2D sits on the root so callbacks
fire there; the collider may live on a child (fetched via `GetComponentInChildren`).

**Obstacle vs Interactable** — set via `isTrigger` on the prefab's collider; same `HandleHit` path
for both. No separate CollisionSystem.

**Per-structure colliders (no CompositeCollider2D on structures)** — each has its own BoxCollider2D
so it can be enabled/disabled individually during drag and on resource-source depletion.

**Build-mode pause** uses `Time.timeScale = 0` (not `Physics2D.simulationMode`); units are
despawned on enter, so there is nothing to simulate anyway.

---

## Systems & Entities Overview

| Component | Type | Responsibility |
|-----------|------|---------------|
| `IslandGrid` | Plain C# | Grid data: cells (`terrain` + `occupant: StructureInstance`), placement validation (`CanPlace/Place/Remove/Move` — **stubs, Phase 2**), world↔grid conversion |
| `IslandGenerator` | Plain C# | Procedural terrain fill / expansion per age (**currently fills all cells Grass**) |
| `IslandSystem` | MB | Owns Grid + Generator; `GenerateForRun()`; reacts to AgeStarted (TODO) |
| `UnitPool` | MB | Pool per UnitDef; `Get` / `Release` |
| `UnitSystem` | MB | Live-unit registry (`ActiveUnits`); fed by **direct `SpawnSystem` Add/Remove** (no events); home for future bulk ops (tap-AoE) |
| `SpawnSystem` | MB | Bridges Spawner ↔ UnitPool; per-type cap; syncs UnitSystem; owns the **spawner registry**; `DespawnAllAndResetSpawners` / `WarmupAllSpawners` (build mode) |
| `ResourceSystem` | MB | `ReactiveValue<float>` per resource; `AddResource` / `GetResource` |
| `StructureSystem` | MB | Place / Remove / Move structures via IslandGrid (**stubs, Phase 2**); publishes Structure* events |
| `DragController` | MB | **Legacy** drag-and-drop stub for BuildMode; being replaced by a click-based `PlacementController` in Phase 2 |
| `TapSystem` | MB | Click-on-unit → `Boost`; Pier click → prestige (New Input System) |
| `AgeSequencer` | MB | Step coroutine chain for age transitions (planned) |
| `PerkSystem` | MB | Weighted random roll of perks; `ApplyPerk` |
| `PrestigeSystem` | MB | Points formula; resets run via RunManager |
| `RunManager` | MB | Creates RunContext; applies MetaContext multipliers; owns island generation timing |
| `SaveSystem` | MB | JSON serialization of MetaContext (**stub: returns fresh MetaContext**) |
| `CollisionTarget` | MB (base) | Collision callbacks + `ICollisionEffect` dispatch + `CollisionEvent`; `SetColliderEnabled` |
| `Structure` | MB : CollisionTarget | Placement identity: `def` (StructureDef) + health / `TakeDamage` (stub) |
| `Spawner` | MB, `ICollisionEffect` | Per-slot spawn → travel → rest cycle; self-registers with SpawnSystem; `ResetSlots` / `BeginWarmup` |
| `ResourceSource` | MB, `ICollisionEffect` | Resource node: grants `def.resource` per allowed-worker hit, depletes/respawns |
| `BuildModeButton` | MB (UI) | Toggle button: publishes `BuildModeToggleRequestedEvent`, reflects `BuildModeUIStateEvent` |
