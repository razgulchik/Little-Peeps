# Little Peeps — Architecture

> Status note: the architecture skeleton is in place; several systems are still stubs.
> Build mode is implemented through **Phase 1** (mode toggle / pause / despawn-respawn).
> The **age flow + RunStats bonus system are implemented** (buy age → spend → grow island →
> apply stat modifiers → fade/banner transition; perk pick inside it is still a hook).
> **Animals (mobile resource nodes) are implemented** in code (Animal / AnimalWander /
> AnimalSpawner + IStructureSpawner); their prefabs/defs are editor work. Placement,
> prestige, save, and the main-menu states are not wired yet — called out with **(stub)** /
> **(planned)** / **(Phase N)** below.

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
│   SpawnSystem · UnitSystem · TapSystem · AgeSystem ·          │
│   AgeSequencer · …)                                          │
└──────┬────────────────────────────────────────┬─────────────┘
       │ uses                                   │ dispatches to
┌──────▼────────────────────────┐    ┌──────────▼──────────────┐
│  Entities                     │    │  Effects                │
│  CollisionTarget (base)       │    │  ICollisionEffect + impls│
│   ├ Structure                 │    │  (Spawner, ResourceSource,│
│  Unit · Spawner · Pier        │    │   Animal are MB impls; the│
│  ResourceSource · Animal      │    │   Effects/ classes are   │
│  AnimalSpawner · AnimalWander │    │   legacy plain-class stubs)│
│  IStructureSpawner (contract) │    └─────────────────────────┘
└──────┬────────────────────────┘
       │ uses
┌──────▼──────────────────────────────────────────────────────┐
│  Core  (EventBus<T> · StateMachine · ReactiveValue<T>)       │
│  Core/Context  (RunContext · MetaContext · SessionContext)   │
└──────┬───────────────────────────────────────────────────────┘
       │ defined by
┌──────▼───────────────────────────────────────────────────────┐
│  Data  (ScriptableObjects + Enums)                           │
│  StructureDef · UnitDef · ResourceSourceDef · PerkDef ·      │
│  AgeDef · StatModifier · StatId                              │
└──────────────────────────────────────────────────────────────┘
```

Rule: layers only depend downward. UI never writes to Systems directly — it reads ReactiveValues and fires events.

---

## Unified "Structure" model

Everything that stands on the grid is a **Structure**; behaviour lives in components:

- `CollisionTarget` (base MonoBehaviour) — owns the collision callbacks, dispatches hits to its
  `ICollisionEffect` components, and publishes the global `CollisionEvent`.
- `Structure : CollisionTarget` — adds placement identity: `def` (`StructureDef`) + health.
- A structure carries one (or more) behaviour components:
  - `Spawner` (produces units) — `[RequireComponent(typeof(Structure))]`.
  - `ResourceSource` (produces resources) — `[RequireComponent(typeof(CollisionTarget))]`.
  - `AnimalSpawner` (produces animals — mobile resource nodes) — `[RequireComponent(typeof(Structure))]`.
- Examples: House/Hut = Structure + Spawner · Forge/Church = Structure + ResourceSource (infinite) ·
  Tree/Wheat/Stone = Structure + ResourceSource (natural; **adding the `Structure` component to
  these prefabs is Phase 2 editor work**) · Stable/Den = Structure + AnimalSpawner.

Units are gatherers (Farmer/Lumberjack/Hunter/Miner); military (Swordsman) is a later second kind.
**Animals (Alpaca/Boar/Fox) are NOT units** — they are mobile resource nodes: a standalone
`CollisionTarget` (not a `Structure`: no def, no grid cell, no health — the first direct use of the
base class) + `Animal` (harvest via the same `ResourceSourceDef` pipeline as static sources;
resource per hit, despawns after `hitsBeforeDespawn`, `infinite` never despawns) + `AnimalWander`
(kinematic wandering). Their `UnitType` entries were removed; an animal is identified by its
`ResourceSourceDef` asset, like a tree.

Both spawner kinds implement **`IStructureSpawner`** (`ResetForBuildMode` / `Warmup`) —
`SpawnSystem`'s registry drives build-mode transitions through the interface. Internals are
deliberately NOT shared: the unit launch→return→rest slot cycle and the animal
die→cooldown→replace cycle have nothing in common beyond that contract.

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
| `stats` | `RunStats` | Accumulated bonus layer (age/perk/meta modifiers); see below |

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
**Owner:** reserved for runtime session state — **currently not instantiated** (the DragController that used it was removed; PlacementController keeps its own state). Kept as the future home for session-scoped data.

| Field | Type | Purpose |
|-------|------|---------|
| `unitPool` | `UnitPool` | Reference for any system that needs to spawn/despawn units |
| `draggedStructure` | `Structure` | Structure currently following the cursor in BuildMode |
| `hoveredCell` | `Vector2Int?` | Grid cell under the cursor (for hover highlights) |

**Lifecycle:** none yet — no system creates or reads it at the moment. Its current fields are drag-era leftovers; revisit when real session state is added.

---

## Stat System (RunStats) — base + modifiers

Game parameters follow a **base + modifiers** split, so bonuses stay data-driven and reset cleanly on prestige:

- **Base** values live in configs and never change: `UnitDef.speed`, `ResourceSourceDef.workerYields[worker].amount`, etc.
- **Modifiers** accumulate on `RunContext.stats` (`RunStats`, plain C#), the per-run bonus layer. Sources (ages now; perks / meta later) just push `StatModifier` data; `RunStats` aggregates.
- **One formula, everywhere:** `final = (base + Σflat) * (1 + Σpercent)`.

`StatModifier` (Data, `[Serializable]`) = `id` (`StatId`) + `unitScope` (`UnitType`) + `resourceScope` (`ResourceType`) + `flat` + `percent`. `StatId` = `ProductionGlobal` (no scope) · `ResourceYield` (unit×resource) · `UnitSpeed` (unit). `StatMeta.ScopeOf` gives each id's scope mask; `RunStats` normalises the lookup key by that mask in **both** `Add` and `Apply`, so a stray/absent scope can never make authored data miss its query.

**Consumers (where base meets modifiers):**
- Harvest gains — `ResourceSystem.AddHarvest(type, worker, base)` applies `ResourceYield` then `ProductionGlobal`, then credits. `AddResource`/`Spend` stay raw (spends/refunds are never production-boosted). Only live harvest path today: `ResourceSource.OnHit`.
- Unit speed — `Unit.ResolveBaseSpeed()` = `stats.Apply(def.speed, UnitSpeed, type)`, resolved on each `Launch` (cached in `baseSpeed`; injected via `SpawnSystem` → `unit.SetStats`).

**Perf:** one O(1) dictionary hit on a struct key (`IEquatable`, no boxing). Reads can be per-hit (harvest); modifiers change only a couple of times per run. A dirty-flag value cache is the noted growth point if profiling ever needs it.

**Extending:** a bonus on an already-wired param = pure data (author a `StatModifier`). A new param = ~2 edits (one `StatId` entry + one `Apply` at the consumer). A new scope dimension (e.g. `StructureType`) = add a field to `RunStats.Key`/`MakeKey` once. Designer-facing details: `BONUS_SYSTEM_GUIDE.md`.

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
| `AgeStartedEvent` | `AgeSequencer` (banner step) | CameraController (re-clamp bounds), AgeUI (label + button), PierSystem (move pier to the new right edge) |
| `AgeAdvanceRequestedEvent` | `AgeUI` (Next Age button) | `GameplayContainerState` (enters AgeTransition if affordable) |
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
BuildMode ──BuildModeToggleRequestedEvent──▶ Playing      (then 5s re-entry cooldown)
Playing ──AgeAdvanceRequestedEvent (affordable)──▶ AgeTransition
AgeTransition ──sequencer done──▶ Playing
Playing ──PrestigeTriggeredEvent──▶ PrestigeMenu          (planned)
```

**`GameplayContainerState` is the gameplay coordinator:** it subscribes to
`BuildModeToggleRequestedEvent` and `AgeAdvanceRequestedEvent`, owns the **5s re-entry cooldown**
(unscaled time, blocks re-entering build mode right after leaving — anti-respawn-abuse), switches
the inner FSM between `PlayingState` / `BuildModeState` / `AgeTransitionState`, and pushes
`BuildModeUIStateEvent` so the button reflects mode + cooldown. On an age-advance request it enters
`AgeTransitionState` only from normal play and only when `AgeSystem.CanAdvance`.

**`AgeTransitionState`:** on `Enter` runs `TriggerAgeCmd` (spend cost + `currentAge++` +
`stats.Add(ageDef.modifiers)`), sets `Time.timeScale = 0` (this both freezes the sim AND makes
`TapSystem` ignore world clicks — it early-returns at timeScale 0, so no UI-raycast juggling is
needed), and starts the `AgeSequencer`; on completion it returns to `PlayingState` and restores
`timeScale = 1`. The sequencer chain (`AgeSequencer`, unscaled time): fade to black →
`IslandSystem.Expand(ageDef)` → "Age N" banner (+ `AgeStartedEvent`) → perk-pick **hook (no-op,
later)** → fade back.

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
| Animal (alpaca/boar/fox) | Collider2D `isTrigger=false` (child) | Rigidbody2D (**Kinematic**, root) | Units (Dynamic) bounce off it — that bounce IS the harvest hit; the kinematic body itself passes through structures/terrain (only wander destinations are validated, by AnimalSpawner) |
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
| `IslandGrid` | Plain C# | Grid data: cells (`terrain` + `occupant: StructureInstance`), placement validation (`CanPlace/Place/Remove/Move` — **stubs, Phase 2**), world↔grid conversion; `CellBounds(out min,out max)` = integer cell extent (used by `WorldBounds` and `PierSystem`'s right-edge snap) |
| `IslandGenerator` | Plain C# | Seeds the starting island (centered Grass square); `Expand(blocks)` adds `AgeDef.expansionBlocks` — absolute `RectInt`s, only missing cells created (biome variety later) |
| `IslandSystem` | MB | Owns Grid + Generator; `GenerateForRun()`; `Expand(AgeDef)` grows the island + redraws the tilemap (driven explicitly by `AgeSequencer`, not an event) |
| `UnitPool` | MB | Pool per UnitDef; `Get` / `Release` |
| `UnitSystem` | MB | Live-unit registry (`ActiveUnits`); fed by **direct `SpawnSystem` Add/Remove** (no events); home for future bulk ops (tap-AoE) |
| `SpawnSystem` | MB | Bridges Spawner ↔ UnitPool; per-type cap; syncs UnitSystem; owns the **spawner registry** (`IStructureSpawner`: unit Spawners + AnimalSpawners); `DespawnAllAndResetSpawners` / `WarmupAllSpawners` (build mode); `Initialize(RunContext)` → injects `RunStats` into each spawned unit |
| `ResourceSystem` | MB | `ReactiveValue<float>` per resource; `AddResource` / `GetResource`; `AddHarvest(type, worker, base)` = production gateway (applies `ResourceYield` + `ProductionGlobal`); `CanAfford` / `Spend` |
| `StructureSystem` | MB | Place / Remove / Move structures via IslandGrid (**stubs, Phase 2**); publishes Structure* events. `Build`/`PlaceInitial` return the created `StructureInstance` so owners (the pier) can track + move it later |
| `PierSystem` | MB | Owns the pier for a run: `PlaceForRun()` (called by RunManager after island gen) drops it in the island's bottom-right corner; on `AgeStartedEvent` it re-snaps to the new right edge via `StructureSystem` pick-up/drop. Anchors to the **rightmost column's own bottom** (ragged shorelines), warns if the age's right-edge growth is too short. Not part of `StartingLayoutDef` — single owner of the pier's cell |
| `PlacementController` | MB | BuildMode tool controller: ghost place / sell / move-drag + grid overlay; right-click cancels (replaced the old DragController) |
| `TapSystem` | MB | Click-on-unit → `Boost`; Pier click → prestige (New Input System) |
| `AgeSystem` | MB | Owns the ordered `List<AgeDef>` catalogue; `NextAge` / `CanAdvance` (queried by AgeUI + GameplayContainerState). Current age lives on RunContext, so it's stateless between runs |
| `AgeSequencer` | MB | Coroutine chain for an age transition (fade → island expand → "Age N" banner → perk hook → fade), unscaled time; signals completion via callback. Refs: fade `CanvasGroup` + title `TMP_Text` |
| `PerkSystem` | MB | Weighted random roll of perks; `ApplyPerk` (still stub; the age-transition perk step is a hook) |
| `PrestigeSystem` | MB | Points formula; resets run via RunManager |
| `RunManager` | MB | Creates RunContext; seeds `stats` (debug modifiers now, meta later); `Initialize`s resource/structure/**spawn** systems; owns island generation timing; after generation places starting structures + `PierSystem.PlaceForRun()` |
| `SaveSystem` | MB | JSON serialization of MetaContext (**stub: returns fresh MetaContext**) |
| `CollisionTarget` | MB (base) | Collision callbacks + `ICollisionEffect` dispatch + `CollisionEvent`; `SetColliderEnabled` |
| `Structure` | MB : CollisionTarget | Placement identity: `def` (StructureDef) + health / `TakeDamage` (stub) |
| `IStructureSpawner` | interface | Build-mode contract shared by both spawner kinds: `ResetForBuildMode` (enter) / `Warmup` (placement + exit); SpawnSystem's registry is a list of these |
| `Spawner` | MB, `ICollisionEffect`, `IStructureSpawner` | Per-slot spawn → travel → rest cycle; self-registers with SpawnSystem; `ResetForBuildMode` / `Warmup` |
| `ResourceSource` | MB, `ICollisionEffect` | Static resource node: grants `def.resource` per allowed-worker hit (yield resolve = `ResourceSourceDef.TryGetYield`, shared with Animal), depletes/respawns in place; swaps Ready/Harvested visual roots (`SetActive`) + toggles host collider. `infinite` defs keep a single visual |
| `Animal` | MB, `ICollisionEffect` | Mobile resource node: same `ResourceSourceDef` harvest per hit, but after `hitsBeforeDespawn` hits it notifies its owning AnimalSpawner and destroys itself (def.respawnTime unused — replacement cadence is the spawner's `spawnCooldown`); `infinite` never despawns |
| `AnimalWander` | MB | Kinematic wander: point in owner's territory → walk straight → pause → repeat; without an owner (scene-placed) wanders a plain circle around its start |
| `AnimalSpawner` | MB, `IStructureSpawner` | Keeps ≤ `maxAnimals` animals in the structure's territory (land cells within `territoryRadiusCells` of the footprint; own border counts free, occupied land is the fallback — den-in-forest); one replacement per `spawnCooldown`; territory follows the building via `instance.Cell` |
| `BuildModeButton` | MB (UI) | Toggle button: publishes `BuildModeToggleRequestedEvent`, reflects `BuildModeUIStateEvent` |
