# Little Peeps — Architecture

> Status note (synced with the code on 2026-09-16; the age transition re-synced on 2026-09-24): the
> run loop is closed end to end in code.
> **Build mode** is complete — Place / Sell / Move tools, fences on grid edges, hover tints, ghost,
> grid overlay. **Ages + the RunStats bonus system** are in (buy age → spend + apply stat modifiers →
> pick one of the zones on offer → the zone **rises out of the sea** → banner → perk pick; no fade). **Perks** are authored as assets, rolled per age and
> picked on their own screen (hold-to-confirm). **Prestige** pays out from a per-profile record
> formula; the pier click prestiges immediately — the confirmation screen is not built yet.
> **Animals** (mobile resource nodes) and **harvest feedback** (particles + floating numbers) are in.
> **Stamina** is aligned to the design doc: an outing is a stamina clock, only a house refills it,
> a tired unit moves slower and is the only kind a house takes in.
> **Meta → run is in progress** (`GlobalUpgradeDef` exists with `id` + `description` only; no
> catalogue, no purchase path, nothing applied at run start yet). Still stubs, marked **(stub)**
> below: `SaveSystem` (every launch starts with a fresh `MetaContext`), `MainMenuState`,
> `MetaUpgradesState`, `PrestigeMenuState`.

## Layer Diagram

```
┌─────────────────────────────────────────────────────────────┐
│  UI  (ResourcePanel · AgeUI · AgeCostPanel · AgeTimelinePanel │
│       · BuildPanelUI · PerkSelectionUI · BuildModeButton)     │
│  Subscribes to ReactiveValue<T>.OnChanged and EventBus<T>    │
└──────────────────────────┬──────────────────────────────────┘
                           │ reads / fires events
┌──────────────────────────▼──────────────────────────────────┐
│  States  (AppState FSM / GameplayState FSM)                  │
│  Commands  (TriggerAgeCmd)                                   │
└──────────────────────────┬──────────────────────────────────┘
                           │ calls
┌──────────────────────────▼──────────────────────────────────┐
│  Systems  (RunManager · IslandSystem · ResourceSystem ·      │
│   StructureSystem · SpawnSystem · UnitSystem · TapSystem ·   │
│   AgeSystem · AgeSequencer · IslandRisePlayer · PerkSystem · │
│   PrestigeSystem · PierSystem · PlacementController + tools ·│
│   Harvest* · …)                                              │
└──────┬────────────────────────────────────────┬─────────────┘
       │ uses                                   │ routes each hit to, by unit state
┌──────▼────────────────────────┐    ┌──────────▼──────────────┐
│  Entities                     │    │  Effects                │
│  CollisionTarget (base)       │    │  ICollisionEffect impls:│
│   ├ Structure                 │    │  ResourceSource · Animal│
│  Unit · Spawner · Pier        │    │  Shelter (IShelter):    │
│  ResourceSource · Animal      │    │  Spawner                │
│  AnimalSpawner · AnimalWander │    │  Gates (IHitGate impls):│
│  IStructureSpawner (contract) │    │  VisitZone · ForgeHeat  │
└──────┬────────────────────────┘    └─────────────────────────┘
       │ uses
┌──────▼──────────────────────────────────────────────────────┐
│  Core  (EventBus<T> · StateMachine · ReactiveValue<T> ·      │
│         RunStats)                                            │
│  Core/Context  (RunContext · MetaContext · SessionContext)   │
└──────┬───────────────────────────────────────────────────────┘
       │ defined by
┌──────▼───────────────────────────────────────────────────────┐
│  Data  (ScriptableObjects + Enums)                           │
│  StructureDef · UnitDef · ResourceSourceDef · AgeDef ·       │
│  PerkDef / StatPerkDef · PerkCatalogueDef · GlobalUpgradeDef │
│  StartConfigDef · StartingLayoutDef · BuildPaletteDef ·      │
│  PrestigeFormula · StatModifier · StatId · IslandRiseProfile │
└──────────────────────────────────────────────────────────────┘
```

Rule: layers only depend downward. UI never writes to Systems directly — it reads ReactiveValues and fires events.

---

## Unified "Structure" model

Everything that stands on the grid is a **Structure**; behaviour lives in components:

- `CollisionTarget` (base MonoBehaviour) — owns the collision callbacks and routes each hit by the
  unit's state: a WORKING unit reaches the `ICollisionEffect` components after every `IHitGate` on
  the target has let the hit through; a TIRED unit reaches only the `IShelter` components (see
  *Stamina and Tired* below). Dispatch is **local**: there is no global collision event.
- `Structure : CollisionTarget` — adds placement identity: `def` (`StructureDef`). No health — a
  structure is removed by selling it, never by damage.
- A structure carries one (or more) behaviour components:
  - `Spawner` (produces units; as the house it is also the one `IShelter`) — `[RequireComponent(typeof(Structure))]`.
  - `ResourceSource` (produces resources) — `[RequireComponent(typeof(CollisionTarget))]`.
  - `AnimalSpawner` (produces animals — mobile resource nodes) — `[RequireComponent(typeof(Structure))]`.
  - `VisitZone` (rations hits per visit — a gate, not an effect) — on a trigger CHILD of the target,
    `[RequireComponent(Collider2D, Rigidbody2D)]`.
- Examples: House/Hut = Structure + Spawner · Market = Structure + ResourceSource (infinite) +
  VisitZone (the passage) · Tree/Wheat = Structure + ResourceSource (natural) · Stable/Den =
  Structure + AnimalSpawner.

**Hit gates** (`IHitGate.TryConsume(unit)`) sit between a collision and its effects. `HandleHit`
asks every gate on the target (root + children) and drops the hit on the first refusal — the unit
still bounces, it just pays nothing. `TryConsume` decides AND spends in one call, so a rationing
gate can never hand out a budget it can't then debit; the flip side is that gates are asked in
order, and a gate that consumed before a later one refused has spent a hit for nothing — keep ONE
rationing gate per target. A gate is effect-agnostic and prefab-composable: it knows nothing about
what the hit would have paid, so the same component can sit on any building or animal. Two so far:
`VisitZone` (Market — `hitsPerVisit` counted hits per stay inside the passage trigger, then nothing
until the unit leaves and re-enters; a unit outside the zone never counts, so the zone also decides
WHICH surfaces pay — inside the passage a unit can only touch the inner walls) and `ForgeHeat`
(refuses while overheated). The per-visit number lives on the component, not in
`ResourceSourceDef`, and is the `MarketVisitHits` stat, resolved on entry — a unit already inside
finishes the budget it came in with.

**Stamina and Tired** (design doc: *Stamina and Tired*, *Population and identity*). An outing is a
stamina clock: `UnitDef.stamina` seconds of field work (× `UnitStamina`), ticking in
`Unit.FixedUpdate` the whole time the unit is out of a house, boosted or not — a boost makes the
outing more productive, never longer. Stamina > 0 = WORKING; 0 = TIRED. `Unit.IsTired` is the one
derived flag; there is no second variable. The state decides exactly two things:
- **Dispatch.** `CollisionTarget.HandleHit` branches on `IsTired`: working → gates → effects;
  tired → the target's `IShelter` components only. So a tired unit pays nothing anywhere (no market
  visit is debited, the forge does not heat) and a working unit can never enter a house — both
  halves of the rule live in that one branch, and no effect, gate or shelter checks stamina itself.
- **Speed.** `Unit.TargetSpeed` is the base speed while working and base ×
  `UnitDef.tiredSpeedMultiplier` (`[Range(0.1, 1)]` — never zero, a tired unit must stay distinct
  from a stopped one) while tired. Every boost decays to TargetSpeed, re-read each step, so a unit
  that tires mid-boost eases down instead of snapping; `Unit.OnBecameTired()` is the single
  working→tired transition and rescales the velocity once when no boost is running (direction kept;
  bounciness 1 and zero drag hold it from there).

Only a house refills stamina: `Spawner.OnTiredHit` → `Unit.EnterRest` (stamina zeroed while
inside, the unit stays counted in the population) → `restDuration` (× `SpawnerRecharge`) →
`Unit.Launch` with a full clock. Any house of the unit's type takes it, whichever house spawned it;
an occupied slot refuses; there is **no lockout** after a launch — the unit that just left has full
stamina, so nothing can duck back in (the old `Cooldown` slot state existed only before stamina
did). A tap is speed only (`Unit.Boost`); `TapSystem.refreshStamina` (default off) turns it into a
full restart — the hook for a future perk. `TiredView` (the bars over the head) polls `IsTired` and
is presentation only. Planned on this skeleton, not built: `Unit.SpendStamina` for buildings that
charge per paying hit or per work period (calls the same `OnBecameTired`); a hold-without-reset for
buildings that keep the worker with their stamina intact (tavern, mine, bakery); a `Drunk` status
(`IsTired || IsDrunk` at the shelter branch); a third, state-neutral dispatch list for things like
spring towers.

**Two placement kinds** (`StructureDef.placement`): `Cell` — a footprint of cells (the default), keyed
by `Vector2Int` in `RunContext.structures`; `Edge` — sits on the boundary line between two cells
(fences), keyed by `Edge` in `RunContext.fences`. Cells and edges share one integer lattice
(`Edge.cs`), so there is no offset between the two coordinate spaces. `StructureDef.requiredAge`
gates a card in the build palette until the run reaches that age.

Units are gatherers (Farmer/Lumberjack/Hunter/Miner); military (Swordsman) is a later second kind.
**Animals (Alpaca/Boar/Fox) are NOT units** — they are mobile resource nodes: a standalone
`CollisionTarget` (not a `Structure`: no def, no grid cell — the first direct use of the base class)
+ `Animal` (harvest via the same `ResourceSourceDef` pipeline as static sources; resource per hit,
despawns after `hitsBeforeDespawn`, `infinite` never despawns) + `AnimalWander` (kinematic
wandering). An animal is identified by its `ResourceSourceDef` asset, like a tree.

Both spawner kinds implement **`IStructureSpawner`** (`ResetForBuildMode` / `Warmup`) —
`SpawnSystem`'s registry drives build-mode transitions through the interface. Internals are
deliberately NOT shared: the unit launch→return→rest slot cycle and the animal
die→cooldown→replace cycle have nothing in common beyond that contract.

---

## Three Contexts

### RunContext — resets on prestige
**Owner:** `RunManager` creates it in `StartNewRun()`. Long-lived FSM states take the `RunManager`
and read `CurrentRun` at the point of use; MonoBehaviour systems that cache it re-bind on
`RunStartedEvent`. Never hold a `RunContext` in an object that outlives the run — that exact bug
shipped once (see `GameplayContainerState`).

| Field | Type | Purpose |
|-------|------|---------|
| `resources` | `Dictionary<ResourceType, float>` | Current resource amounts (float; display floors at show time) |
| `structures` | `Dictionary<Vector2Int, StructureInstance>` | Live cell-structure registry (origin cell → instance) |
| `fences` | `Dictionary<Edge, EdgeInstance>` | Live edge-structure registry |
| `currentAge` | `int` | Age index (0-based; counts transitions bought) |
| `perksChosen` | `List<PerkDef>` | Perks already applied this run (excluded from later rolls) |
| `harvested` | `Dictionary<ResourceType, float>` | Everything PRODUCED this run, credited by `AddHarvest` only — the prestige ledger; spends/refunds never land here |
| `stats` | `RunStats` | Accumulated bonus layer; see below |

`StructureInstance` = `Def` + `RuntimeObject` (`Structure`) + `Cell`; `EdgeInstance` is the same with
an `Edge`. `RuntimeObject` is never saved — it is rebuilt from the def. **Rule:** do not introduce
run state that cannot be rebuilt from a def + an id; that is what keeps a run save small.

**Lifecycle:** `StartNewRun` tears the previous run down first (`EndRun`: pier → structures → spawn
system, order load-bearing), creates a fresh context, seeds it from `StartConfigDef` (island size,
starting layout, resources, baseline modifiers), initialises the run-scoped systems, places the
starting structures + pier, and publishes `RunStartedEvent` LAST so observers re-bind to a fully
built run. Prestige and the debug "Restart Run" context menu both go through this one path.

### MetaContext — persists to disk (JSON) — persistence is a **(stub)**
**Owner:** `SaveSystem` loads it (currently always fresh); `PrestigeSystem` writes it via
`BankPayout`; the meta-upgrade path that will read `globalUpgrades` at run start is in progress.

| Field | Type | Purpose |
|-------|------|---------|
| `prestigePoints` | `int` | Currency for global upgrades |
| `agePointsAwarded` | `int` | Record: age-term points already paid to this profile |
| `harvestPointsAwarded` | `int` | Record: harvest-term points already paid |
| `globalUpgrades` | `Dictionary<string, int>` | Level per upgrade, keyed by `GlobalUpgradeDef.id` (`[NonSerialized]`) |

A run earns only what it BEATS: `payout = max(0, ageTerm − agePointsAwarded) + max(0, harvestTerm −
harvestPointsAwarded)`; `BankPayout` raises each record with `Max`, never assignment. The records are
stored as POINTS so both terms subtract the same way; the cost is that retuning `PrestigeFormula`
does not re-price what was already paid.

**Lifecycle:** loaded on Boot → mutated by `PrestigeSystem` (earn) and, later, the meta-upgrade
purchase (spend) → saved whenever either mutates it *(SaveSystem.Save is a no-op today)*.

**Serialization note:** `Dictionary<>` is not supported by `JsonUtility`. When saves land,
`globalUpgrades` must round-trip through a serializable list of (id, level) pairs.

### SessionContext — runtime only, never saved
**Currently not instantiated and referenced by nothing** — a drag-era leftover kept as the future
home for session-scoped data. Its fields (`unitPool`, `draggedStructure`, `hoveredCell`) are stale;
`PlacementController` and the tools keep their own state.

---

## Stat System (RunStats) — base + modifiers

Game parameters follow a **base + modifiers** split, so bonuses stay data-driven and reset cleanly on prestige:

- **Base** values live in configs and never change: `UnitDef.speed`, `UnitDef.stamina`,
  `ResourceSourceDef.workerYields[worker].amount`, `ResourceSourceDef.respawnTime`, `Spawner`'s rest
  duration, etc.
- **Modifiers** accumulate on `RunContext.stats` (`RunStats`, plain C#). Sources only push
  `StatModifier` data: `StartConfigDef.startingModifiers` (run baseline, seeded by `RunManager`),
  `AgeDef.modifiers` (via `TriggerAgeCmd`), `StatPerkDef.modifiers` (via `ApplyPerk`), and — in
  progress — meta upgrades at run start.
- **One formula, everywhere:** `final = (base + Σflat) * (1 + Σpercent)`. Durations are ordinary
  stats on this same formula: a modifier scales the SECONDS, so "regrows faster" is authored as a
  NEGATIVE percent. No clamp, on purpose — a negative duration just fires instantly.

`StatModifier` (Data, `[Serializable]` struct) = `id` (`StatId`) + `unitScope` (`UnitType`) +
`resourceScope` (`ResourceType`) + `sourceScope` (`ResourceSourceDef`, may be EMPTY) + `flat` +
`percent`. Drawn by `Editor/StatModifierDrawer`, which reads `StatMeta.ScopeOf` and shows only the
scope fields the stat actually uses.

| `StatId` | Scope | Consumer (`stats.Apply` site) |
|----------|-------|-------------------------------|
| `ProductionGlobal` | none | `ResourceSystem.AddHarvest` — multiplier on every credited harvest |
| `ResourceYield` | unit × resource × source | `ResourceSystem.AddHarvest` — amount per hit |
| `UnitSpeed` | unit | `Unit.ResolveBaseSpeed` (resolved on each `Launch`) |
| `SpawnerRecharge` | unit | `Spawner` — seconds a unit rests inside before launching |
| `UnitStamina` | unit | `Unit.ResolveMaxStamina` — seconds of field work per outing (resolved on each launch) |
| `SourceRespawn` | source | `ResourceSource` — seconds a depleted source takes to regrow |
| `ForgeHeatPerHit` / `ForgeMaxHeat` / `ForgeCoolingTime` / `ForgeHotYield` | none | `ForgeHeat` — heat per paying hit, overheat cap, full cool-down seconds, yield multiplier at full heat (×1 until a perk adds to it) |
| `HouseCapacity` | unit | `Spawner.ResolveCapacity` via `ApplyCount` — the one **materialised** stat: turned into slots + the global cap at `Warmup`, so a mid-run change is PUSHED (`RunStats.Changed` → `SpawnSystem` → `Spawner.RefreshFromStats`, growth only) |
| `MarketVisitHits` | none | `VisitZone.ResolveHitsPerVisit` via `ApplyCount` — counted hits per visit, resolved on entry |

**Scope normalisation:** `StatMeta.ScopeOf` gives each id's mask; `RunStats.MakeKey` zeroes the
dimensions a stat does not use and derives `resource` from `source` when both are present, in
**both** `Add` and `Apply`, so authored data and the query can never silently miss each other.
**Trap:** the `_ => StatScope.None` default means a new `StatId` without its own line there becomes
GLOBAL — every scoped stat has a test pinning its mask; add one for each new scoped stat.

**The source axis:** an EMPTY `sourceScope` means *any source*, not *its own bucket* — `Apply` sums
the exact-source bucket and the any-source bucket and runs the formula ONCE (twice would multiply
the percents). Rule: `Source` is the only dimension with a meaningful empty value; `Farmer` / `Food`
are enum zeros, not wildcards.

**Perf:** one O(1) dictionary hit on a struct key (`IEquatable`, identity hash on the source, no
boxing) — two for a source-scoped query. Reads can be per-hit; modifiers change a few times per run.
`RunStats` is never serialised: saves store the sources and the sheet is rebuilt.

**Extending:** a bonus on an already-wired param = pure data. A new param = one `StatId` entry + its
`ScopeOf` line + one `Apply` at the consumer (+ the scope test). A new scope dimension = a field on
`RunStats.Key`/`MakeKey` once. Designer-facing details: `BONUS_SYSTEM_GUIDE.md`.

---

## EventBus

`EventBus<T>` is a generic static publish-subscribe bus, allocation-free on `Publish` (a cached
subscriber array is rebuilt only when the subscriber set changes). Each event type gets its own
static subscriber list. **Snapshot semantics are a contract:** a handler unsubscribed mid-dispatch
still receives the event in flight. `EventBus.ClearAll()` runs on
`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`, so a play-mode restart with domain reload
off never carries stale subscribers over.

```csharp
EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);
EventBus<AgeStartedEvent>.Publish(new AgeStartedEvent { Age = 2 });
EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);   // always unsubscribe in OnDisable/Exit
```

**Event catalogue** (`Core/GameEvents.cs`):

| Struct | Published by | Consumed by |
|--------|-------------|-------------|
| `RunStartedEvent` (`Run`) | `RunManager.StartNewRun`, as its LAST step | `AgeSystem`, `TapSystem`, `AgeUI`, `AgeCostPanel`, `AgeTimelinePanel`, `BuildPanelUI` — every MonoBehaviour that caches the run re-binds here. On the very first run it reaches nobody (their `OnEnable` has not run yet), which is why `GameBootstrap` still injects by hand |
| `ResourceChangedEvent` (`ResourceType`, `NewValue`) | `ResourceSystem.AddResource` | `AgeUI`, `AgeCostPanel` (affordability). The resource bar does NOT use it — `ResourcePanel` binds each row to the type's `ReactiveValue`, which is why `ResourceSystem.Initialize` must REUSE those objects across runs rather than replace them |
| `HarvestedEvent` (`Source`, `Type`, `Amount`, `Position`) | `ResourceSystem.AddHarvest` — the production gateway itself, NOT its callers | `HarvestVfxSystem` (pickup particles), `HarvestNumbers` (the "+1.2k"). Published from inside the gateway on purpose: any future production path gets its feedback for free. Carries the whole `ResourceSourceDef` because Wheat/Boar/Fox are all Food and Alpaka/Market are both Coins. `Amount` is the CREDITED figure, after both multipliers |
| `AgeStartedEvent` (`Age`) | `AgeSequencer` (banner step, after the rise) | `CameraController` (re-clamp bounds), `PierSystem` (re-snap to the new right edge), `AgeUI`, `AgeCostPanel`, `AgeTimelinePanel`, `BuildPanelUI` (unlock cards by `requiredAge`) |
| `AgeAdvanceRequestedEvent` | `AgeUI` (Next Age button) | `GameplayContainerState` — enters `ZoneSelectionState` only from `PlayingState` and only if affordable |
| `IslandRepaintedEvent` | `IslandSystem.LateUpdate`, **at most once a frame**, on any frame its `IslandDrawing` painted (run start, zone commit, every tile a rise lands) | `WaterSystem` — rebuilds the coast twins (obstruction + side foam). Once a frame, not once a paint, because a rise lands dozens of tiles in a couple of seconds |
| `BuildModeToggleRequestedEvent` | `BuildModeButton` (click), `GameHotkeys` (B) | `GameplayContainerState`. The key path runs in `Update` past every UI raycast and regardless of timeScale, so the container guards on the CURRENT inner state — any new inner state needs the same guard |
| `SellModeRequestedEvent` | `GameHotkeys` (X) | `BuildPanelUI` (toggles the sell button) |
| `ExitToMenuRequestedEvent` | `GameHotkeys` (Esc) | `GameBootstrap` — DECLINED with a warning while `MainMenuState` is a stub, because entering it stranded the player (B3 restores the transition) |
| `BuildModeUIStateEvent` (`InBuildMode`, `Interactable`) | `GameplayContainerState` | `BuildModeButton` (icon swap + interactable), `BuildPanelUI` (show/hide) |
| `BuildDeniedEvent` (`Def`) | `PlaceTool` (unaffordable / blocked click) | `BuildPanelUI` → the selected card plays its denied cue |
| `PrestigeTriggeredEvent` | `TapSystem` (pier click) | `PlayingState` — deliberately the STATE, not `PrestigeSystem`: the subscription lasts exactly as long as normal play, so a run can never end from build mode or mid-transition. B2 swaps the direct `ExecutePrestige` for a `PrestigeMenuState` |
| `PerkSelectedEvent` (`Perk`) | `PerkSelectionUI` (a card's hold completed) | `PerkSelectionState` — applies via `PerkSystem`, hides the screen, returns to `PlayingState` |

> **No events for:** unit spawn/despawn (`SpawnSystem` calls `UnitSystem.Add/Remove` directly — single
> consumer), collisions (`CollisionTarget` dispatches to its own `ICollisionEffect`s), structure
> place/remove (callers use the `StructureSystem` return values), unit boosts. The skeleton's
> `CollisionEvent` / `Structure*Event` / `UnitBoostedEvent` were removed as dead.

---

## State Machines

`StateMachine` contract, pinned by tests: `Push` runs the new state's `Enter` over the current one;
`Pop` runs the revealed state's `Enter` a SECOND time. Do not insert a state inside a transition that
must not re-run its `Enter` (that is why the perk pick is a sibling of `AgeTransitionState`, not a
push on top of it).

### App FSM  (`appStateMachine` in GameBootstrap)

```
Boot ──(synchronous)──▶ GameplayContainer          ← current path
Boot ──▶ MainMenu ──play──▶ GameplayContainer        (stub — B3)
MainMenu ──meta──▶ MetaUpgrades ──back──▶ MainMenu    (stub — B4)
GameplayContainer ──Esc──▶ MainMenu                   (declined until B3)
```

`GameBootstrap.Awake` does all wiring (order-independent: every `Awake` completes before any
`Start`, so systems must not read injected state before `Start`). It pushes `BootState`, then
`ChangeState`s straight into `GameplayContainerState`. All long-lived states receive the
`RunManager`, never a `RunContext`.

### Gameplay FSM  (`innerFsm` inside `GameplayContainerState`)

```
Playing ──BuildModeToggleRequestedEvent──▶ BuildMode
BuildMode ──BuildModeToggleRequestedEvent──▶ Playing      (then 5s re-entry cooldown)
Playing ──AgeAdvanceRequestedEvent (affordable)──▶ ZoneSelection
ZoneSelection ──zone picked / nothing to offer──▶ AgeTransition
ZoneSelection ──aborted (cost changed)──▶ Playing         (no age, no perk owed)
AgeTransition ──sequencer done──▶ PerkSelection ──perk confirmed / nothing to offer──▶ Playing
Playing ──PrestigeTriggeredEvent──▶ ExecutePrestige → StartNewRun     (B2 inserts PrestigeMenu here)
```

**`GameplayContainerState` is the gameplay coordinator:** it subscribes to
`BuildModeToggleRequestedEvent` and `AgeAdvanceRequestedEvent`, owns the **5s re-entry cooldown**
(unscaled time, anti-respawn-abuse), switches the inner FSM, and pushes `BuildModeUIStateEvent` so
the button reflects mode + cooldown. **Both** entry points check the current inner state — build
mode and an age advance start only from `PlayingState`. It builds a fresh `ZoneSelectionState` per
request, which builds the `AgeTransitionState` (the states allowed to hold a `RunContext`: they die
with the transition).

**`ZoneSelectionState`:** on `Enter` runs `TriggerAgeCmd` (spend + `currentAge++` +
`stats.Add(ageDef.modifiers)`), sets `Time.timeScale = 0` and puts `IslandSystem.ProposeZones()` on
the zone screen. The pick (or there being nothing to offer) hands the chosen `ZoneOffer` to a new
`AgeTransitionState`; an abort (the cost changed between click and spend) goes straight back to
`PlayingState`, because no age happened and no perk is owed.

**`AgeTransitionState`:** sets `Time.timeScale = 0` (freezes the sim AND makes `TapSystem` ignore
world clicks — it early-returns at timeScale 0) and starts the `AgeSequencer`, on unscaled time:
`IslandSystem.CommitZone(offer, forRise: true)` commits the zone whole but hidden → the camera goes to
it → **the island rise** (`IslandRisePlayer`: the zone comes up out of the sea tile by tile, its
structures pop up, it breathes) → "Age N" banner + `AgeStartedEvent`. No fade: people and animals
stand frozen while their island grows. The sequencer takes its own taps from `InputHandler` (first ×3,
second → skip to the finale, one on the banner closes it after a short grace). Completion hands over
to `PerkSelectionState`.

**`PerkSelectionState`:** the pick as its own MODE, after the transition has fully finished, so the
player chooses over the island they just grew (and a future world-changing perk has a world to
change). `Enter` rolls via `PerkSystem.RollPerks` and shows `PerkSelectionUI` at `timeScale 0`
(unscaled timers everywhere — the EventSystem still runs); an empty roll skips the screen entirely.
`PerkSelectedEvent` → `PerkSystem.ApplyPerk` → back to `PlayingState`.

**`BuildModeState`:** on `Enter` sets `Time.timeScale = 0` and calls
`SpawnSystem.DespawnAllAndResetSpawners()` (all units returned to the pool); on `Exit` calls
`SpawnSystem.WarmupAllSpawners()` and restores `timeScale = 1`. `PlacementController` drives the
tools while here. **Invariant:** a run is never ended or saved from build mode — `MoveTool` can hold
a structure OFF the grid mid-drag, and that invariant is what makes `EndRun`'s sweep over
`run.structures` complete. The debug "Restart Run" refuses from build mode for the same reason.

**`PlayingState`:** subscribes to `PrestigeTriggeredEvent` for exactly its own lifetime and calls
`PrestigeSystem.ExecutePrestige` directly (no confirmation yet).

---

## Physics Setup

| Object | Collider | Rigidbody | Notes |
|--------|----------|-----------|-------|
| Unit | CircleCollider2D | Rigidbody2D (Dynamic) | Bounces off obstacles; passes through interactables |
| Structure (obstacle) | BoxCollider2D `isTrigger=false` | none (Static) | Unit bounces — `OnCollisionEnter2D` fires on the `CollisionTarget` |
| Structure (interactable) | BoxCollider2D `isTrigger=true` | none (Static) | Unit passes through — `OnTriggerEnter2D` fires; `SetColliderEnabled(false)` during drag / on source depletion |
| Animal (alpaca/boar/fox) | Collider2D `isTrigger=false` (child) | Rigidbody2D (**Kinematic**, root) | Units (Dynamic) bounce off it — that bounce IS the harvest hit; the kinematic body itself passes through structures/terrain (only wander destinations are validated, by AnimalSpawner) |
| Island boundary | TilemapCollider2D (Composite Operation: Merge) + CompositeCollider2D | Rigidbody2D (Static) | Auto-updates when tiles are added on island expansion |
| Pier | BoxCollider2D `isTrigger=false` (on a `Physics` child) | Rigidbody2D (Static, root) | An ordinary structure that units bounce off. A click on it triggers prestige: `TapSystem` resolves the hit with `GetComponentInParent<Pier>()`, so the `Pier` marker component must sit on the prefab **root**, not next to the collider |
| Market passage (`VisitZone`) | BoxCollider2D `isTrigger=true` on a `Passage` child (the two walls are ordinary obstacle boxes on the `Physics` child) | Rigidbody2D (Static) on that **same child** | The zone's own body is not optional: Unity delivers a trigger callback to the collider's GameObject AND to the GameObject of the Rigidbody2D it belongs to, so hung off the root body the passage's `OnTriggerEnter2D` would also reach `CollisionTarget`, which treats a trigger entry as a hit — walking in would pay a coin by itself. Sizing: across the passage the trigger overlaps the walls by 1–2 px (a unit can't be inside a wall, so this only guards a seam); along it, it stops one unit radius short of each mouth, so a unit sliding along the outside never counts as inside |

**Collision dispatch lives in `CollisionTarget`** (the base). Both `OnCollisionEnter2D` (obstacle
path) and `OnTriggerEnter2D` (interactable path) call the same `HandleHit(unit)`. A TIRED unit
goes to `IShelter.OnTiredHit` on the root and nowhere else; a WORKING unit goes through every
`IHitGate.TryConsume(unit)` on the target (root + children; the first refusal ends the hit as a
plain bounce) → `effect.OnHit(...)` for each `ICollisionEffect` component on the root. `Structure :
CollisionTarget` adds `def`. The Rigidbody2D sits on the root so callbacks fire there; the colliders
may live on children (fetched via `GetComponentsInChildren`) — except a `VisitZone`'s trigger, which
brings its own body precisely so its events do NOT reach the root (see the table).

**Obstacle vs Interactable** — set via `isTrigger` on the prefab's collider; same `HandleHit` path
for both. No separate CollisionSystem.

**Per-structure colliders (no CompositeCollider2D on structures)** — each has its own BoxCollider2D
so it can be enabled/disabled individually during drag and on resource-source depletion.
`SetColliderEnabled` toggles every collider under the target, a `VisitZone` trigger included; with
`Physics2D.callbacksOnDisable` on (it is, in Physics2DSettings) that exits every unit inside, and a
despawning unit exits the same way — a visit can't leak past the unit that started it.

**Pauses** (build mode, zone pick, age transition, perk pick) all use `Time.timeScale = 0` (not
`Physics2D.simulationMode`); build mode despawns the units first, so there is nothing to simulate.
What must move during a pause runs on unscaled time: the camera, the UI, the water's shaders
(`UnscaledShaderTimeFeature`) and the island rise — whose particle effects therefore need **Use
Unscaled Time**, and whose camera kick switches `CinemachineImpulseManager.IgnoreTimeScale` on. The
water's ripple simulation steps in `FixedUpdate`, so it can never run in a pause; it stays off.

---

## Assemblies & Tests

One runtime assembly, `LittlePeeps.Runtime` (single `namespace LittlePeeps`; folders give the
structure), plus `LittlePeeps.Editor` (drawers, the `HarvestFeedbackWindow`, the `IslandRiseWindow`)
and `LittlePeeps.Tests.EditMode` under `Assets/Little Peeps/Tests/EditMode`. Tests cover the risk
points, one file each: `RunStats`, `IslandGrid`, `EventBus`, `StateMachine`, spawner directions,
run teardown, harvest ledger, prestige formula + payout, perk roll, perk state + card, placement
target, the island rise's schedule and notes, and the coast painter (tile-by-tile repaint must equal
a full repaint at every step). **Edit Mode runs NO MonoBehaviour lifecycle callbacks** (no `[ExecuteAlways]` anywhere), so a
test may never depend on Unity invoking `Start`/`OnDestroy` — call the method by hand. Anything that
must prove a callback belongs in a PlayMode assembly (none yet).

---

## Systems & Entities Overview

| Component | Type | Responsibility |
|-----------|------|---------------|
| `RunManager` | MB | The one way a run starts and ends. `StartNewRun` = `EndRun` + fresh `RunContext` seeded from `StartConfigDef` (island size, `StartingLayoutDef`, resources, baseline modifiers) → init resource/structure/spawn systems → island → starting structures → `PierSystem.PlaceForRun` → `RunStartedEvent`. `EndRun` tears down pier → structures → spawn system (order load-bearing: spawners despawn their resting units, then SpawnSystem collects the roamers). `[ContextMenu("Restart Run")]` debug trigger, refused from build mode |
| `IslandGrid` | Plain C# | Grid data: sparse cells (`terrain` + `occupant`), placement validation (`CanPlace/Place/Remove` with footprint, allowed terrain, border), edge registry (`CanPlaceEdge/PlaceEdge/RemoveEdge`), world↔grid and world↔edge conversion; `CellBounds` / `WorldBounds` for the camera and the pier |
| `IslandGenerator` | Plain C# | Seeds the starting island (centered Grass square); `Expand(blocks)` adds `AgeDef.expansionBlocks` — absolute `RectInt`s, only missing cells created |
| `IslandSystem` | MB | Owns Grid + Generator + `Drawing`; `GenerateForRun`; `ProposeZones()` (one populated offer per biome) and `CommitZone(offer, forRise)` — driven explicitly by `AgeSequencer`, not an event. `forRise` commits the zone whole (grid, run, structures) but hides its land and switches its structures off in the same frame, so their `Start` waits for the rise to switch them on. Remembers `LastSection` / `LastContent` (what the rise brings up); publishes `IslandRepaintedEvent` once a frame |
| `IslandDrawing` | Plain C# | The island as DRAWN: the ground + trim tilemaps painted from the grid, minus the cells held back (`HideLand` / `HideOnly` / `RevealLand` — the last repaints only the 3×3 — / `RevealAllLand`); per-cell tint (`TintLand`) and lift (`OffsetLand`: the ground tile and the outline pieces it owns, via `SetTransformMatrix`). Plain so the rise's tuning window can draw an island of its own |
| `IslandTilePainter` | static | The autotiler: `PaintOf(land, cell)` decides a cell's ground or outline piece from its neighbours; `Repaint` (all) and `RepaintAround` (the 3×3 one cell can change) both go through it — tested to agree at every step |
| `IslandRiseSchedule` | Plain C# | The rise as a function of time, worked out once: each cell's start (wave order modes, noise, jitter, crescendo; normalised so any zone takes the cascade's length), its phases (under water → hop → squash → settled), note rank, the structures' pops (after their land; the river runs in from its source), the breath from the join, the finale and the banner time. No engine calls — fully unit-tested |
| `IslandRise` | Plain C# | Plays a schedule on an `IslandDrawing`: pooled stand-in sprites for tiles in flight (swapped for the real tile as each settles), drying via tile colour, standing neighbours bounce, the new zone breathes (its structures ride along), structures pop by root scale (a den's animals with it), effects / notes / camera kick on forward play only. `Tick` (forward) / `Seek` (any time, both ways) / `SpeedUp` / `SkipToFinale` / `Finish`. The SAME code runs in the game and in the tuning window |
| `IslandRisePlayer` | MB | The game's driver of `IslandRise`: `Play(section, content, onDone)` on unscaled time, the tap's `SpeedUp` / `SkipToFinale`, finishes whatever cuts it short (nothing is ever left hidden). `[ContextMenu] Replay Last Zone` for Play Mode tuning |
| `IslandRiseProfile` | SO | Every knob of the rise: `timing` (plain `IslandRiseTiming`: wave, tile phases, act 2, river, finale + breath) and the looks (depth scale/tint, hop, squash, wetness, pops + per-`StructureDef` overrides, bounce, breath, pixel snap, effect/sound slots, shake, tap speed-up). Read live — game and window alike |
| `SharedEmitter` | static | The one-shared-ParticleSystem-per-effect rule (World space, looping, no self-emission; `pausedPlay` also demands Use Unscaled Time): `Create` validates loudly and refuses, `EmitAt` moves + emits. Used by `HarvestVfxSystem` and the rise |
| `StructureSystem` | MB | Place / Sell / Remove / PickUp / Drop for BOTH cell and edge structures; `PlaceStructure` validates + spends, `PlaceInitial` is the free run-start path (layout, pier); `ClearAll` sweeps the run's registries on `EndRun`. Returns the created instance so owners (the pier) can track and move it |
| `PlacementController` | MB | Build-mode input router: the panel selection chooses the tool — card → `PlaceTool`, sell button → `SellTool`, nothing → `MoveTool` (default). Right-click cancels the tool's action or clears the selection (`ToolCleared` → panel) |
| `IPlacementTool` / `PlaceTool` / `MoveTool` / `SellTool` | Plain C# | One tool active at a time. Contract: `Exit` leaves NOTHING behind (no ghost, no tint, no half-finished drag). `PlaceTool` is one instance per `StructureDef` (ghost, cell or edge); `MoveTool` is the only tool with state between clicks and the only one that holds a structure off the grid; `SellTool` refunds `sellRefundPercent` |
| `PlacementTarget` | Plain C# | THE answer to "what is under the cursor" — fence on an edge, structure on a cell, or nothing — shared by Sell, Move pick-up and the hover highlight, so the fence-wins rule exists once |
| `PlacementVisuals` / `HoverHighlight` / `GridOverlay` | MB | Everything build mode DRAWS that is not a real structure: ghost + territory halo + hover/drag tints (visuals); cursor-target tint with meaning chosen by the tool (highlight); one procedural quad mesh outlining every cell (overlay — thin quads, not `MeshTopology.Lines`, which URP 2D draws unreliably) |
| `UnitPool` | MB | Pool per UnitDef; `Get` / `Release` |
| `UnitSystem` | MB | Live-unit registry (`ActiveUnits`); fed by **direct `SpawnSystem` Add/Remove** (no events); home for future bulk ops |
| `SpawnSystem` | MB | Bridges Spawner ↔ UnitPool; per-type capacity; syncs UnitSystem; owns the **spawner registry** (`IStructureSpawner`: unit Spawners + AnimalSpawners); `DespawnAllAndResetSpawners` / `WarmupAllSpawners` (build mode, `IsBuildMode`); `ResetForNewRun`; `Initialize(RunContext)` injects `RunStats` into each spawned unit |
| `ResourceSystem` | MB | One `ReactiveValue<float>` per resource type, REUSED across runs (`Initialize` assigns into the existing slot — replacing it froze the bar after a prestige). `AddHarvest(source, worker, base, position)` is the single production gateway (applies `ResourceYield` + `ProductionGlobal`, books `harvested`, publishes `HarvestedEvent`); `AddResource` / `Spend` / `CanAfford` stay raw for spends and refunds |
| `PierSystem` | MB | Owns the pier for a run: `PlaceForRun()` (after island gen) drops it in the bottom-right corner; on `AgeStartedEvent` re-snaps to the new right edge via `StructureSystem` pick-up/drop; `ClearForRun()` on teardown. Not part of `StartingLayoutDef` — single owner of the pier's cell |
| `TapSystem` | MB | Click on the world: boosts units in an AoE radius around the cursor — speed only by default, `refreshStamina` (off) also refills stamina; a click on the pier publishes `PrestigeTriggeredEvent`. Early-returns at `timeScale 0`, which is what makes every pause also an input block. Re-binds on `RunStartedEvent` |
| `AgeSystem` | MB | Owns the ordered `List<AgeDef>` catalogue; `TransitionFrom(age)` / `NextAge` / `CanAdvance`. Current age lives on `RunContext`, so the system is stateless between runs; re-binds on `RunStartedEvent` |
| `AgeSequencer` | MB | Coroutine chain for an age transition, unscaled time, no fade: commit the zone for the rise → camera to it (`RefreshBounds` + `FocusOn` — the bounds would otherwise catch up only at the banner) → `IslandRisePlayer.Play` → "Age N" banner (its `CanvasGroup` faded in and out). Taps from `InputHandler`: ×3, then skip, then close the banner after `bannerTapGrace`. Without a rise player the zone simply appears. Signals completion via callback. Pure choreography: every step takes a KNOWN time — the perk pick, which waits on a human, lives in `PerkSelectionState` instead |
| `PerkSystem` | MB | `RollPerks(age, run)`: weighted draw WITHOUT replacement from `PerkCatalogueDef`, filtered by `minAge` and `perksChosen`, fewer than `choicesOffered` when fewer are eligible (never filler). `ApplyPerk` calls `perk.ApplyPerk(run)` and records it. Validates ids at startup (empty / duplicate = a future lost-perk bug). Never casts to `StatPerkDef` — a perk with behaviour is its own `PerkDef` subclass |
| `PrestigeSystem` | MB | Owns the payout. `Calculate` = `PrestigeFormula.Points(run, meta)`: an age term (`pointsPerAge × currentAge`) and a harvest term (`coefficient × weightedHarvest^exponent`), each paid only for what it BEATS of the profile's record. `ExecutePrestige` reads the payout and both gross terms BEFORE banking, saves (no-op today), then `RunManager.StartNewRun`. `CanPrestige` gates the pier on `pierUnlockAge` |
| `SaveSystem` | MB | JSON persistence of `MetaContext` (**stub**: `Load` returns a fresh context, `Save` does nothing) |
| `GameHotkeys` | MB | Discrete hotkeys → events (B build mode, X sell, Esc exit-to-menu), bindings editable in the inspector. Runs in `Update`, past UI raycasts and timeScale — consumers guard |
| `InputHandler` | MB | Raw input router (screen → world); read by `CameraController`, `PlacementController`, `TapSystem`, and `AgeSequencer` (the rise's taps — it reports clicks at any time scale) |
| `CameraController` | MB | Continuous camera movement; `RefreshBounds` re-clamps to `IslandGrid.WorldBounds` on `AgeStartedEvent` (and when `AgeSequencer` asks, at the start of a rise); `FocusOn` sends the target to a point |
| `HarvestVfxSystem` | MB | Pickup particles for every harvest. **One shared emitter per `ResourceSourceDef`** (the `SharedEmitter` rule), made from `def.pickupFx` on first harvest and reused (one system emitting many particles = one draw call). Moves the emitter and `Emit`s — deliberately not `EmitParams`, so the authored Shape module keeps working. **Validates instead of silently fixing** a prefab that isn't World-space/looping. Skips off-screen harvests |
| `HarvestNumbers` | MB | The floating "+1.2k" — universal, one prefab and one set of curves for every resource. Pooled world-space `TextMeshPro`, driven by one flat loop over a fixed-size array (no coroutine, no MonoBehaviour per popup); `SetText` overloads format in place, nothing lands on the GC. Cap + recycle-nearest-death. **Tuning lives on the system, not the prefab** |
| `CollisionTarget` | MB (base) | Collision callbacks → routed by `Unit.IsTired`: tired → `IShelter` (root) only; working → `IHitGate` check (root + children) → `ICollisionEffect` dispatch (root); `SetColliderEnabled` toggles every collider under it, a `VisitZone` trigger included |
| `Structure` | MB : CollisionTarget | Placement identity: `def` (`StructureDef`) |
| `IHitGate` | interface | `TryConsume(unit)` — decides AND spends in one call; asked before any effect, one refusal = plain bounce. Effect-agnostic, so a gate composes onto any target; one rationing gate per target. Implemented by `VisitZone` and `ForgeHeat` |
| `IShelter` | interface | `OnTiredHit(unit)` — what a TIRED unit's hit reaches instead of gates + effects; the other half of the one state branch in `HandleHit`. Implemented by `Spawner` (the house) |
| `VisitZone` | MB, `IHitGate` | Per-visit hit ration (`hitsPerVisit` on the component, not in a def): `Dictionary<Unit,int>` filled on trigger enter, debited per hit, dropped on exit. Sits on its own child with the trigger AND a Static Rigidbody2D so its trigger events stay off the root; `Reset()` sets both up, `Awake` errors if the collider isn't a trigger |
| `IStructureSpawner` | interface | Build-mode contract shared by both spawner kinds: `ResetForBuildMode` (enter) / `Warmup` (placement + exit) |
| `Spawner` | MB, `IShelter`, `IStructureSpawner` | Per-slot spawn → travel → rest cycle, slot = `Free ↔ Occupied` (no lockout); any free slot takes a tired unit of its type, whichever house spawned it; self-registers with SpawnSystem; base `capacity` × `HouseCapacity` materialised into slots at `Warmup`, grown on `RefreshFromStats`; rest duration through `SpawnerRecharge`; launch boost (`launchSpeedMultiplier` / `launchBoostDuration`) |
| `Unit` | MB | Bouncing gatherer: stamina clock (`UnitDef.stamina` × `UnitStamina`, ticks whenever on the field) → `IsTired`; `TargetSpeed` (`UnitSpeed`, × `tiredSpeedMultiplier` when tired); `Launch` (house — refills), `Boost(mult, duration, refreshStamina)` (tap), `EnterRest`, `OnBecameTired` (the one transition); holds `RunStats` via `SetStats` |
| `TiredView` | MB | The bars over a tired unit's head: polls `Unit.IsTired`, presentation only, sits under the visual root as a sibling of the model so it hides with the unit and dodges the walk animator |
| `ResourceSource` | MB, `ICollisionEffect` | Static resource node: grants `def.resource` per allowed-worker hit (yield via `ResourceSourceDef.TryGetYield`, shared with Animal), depletes and respawns in place (respawn through `SourceRespawn`); swaps Ready/Harvested visual roots + toggles the host collider; `infinite` defs keep a single visual. On depletion the ready root **fades** (`fadeOutTime` + curve, per prefab) via `SpriteRenderer.color` — gameplay ends at the hit, so the fade can never hand out a free harvest |
| `Animal` | MB, `ICollisionEffect` | Mobile resource node: same `ResourceSourceDef` harvest per hit; after `hitsBeforeDespawn` it notifies its owning AnimalSpawner and destroys itself (`def.respawnTime` unused — cadence is the spawner's `spawnCooldown`); `infinite` never despawns |
| `AnimalWander` | MB | Kinematic wander: point in owner's territory → walk straight → pause → repeat; without an owner wanders a plain circle around its start |
| `AnimalSpawner` | MB, `IStructureSpawner` | Keeps ≤ `maxAnimals` animals in the structure's territory (land cells within `territoryRadiusCells`); one replacement per `spawnCooldown`; territory follows the building via `instance.Cell`. `Animals` (read-only) lets the island rise pop a new den's animals up with it |
| `ResourcePanel` / `ResourceUnit` | MB (UI) | Top resource bar: one `ResourceUnit` per `ResourceIconSet` entry, bound to its `ReactiveValue`; the icon set's order IS the display order |
| `AgeUI` / `AgeCostPanel` / `AgeTimelinePanel` | MB (UI) | Next-age button + label; the price tag (one `ResourceUnit` per cost entry, same prefab as the bar); the timeline of bought ages (layout-driven: a bottom-aligned Vertical Layout Group + RectMask2D do the "slide up") |
| `BuildModeButton` / `BuildPanelUI` / `BuildCardUI` | MB (UI) | Toggle button (publishes toggle, reflects `BuildModeUIStateEvent`); the build palette (a card per `BuildPaletteDef` entry, locked below `requiredAge`, sell button, drives the controller's tool selection; hidden via `CanvasGroup`, stays active to keep listening); one card (fixed root = slot, `AnimatedVisual` child moves, LitMotion) |
| `PerkSelectionUI` / `PerkCardUI` | MB (UI) | The perk screen: owns the cards and nothing else — publishes `PerkSelectedEvent`, holds no run. Hidden via `CanvasGroup`, never `SetActive(false)` (an inactive panel's `Awake` would fire inside `Show()` and re-hide it). A card confirms on HOLD (release, never press, even at `holdDuration 0`), unscaled time; frame = hover, scale = press (LitMotion), fill = hold — three independent visuals |
