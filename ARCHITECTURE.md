# Little Peeps — Architecture

> Status note (the whole document re-synced with the code on 2026-09-29): the run loop is closed end
> to end in code. **Build mode** is complete — Place / Sell / Move tools, fences on grid edges, hover
> tints, ghost, grid overlay, shaped footprints. **The island is generated** from a seed, zone by
> zone, out of biomes. **Ages + the RunStats bonus system** are in (buy age → spend + apply stat
> modifiers → pick one of the zones on offer → the zone **rises out of the sea** → banner → perk pick;
> no fade). **Perks** are authored as assets, rolled per age and picked on their own screen
> (hold-to-confirm). **Prestige** pays out from a per-profile record formula; the pier click
> prestiges immediately — the confirmation screen is not built yet.
> **Animals** (mobile resource nodes) and **harvest feedback** (particles + floating numbers) are in.
> **Stamina** is aligned to the design doc: an outing is a stamina clock, only a house refills it,
> a tired unit moves slower and a house takes in only the tired and the drunk. **Professions** come
> from tool racks. The **tavern** serves a drink, holds its guest and lets it out **Drunk**; a tired
> guest walks home on **autopilot**. What is on screen is **one table per UI mode**; the sea is
> **Modern 2D Water**, spawned at run time.
> **Meta → run is in progress** (`GlobalUpgradeDef` exists with `id` + `description` only; no
> catalogue, no purchase path, nothing applied at run start yet). Still stubs, marked **(stub)**
> below: `SaveSystem` (every launch starts with a fresh `MetaContext`), `MainMenuState`,
> `MetaUpgradesState`, `PrestigeMenuState`.

## Layer Diagram

```
┌────────────────────────────────────────────────────────────────────┐
│  UI  (UIRoot · UIVisibility · ResourcePanel · AgeAdvancePanel ·    │
│       AgeCostPanel · AgeTimelinePanel · BuildModeButton ·          │
│       BuildPanelUI · ZoneSelectionUI · PerkSelectionUI)            │
│  Reads ReactiveValue<T>.OnChanged and EventBus<T>; what is on      │
│  screen is one table per UIMode (UIVisibility)                     │
└──────────────────────────┬─────────────────────────────────────────┘
                           │ reads / fires events
┌──────────────────────────▼─────────────────────────────────────────┐
│  States  (AppState FSM / GameplayState FSM)                        │
│  Commands  (TriggerAgeCmd)                                         │
└──────────────────────────┬─────────────────────────────────────────┘
                           │ calls
┌──────────────────────────▼─────────────────────────────────────────┐
│  Systems  (RunManager · IslandSystem + IslandGenerator ·           │
│   ResourceSystem · StructureSystem · SpawnSystem · UnitSystem ·    │
│   TapSystem · AgeSystem · AgeSequencer · IslandRisePlayer ·        │
│   PerkSystem · PrestigeSystem · PierSystem · WaterSystem ·         │
│   CameraController · PlacementController + tools · Harvest* · …)   │
│  Rendering  (IslandBend · UnscaledShaderTimeFeature)               │
└──────┬──────────────────────────────────────────┬──────────────────┘
       │ uses                                     │ routes each hit to, by unit state
┌──────▼──────────────────────────┐    ┌──────────▼──────────────────┐
│  Entities                       │    │  Effects                    │
│  CollisionTarget (base)         │    │  Entrances (IEntrance):     │
│   ├ Structure                   │    │  Spawner · Tavern           │
│  Unit · Spawner · Tavern        │    │  ICollisionEffect impls:    │
│  ToolRack · Pier · ForgeHeat    │    │  ResourceSource · ToolRack  │
│  ResourceSource · Animal        │    │  Gates (IHitGate impls):    │
│  AnimalSpawner · AnimalWander   │    │  VisitZone · ForgeHeat      │
│  IStructureSpawner (contract)   │    │  Yield (IYieldScale impls): │
│  + their views (TiredView, …)   │    │  ForgeHeat                  │
└──────┬──────────────────────────┘    └─────────────────────────────┘
       │ uses
┌──────▼─────────────────────────────────────────────────────────────┐
│  Core  (EventBus<T> · StateMachine · ReactiveValue<T> · RunStats · │
│         StatModifierText · ResourceFormat · RomanNumeral)          │
│  Core/Context  (RunContext · MetaContext · SessionContext)         │
└──────┬─────────────────────────────────────────────────────────────┘
       │ defined by
┌──────▼─────────────────────────────────────────────────────────────┐
│  Data  (ScriptableObjects + Enums)                                 │
│  StructureDef · UnitDef · ProfessionDef · ResourceSourceDef ·      │
│  AgeDef · BiomeDef (IslandBiome) · StartConfigDef (IslandRules) ·  │
│  PerkDef / StatPerkDef · PerkCatalogueDef · GlobalUpgradeDef ·     │
│  BuildPaletteDef · PrestigeFormula · StatModifier · StatId ·       │
│  IslandRiseProfile · IslandTileSet · ResourceIconSet · …           │
└────────────────────────────────────────────────────────────────────┘
```

Rule: layers only depend downward. UI never writes to Systems directly — it reads ReactiveValues and
fires events. Two deliberate exceptions, both presentation or input routing and never run state: the
build palette selects the `PlacementController`'s tool, and the zone screen drives its preview
overlay and the camera.

---

## Unified "Structure" model

Everything that stands on the grid is a **Structure**; behaviour lives in components:

- `CollisionTarget` (base MonoBehaviour) — owns the collision callbacks and routes each hit in two
  steps: first every `IEntrance` on the root, whatever the unit's state — a building that lets the
  unit in ends the hit; then, for a WORKING unit only, the `ICollisionEffect` components after every
  `IHitGate` on the target has let the hit through (see *Stamina and Tired* below). Dispatch is
  **local**: there is no global collision event.
- `Structure : CollisionTarget` — adds placement identity: `def` (`StructureDef`). No health — a
  structure is removed by selling it, never by damage.
- A structure carries one (or more) behaviour components:
  - `Spawner` (produces units; the house — an `IEntrance`) — `[RequireComponent(typeof(Structure))]`.
  - `Tavern` (serves a drink and holds the guest — an `IEntrance`) — `[RequireComponent(typeof(Structure))]`.
  - `ResourceSource` (produces resources) — `[RequireComponent(typeof(CollisionTarget))]`.
  - `ToolRack` (hands out a profession — an `ICollisionEffect` on a walk-through trigger).
  - `AnimalSpawner` (produces animals — mobile resource nodes) — `[RequireComponent(typeof(Structure))]`.
  - `VisitZone` (rations hits per visit — a gate, not an effect) — on a trigger CHILD of the target,
    `[RequireComponent(Collider2D, Rigidbody2D)]`.
  - `ForgeHeat` (heat per paying hit — a gate AND an `IYieldScale`) —
    `[RequireComponent(typeof(ResourceSource))]`.
- Examples: House = Structure + Spawner · Tavern = Structure + Tavern · Market = Structure +
  ResourceSource (`Depletion.Never`) + VisitZone (the passage) · Smithy = Structure + ResourceSource
  (`Never`) + ForgeHeat (+ `ForgeHeatView`) · Tree/Wheat/Rock/Bush = Structure + ResourceSource
  (`Regrow`) · Tool rack = Structure + ToolRack · Stable/Den = Structure + AnimalSpawner ·
  Mountain/River = a bare Structure (a wall the generator places; `canSell`/`canMove` off).
- Looks are components too, and never gameplay: `SpriteShadow` (a drop shadow made from the sprite
  at Start, so the build-mode ghost — every behaviour disabled — never gets one), `WaterReflection`
  (the silhouette in the water), `DualVisual` (one of two roots: a fence by orientation, a forest by
  grid-row parity — the caller decides), `TiredView`, `ProfessionView`, `ForgeHeatView`.

**Footprints and exits.** `StructureDef.size` is the footprint's box; `footprintMask` paints which
of its cells the structure really takes (`StructureDef.Footprint`, e.g. the smithy's staircase), and
the grid, placement, halo, animals and exits all follow that shape. `border` claims a ring of cells
around it. `passable` (units cross it: bush, field, rack) and `animalsAvoid` (AI only) are separate
flags. Where a unit may LEAVE a structure is `StructureExits` (static): an exit is a land cell one
step outside the claimed territory that shares an unfenced edge with it and holds no solid occupant;
`TryPickDirection` picks one at random (+ jitter) and `ExitPoint` puts the unit where the ray from
the box centre leaves the footprint, plus its radius and a gap. A house launches, a tavern lets its
guests out and a stable or den lets its animals out this way; sealed in on every side, each keeps
them inside and tries again later.

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
(refuses while overheated; it asks its sibling `ResourceSource` whether the worker is one it pays,
so a lumberjack bouncing off never heats the forge). The per-visit number lives on the component, not in
`ResourceSourceDef`, and is the `MarketVisitHits` stat, resolved on entry — a unit already inside
finishes the budget it came in with.

**Stamina and Tired** (design doc: *Stamina and Tired*, *Population and identity*). An outing is a
stamina clock: `UnitDef.stamina` seconds of field work (× `UnitStamina`), ticking in
`Unit.FixedUpdate` the whole time the unit is out of a house, boosted or not — a boost makes the
outing more productive, never longer. Stamina > 0 = WORKING; 0 = TIRED. `Unit.IsTired` is the one
derived flag; there is no second variable. The state decides exactly two things:
- **Dispatch.** `CollisionTarget.HandleHit` asks every `IEntrance` on the target first, for every
  unit: WHO may come in is each building's own door rule (`TryEnter` decides and takes the unit in
  one call) — a house takes the tired and the drunk, a tavern anyone sober with a seat free. Past the
  entrances a tired unit stops; a working one goes on to gates → effects. So a tired unit pays
  nothing anywhere (no market visit is debited, the forge does not heat) and a working unit gets into
  a house only drunk. Entrances check the unit's state themselves, on purpose — every door differs;
  gates and effects never do.
- **Speed.** `Unit.TargetSpeed` = base (`UnitSpeed`) × `UnitDef.tiredSpeedMultiplier` while tired ×
  the drunk factor while drunk (`tiredSpeedMultiplier` is `[Range(0.1, 1)]` — never zero, a tired
  unit must stay distinct from a stopped one). `FixedUpdate` holds the speed AT TargetSpeed every
  step — a bounce off a moving (kinematic) animal is taken in the animal's frame and would otherwise
  fling or stall the unit — and every boost decays to it, so a unit that tires or sobers mid-boost
  eases instead of snapping. `Unit.OnBecameTired()` is the single working→tired transition and only
  zeroes the clock; the next step reads the new speed. A unit at a standstill on the field is WEDGED
  (a boar pinned it and the solver killed its velocity) and gets kicked out in a random direction.

Only a house refills stamina: `Spawner.TryEnter` (tired or drunk) → `TryShelter` → `Unit.EnterRest`
(stamina zeroed while inside, sobered up, the rack's tool handed back; the unit stays counted in the
population) → `restDuration` (× `SpawnerRecharge`) → `Unit.Launch` with a full clock, through an
open side. Any house takes any such unit, whichever house launched it and whatever it worked as — a
house provides population, not professions; a full house is a plain wall. There is **no lockout**
after a launch: the unit that just left is rested and sober, so the door keeps it out. A tap is speed
only (`Unit.Boost`); `TapSystem.refreshStamina` (default off) turns it into a full restart — the hook
for a future perk. `TiredView` (the bars over the head) polls `IsTired` and is presentation only.
Planned on this skeleton, not built: `Unit.SpendStamina` for buildings that charge per paying hit or
per work period (calls the same `OnBecameTired`); more buildings that hold a worker without a reset
(mine, bakery — `EnterHold` is built, for the tavern); a state-neutral hook for things like spring
towers.

**Professions (tool racks).** Houses provide the population, racks the professions. A `ToolRack` is
a one-cell walk-through structure holding ONE tool of one `ProfessionDef`: an Unassigned worker that
crosses it while the tool is on the stand takes it (`Unit.Equip`) and works as that profession for
the rest of the outing; the tool goes back only when the outing ends (`Unit.Unequip`: house entry,
despawn). So the number of racks IS the number of workers of that profession at once. `Unit.Type` is
derived from `Unit.Profession`, and every yield table and stat scope keys on it. `ProfessionDef` (one
per `UnitType`, Unassigned included) also carries the look — frames that `ProfessionView` cycles
itself (no Animator).

**Tavern, Drunk and the autopilot** (design doc: *Tavern*). The `Tavern` (`IEntrance`) lets in any
SOBER unit with a seat free, working or tired, and pays `payout` coins ONCE on entry through
`AddHarvest(coinSource, …)` — the service exception: a tired worker, who may not work, still pays.
Inside, the unit is HELD, not rested (`Unit.EnterHold`): it keeps its stamina, profession and tool,
and its stamina clock stands still. The unit remembers its tavern (`holder`) so a despawn from inside
gives the seat back (`SpawnSystem.Despawn` → `Unit.LeaveHold` → `Tavern.Forget`). After
`stayDuration` it leaves through an open side, walking, and **Drunk** (`Unit.MakeDrunk(duration,
speedMultiplier)`): a timed status in `Unit`, ticking on simulation time, whose only effect is the
speed factor. A house takes a drunk unit in and sobers it; a tavern turns it away — so tavern → house
is a legitimate loop. Trap: tired × drunk below ~0.12 of base speed reads as STUCK (see below).

A tired guest with `Tavern.autopilot` on leaves on **autopilot** — the lore: a drunk finds the way
home without knowing how. The tavern only switches it on (`Unit.EnterAutopilot`, after `Resume`); the
unit does the rest: `IsOnAutopilot` = switched on AND drunk, so it ends when the unit sobers up (then
it is on its own — it may walk into a tavern again, and pay again) and on every way off or onto the
field. At every bounce on autopilot `Unit.OnCollisionEnter2D` asks `BilliardAim.TrySteer` (static,
the autopilot's eyes): the houses with a free slot within `Unit.autopilotRadius` (from `SpawnSystem`,
injected on spawn), shuffled; a way to one is taken if it turns no more than
`autopilotMaxTurnDegrees` off the physical reflection, leaves at least 15° off the wall, and the first
thing a cast of the unit's body circle meets along it is a free house (animals ignored). Otherwise
the bounce is a plain one. No destination is kept — every bounce looks again from where the unit is
— so a knock from an animal or a house that filled up meanwhile is just another bounce. Cost: under
one `CircleCast` per bounce (most houses fail the angle test first), ~0.05 ms in the editor. Tried
and dropped (2026-09-29, measured): predicting the whole path at release (banks off walls) — ~12 ms
per aim in one frame for a 360-direction sweep, 1.3 ms once cut to the first shot that works, and no
better at getting units home than looking at each bounce; aiming the tavern's exit at a house — every
guest went to the one house in sight and the first ones filled it.
`AimProbe` (end of `BilliardAim.cs`) logs every look and outcome with a running TOTAL line; it is
`[Conditional("AIM_PROBE")]`, so it compiles away unless that scripting define is set.

**Stuck rescue.** A unit that cannot get anywhere cannot free itself (a tired one can't chop, a
farmer never could). `StuckWatch` (on `Unit`, field only) cuts time into `stuckWindow` windows (5 s):
a window in which the unit never got further than `stuckRadius` (1) from where it started it = STUCK
— pinned inside a regrown tree, wedged, sealed into a pocket; bouncing alone never trips it.
`SpawnSystem` sweeps every `stuckSweepInterval` (0.5 s) and shelters each stuck unit in the nearest
house with a free slot (`Spawner.TryShelter`, no door rule — any unit); with none free it tries again
next sweep. Every way off or onto the field resets the watch.

**Two placement kinds** (`StructureDef.placement`): `Cell` — a footprint of cells (the default), keyed
by `Vector2Int` in `RunContext.structures`; `Edge` — sits on the boundary line between two cells
(fences), keyed by `Edge` in `RunContext.fences`. Cells and edges share one integer lattice
(`Edge.cs`), so there is no offset between the two coordinate spaces. `StructureDef.requiredAge`
gates a card in the build palette until the run reaches that age. `canSell` / `canMove` say whether
build mode may sell or pick a structure up — off for the generated mountains and rivers, which are
part of the island rather than something the player owns; generated trees and the starting house
stay sellable (there is no "player-placed" distinction, only what a def allows). `PlacementTarget`
answers both, so the hover tint and the click agree.

Units are villagers: a unit leaves a house as its `UnitDef` was born (the Unassigned villager) and
works as whatever profession it picks up (Farmer / Lumberjack / Hunter / Miner / Blacksmith — see
*Professions* above); military (Swordsman) is a later second kind.
**Animals (Alpaca/Boar/Fox) are NOT units** — they are mobile resource nodes: a standalone
`CollisionTarget` (not a `Structure`: no def, no grid cell — the first direct use of the base class)
+ an ordinary `ResourceSource` (the harvest, read from its `ResourceSourceDef` like a tree's) +
`Animal` (only the link back to the den, so it can refill the slot) + `AnimalWander` (kinematic
wandering). An animal is identified by its `ResourceSourceDef` asset, like a tree. It comes out of
its stable or den through a free side (`StructureExits`, see *Footprints and exits*) and wanders the
den's territory; a kinematic body gets no callbacks against terrain or other kinematic bodies, so
`AnimalWander` polls instead — a look-ahead probe stops it at the island's edge and at structures
flagged `animalsAvoid`, and a comfort distance parts two animals that come too close.

**Depletion** is one axis on `ResourceSourceDef.depletion`, and every source is one of three:
`Regrow` (after `hitsToDeplete` paying hits it stays in place and comes back after `regrowTime` ×
`SourceRespawn` — tree, field, rock, bush; its colliders switch off meanwhile unless
`keepBodyWhileDepleted`, as the shorn alpaca keeps walking), `Despawn` (used up and destroyed, the den
brings a new one — boar, fox; never a structure, which would leave its cells taken) and `Never` (pays
on every hit — market, smithy).

Both spawner kinds implement **`IStructureSpawner`** (`ResetForBuildMode` / `Warmup`) —
`SpawnSystem`'s registry drives build-mode transitions through the interface. Internals are
deliberately NOT shared: the unit launch→return→rest slot cycle and the animal
die→cooldown→replace cycle have nothing in common beyond that contract.

---

## The Island — generation, zones, drawing

**Generation** is a port of `Prototypes/IslandShapeLab` (its README is the spec). The island grows
section by section — the start, then one zone per age — and `IslandGenerator` (plain C#) decides only
which cells exist:
- `GenerateStart()` / `Propose(n)` sample candidate shapes, each valid on its own against the island
  as it stands (`IslandShape`: area within ±10% of the target, `minWidth` everywhere, connected, no
  holes, narrow bays filled, a broad join, a usable interior). Sampling is grouped by shape FAMILY
  (translations and rotations), so a shape proposed often gains no odds.
- `Populate(candidate, biome, house)` fills one with a biome's content (`IslandContent`: reservations
  first, then mountains, a river and scattered objects, each only where the island stays reachable —
  counting the PLANNED bridges as walkable). Content draws from its own rng streams
  (`IslandRng.Derive`), so populating three candidates never changes the shapes still to come.
- `Commit(candidate, content)` makes it an `IslandSection`: permanent cells, plus the
  `IslandSectionContent` (biome, features, and the generator's own reservations, which the game never
  sees). A candidate proposed before another commit is stale, and the generator throws.

Everything is drawn from `IslandRng` (PCG32, not `System.Random`, whose sequence differs between
runtimes), so a seed plus the same picks rebuilds the same island. The rules are `IslandRules` on the
`StartConfigDef` (with `islandSeed`: 0 = random, and the seed used is logged). They are hard
constraints, never loosened on the fly: when no valid zone exists, that is reported and the age goes
on without growing — the seed sweep in `IslandShapeTests` is what keeps a tuning from failing in
play. A biome is a
`BiomeDef` asset: the `IslandBiome` profile (terrain, budgets in % of the zone, river chance,
mountain and river defs, `IslandObjectRule`s — chance, min..max, weight, clustered), its tile set,
and the zone card's name, icon and preview colour. The start is `StartConfigDef.startBiome` (a
gentle profile) plus `house`, whose footprint the start zone keeps clear. Mountains and rivers are
STRUCTURES, not terrain: `TerrainType` (Grass / Forest / Desert) is a zone's ground, read by
`allowedTerrain` and the painter, and the sea is simply the absence of a cell. `IslandSystem`
validates every biome at run start (a bad one is reported and skipped) and places a zone's content
through `StructureSystem.PlaceInitial` — the same registry as anything the player builds.

**The zone pick.** `IslandSystem.ProposeZones()` → `ZoneOffers.Build`: one proposal per biome, biome
i starting on shape i and falling through to the others; a biome that fits nothing gets no card (a
warning). Each `ZoneOffer` (biome + shape + content + counts per `StructureDef`) is populated BEFORE
the pick, so the card promises exact numbers and the pick commits exactly what was shown. It runs
synchronously on the frame the age is bought — a few ms for three biomes even on a late island.
`ZoneSelectionUI` puts up a `ZoneCardUI` per offer; hovering a card gives it the focus, which is
sticky: only the focused zone is drawn (`ZonePreviewOverlay`, a quad mesh in the biome's preview
colour) and the camera goes to it (`CameraController.SetExtraBounds` lets the clamp reach the water,
`FocusOn`). A plain click publishes `ZoneSelectedEvent`. There is no cancel — the age is paid on entry.

**Drawing.** The grid is the truth; `IslandDrawing` (plain C#) only decides which of its cells the
ground and trim tilemaps SHOW, which is what lets a committed zone stay hidden and then rise.
`IslandTilePainter` (static) picks each cell's piece from its neighbours, each terrain in its biome's
`IslandTileSet`; the coastline outline is drawn on WATER cells, on its own collider-less tilemap, so
the grid stays exactly the playable cells. During a rise `IslandBend` bends the land as one sheet: a
height per grid corner in a small texture that the "Little Peeps/Sprite Lit Bend" shader reads per
vertex, so neighbouring tiles never come apart; the old island is pinned, and what stands on the land
rides up by the height under its root.

**Water.** `WaterSystem` spawns Modern 2D Water from a prefab at run time (the package runs in edit
mode and would churn `SampleScene.unity` on every save), copies the drawn coast into the water's
obstruction layer after every `IslandRepaintedEvent`, sizes the south-coast foam in art pixels and
pulses it on real time, draws side foam along the east and west edges, and bends its coast copies
with the land during a rise. `WaterReflection` gives a prefab its silhouette in the water — it adds
the package's `Reflector` at Start, set up the one way that works for our art; never put the
package's `Reflector` on a prefab directly (it runs in edit mode and litters the scene).
`UnscaledShaderTimeFeature` (a URP renderer feature) feeds shaders unscaled time, so the sea keeps
moving through every pause.

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
run state that cannot be rebuilt from a def + an id; that is what keeps a run save small. The island
keeps to it too: its seed plus the zones picked rebuild it cell for cell (`IslandRng`).

**Lifecycle:** `StartNewRun` tears the previous run down first (`EndRun`: pier → structures → spawn
system, order load-bearing), creates a fresh context, seeds it from `StartConfigDef` (resources,
baseline modifiers), initialises the run-scoped systems, generates the island from the same config
(seed, rules, start biome, house — the start zone places its own content), places the pier, and
publishes `RunStartedEvent` LAST so observers re-bind to a fully built run. Prestige and the debug
"Restart Run" context menu both go through this one path.

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
  `ResourceSourceDef.workerYields[worker].amount`, `ResourceSourceDef.regrowTime`, `Spawner`'s rest
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
| `ResourceYield` | profession × resource × source | `ResourceSystem.AddHarvest` — amount per hit |
| `UnitSpeed` | profession | `Unit.ResolveBaseSpeed` — re-resolved on every launch / resume / boost and on `Equip`, so "lumberjacks walk faster" reaches a villager the moment it takes an axe |
| `SpawnerRecharge` | none | `Spawner` — seconds a unit rests inside before launching |
| `UnitStamina` | none | `Unit.ResolveMaxStamina` — seconds of field work per outing (resolved on each launch) |
| `SourceRespawn` | source | `ResourceSource` — seconds a depleted source takes to regrow |
| `ForgeHeatPerHit` / `ForgeMaxHeat` / `ForgeCoolingTime` / `ForgeHotYield` | none | `ForgeHeat` — heat per paying hit, overheat cap, full cool-down seconds, yield multiplier at full heat (×1 until a perk adds to it) |
| `HouseCapacity` | none | `Spawner.ResolveCapacity` via `ApplyCount` — the one **materialised** stat: turned into slots + the global cap at `Warmup`, so a mid-run change is PUSHED (`RunStats.Changed` → `SpawnSystem` → `Spawner.RefreshFromStats`, growth only) |
| `MarketVisitHits` | none | `VisitZone.ResolveHitsPerVisit` via `ApplyCount` — counted hits per visit, resolved on entry |

**The unit axis is the PROFESSION** (`Unit.Type`, what the worker does now — not what it was born
as). Only a stat read while the unit is out working can carry it: the house, the launch and the
stamina clock all happen before a villager has crossed a rack, so `SpawnerRecharge`, `UnitStamina`
and `HouseCapacity` are unscoped on purpose (a house rests whoever comes home).

**Scope normalisation:** `StatMeta.ScopeOf` gives each id's mask; `RunStats.MakeKey` zeroes the
dimensions a stat does not use and derives `resource` from `source` when both are present, in
**both** `Add` and `Apply`, so authored data and the query can never silently miss each other.
**Trap:** the `_ => StatScope.None` default means a new `StatId` without its own line there becomes
GLOBAL — every scoped stat has a test pinning its mask; add one for each new scoped stat.

**The source axis:** an EMPTY `sourceScope` means *any source*, not *its own bucket* — `Apply` sums
the exact-source bucket and the any-source bucket and runs the formula ONCE (twice would multiply
the percents). Rule: `Source` is the only dimension with a meaningful empty value; `Unassigned` /
`Food` are enum zeros, not wildcards — a `ResourceYield` modifier left on `Unassigned` reaches
nobody, since the plain villager harvests nothing.

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

**Event catalogue** (`Core/GameEvents.cs`, which also holds the `[Flags] UIMode` enum — flags so a UI
group can be authored as "visible in THESE modes"; an event always carries exactly one):

| Struct | Published by | Consumed by |
|--------|-------------|-------------|
| `RunStartedEvent` (`Run`) | `RunManager.StartNewRun`, as its LAST step | `AgeSystem`, `TapSystem`, `AgeAdvancePanel`, `AgeCostPanel`, `AgeTimelinePanel`, `BuildPanelUI` — every MonoBehaviour that caches the run re-binds here. On the very first run it reaches nobody (their `OnEnable` has not run yet), which is why `GameBootstrap` still injects by hand (the age panels through `UIRoot.Initialize`) |
| `ResourceChangedEvent` (`ResourceType`, `NewValue`) | `ResourceSystem.AddResource` | `AgeAdvancePanel`, `AgeCostPanel` (affordability). The resource bar does NOT use it — `ResourcePanel` binds each row to the type's `ReactiveValue`, which is why `ResourceSystem.Initialize` must REUSE those objects across runs rather than replace them |
| `HarvestedEvent` (`Source`, `Type`, `Amount`, `Position`) | `ResourceSystem.AddHarvest` — the production gateway itself, NOT its callers | `HarvestVfxSystem` (pickup particles), `HarvestNumbers` (the "+1.2k"). Published from inside the gateway on purpose: any future production path gets its feedback for free. Carries the whole `ResourceSourceDef` because Wheat/Boar/Fox are all Food and Alpaka/Market are both Coins. `Amount` is the CREDITED figure, after both multipliers |
| `AgeStartedEvent` (`Age`) | `AgeSequencer` (banner step, after the rise) | `CameraController` (re-clamp bounds), `PierSystem` (re-snap to the new bottom-right corner), `AgeAdvancePanel`, `AgeCostPanel`, `AgeTimelinePanel`, `BuildPanelUI` (unlock cards by `requiredAge`) |
| `AgeAdvanceRequestedEvent` | `AgeAdvancePanel` (Next Age button) | `GameplayContainerState` — enters `ZoneSelectionState` only from `PlayingState` and only if affordable |
| `ZoneSelectedEvent` (`Offer`) | `ZoneSelectionUI` (a zone card clicked; every card locks at once) | `ZoneSelectionState` — records the pick; its next `Tick` hands the offer to a new `AgeTransitionState`. Same split as the perk pick: the UI shows and reports, the state acts |
| `IslandRepaintedEvent` | `IslandSystem.LateUpdate`, **at most once a frame**, on any frame its `IslandDrawing` painted (run start, zone commit, every tile a rise lands) | `WaterSystem` — rebuilds the coast twins (obstruction + side foam). Once a frame, not once a paint, because a rise lands dozens of tiles in a couple of seconds |
| `BuildModeToggleRequestedEvent` | `BuildModeButton` (click), `GameHotkeys` (B) | `GameplayContainerState`. The key path runs in `Update` past every UI raycast and regardless of timeScale, so the container guards on the CURRENT inner state — any new inner state needs the same guard |
| `SellModeRequestedEvent` | `GameHotkeys` (X) | `BuildPanelUI` (toggles the sell button) |
| `ExitToMenuRequestedEvent` | `GameHotkeys` (Esc) | `GameBootstrap` — DECLINED with a warning while `MainMenuState` is a stub, because entering it stranded the player (B3 restores the transition) |
| `UIModeChangedEvent` (`Mode`: `Playing` / `Build` / `ZonePick` / `PerkPick` / `AgeTransition`) | Each gameplay state, at the moment its screen actually goes up — never at the top of `Enter`, so a state that bails out (an age whose cost moved, an empty perk roll) never makes the mode blink | `UIVisibility` (the one table of what is on screen per mode), `BuildPanelUI` (opens / closes its cards on the crossing into or out of `Build` only). `PlayingState` announces it on every way back — that is the whole "put the HUD back" story |
| `BuildModeButtonStateEvent` (`InBuildMode`, `Interactable`) | `GameplayContainerState` | `BuildModeButton` alone (label swap + interactable). What is VISIBLE in build mode is the UI mode's business; the button is the one thing a mode cannot express, because the cooldown outlives the mode change |
| `BuildDeniedEvent` (`Def`) | `PlaceTool` — a valid spot the player cannot afford (a blocked spot publishes nothing: the ghost is already red) | `BuildPanelUI` → the selected card plays its denied cue |
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
`RunManager`, never a `RunContext`. The UI is ONE reference on it: `UIRoot`, on the Canvas prefab,
lists the panels the game has to reach — the two screens a state drives (`ZoneScreen`, `PerkScreen`)
and the three age panels that need the run (`UIRoot.Initialize`) — and warns in `Start` about a
missing one. Wiring the panels inside the prefab keeps `SampleScene.unity` down to one link instead of
a stub per panel. Deliberately not a singleton and without logic: a panel that wants another hidden
asks by mode, a panel that wants another's data asks the EventBus.

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
(unscaled time, anti-respawn-abuse), switches the inner FSM, and pushes `BuildModeButtonStateEvent`
so the button reflects mode + cooldown. **Both** entry points check the current inner state — build
mode and an age advance start only from `PlayingState`. It builds a fresh `ZoneSelectionState` per
request, which builds the `AgeTransitionState` (the states allowed to hold a `RunContext`: they die
with the transition).

**What is on screen** follows the FSM: each inner state publishes `UIModeChangedEvent` with its own
mode at the moment its screen goes up, and `UIVisibility` maps the mode onto CanvasGroups from one
inspector table (a row per group that differs from its parent — alpha multiplies down the chain).
Per-mode visibility lives nowhere else, and no panel reaches across to hide another: the HUD going
down under the zone cards is a row in that table, not a field on the zone panel. A screen a state
drives stays ACTIVE and is hidden by its CanvasGroup — an inactive panel would run its `Awake` inside
the very reveal that shows it. (The age banner is the sequencer's own fade, not a mode.)

**`ZoneSelectionState`:** on `Enter` runs `TriggerAgeCmd` (spend + `currentAge++` +
`stats.Add(ageDef.modifiers)`), sets `Time.timeScale = 0`, announces `UIMode.ZonePick` and puts
`IslandSystem.ProposeZones()` on the zone screen. The pick (or there being nothing to offer — the
age then goes on without growing) hands the chosen `ZoneOffer` to a new `AgeTransitionState`; an
abort (the cost changed between click and spend) goes straight back to `PlayingState`, because no age
happened and no perk is owed. A missing zone screen is a wiring error: it logs and takes the first
offer rather than softlock or stunt the island.

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
tools between its `Begin()` and `End()`, which this state calls. **Invariant:** a run is never ended
or saved from build mode — `MoveTool` can hold a structure OFF the grid mid-drag, and that invariant
is what makes `EndRun`'s sweep over `run.structures` complete. The debug "Restart Run" refuses from
build mode for the same reason.

**`PlayingState`:** subscribes to `PrestigeTriggeredEvent` for exactly its own lifetime and calls
`PrestigeSystem.ExecutePrestige` directly (no confirmation yet).

---

## Physics Setup

| Object | Collider | Rigidbody | Notes |
|--------|----------|-----------|-------|
| Unit | CircleCollider2D | Rigidbody2D (Dynamic) | Bounces off obstacles; passes through interactables |
| Structure (obstacle) | BoxCollider2D `isTrigger=false` | none (Static) | Unit bounces — `OnCollisionEnter2D` fires on the `CollisionTarget` |
| Structure (interactable) | BoxCollider2D `isTrigger=true` | none (Static) | Unit passes through — `OnTriggerEnter2D` fires; `SetColliderEnabled(false)` during drag / on source depletion (unless the def says `keepBodyWhileDepleted`) |
| Animal (alpaca/boar/fox) | Collider2D `isTrigger=false` (child) | Rigidbody2D (**Kinematic**, root) | Units (Dynamic) bounce off it — that bounce IS the harvest hit. The kinematic body itself passes through everything, so `AnimalWander` steers by polling: destinations from the den's territory, a look-ahead probe that stops at the island's edge and at `animalsAvoid` structures, a comfort distance to other animals |
| Island boundary | TilemapCollider2D on the ground tilemap (Composite Operation: Merge) + CompositeCollider2D (Outlines) | Rigidbody2D (Static) | Follows the tiles as they are painted — a rising zone joins the outline as its tiles land (the sim is frozen meanwhile). The coastline trim sits on its own tilemap WITHOUT a collider, or the coast would grow a cell outward |
| Pier | BoxCollider2D `isTrigger=false` (on a `Physics` child) | Rigidbody2D (Static, root) | An ordinary structure that units bounce off. A click on it triggers prestige: `TapSystem` resolves the hit with `GetComponentInParent<Pier>()`, so the `Pier` marker component must sit on the prefab **root**, not next to the collider |
| Market passage (`VisitZone`) | BoxCollider2D `isTrigger=true` on a `Passage` child (the two walls are ordinary obstacle boxes on the `Physics` child) | Rigidbody2D (Static) on that **same child** | The zone's own body is not optional: Unity delivers a trigger callback to the collider's GameObject AND to the GameObject of the Rigidbody2D it belongs to, so hung off the root body the passage's `OnTriggerEnter2D` would also reach `CollisionTarget`, which treats a trigger entry as a hit — walking in would pay a coin by itself. Sizing: across the passage the trigger overlaps the walls by 1–2 px (a unit can't be inside a wall, so this only guards a seam); along it, it stops one unit radius short of each mouth, so a unit sliding along the outside never counts as inside |

**Collision dispatch lives in `CollisionTarget`** (the base). Both `OnCollisionEnter2D` (obstacle
path) and `OnTriggerEnter2D` (interactable path) call the same `HandleHit(unit)`. Every unit is
offered first to each `IEntrance.TryEnter` on the root (the first that takes it in ends the hit);
past them a TIRED unit stops, and a WORKING unit goes through every `IHitGate.TryConsume(unit)` on
the target (root + children; the first refusal ends the hit as a plain bounce) →
`effect.OnHit(...)` for each `ICollisionEffect` component on the root. `Structure :
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
(`UnscaledShaderTimeFeature`) and its foam pulse, and the island rise — whose particle effects
therefore need **Use Unscaled Time**, and whose camera kick switches
`CinemachineImpulseManager.IgnoreTimeScale` on. The water's ripple simulation steps in `FixedUpdate`,
so it can never run in a pause; it stays off (the water prefab's simulation is disabled).

---

## Assemblies & Tests

One runtime assembly, `LittlePeeps.Runtime` (single `namespace LittlePeeps`; folders give the
structure), plus `LittlePeeps.Editor` and `LittlePeeps.Tests.EditMode` under
`Assets/Little Peeps/Tests/EditMode`. The editor assembly holds the drawers (`StatModifierDrawer`,
`FootprintMaskDrawer`), the inspectors (`AgeDefEditor` and `StatPerkDefEditor` on the shared
`BonusOverrideEditor` base — a live preview of the generated card text over the override field;
`ResourceSourceDefEditor`, which hides the depletion fields the chosen mode never reads) and two Edit
Mode tools that run the game's own code: `HarvestFeedbackWindow` (a reaped node, its particles and
its number, via `HarvestFade` / `HarvestNumberMotionDef` / `HarvestNumberFormat`) and
`IslandRiseWindow` (the rise of a sample island's next zone, scrubbable both ways).
`SceneGridGizmo` is a runtime-assembly component whose drawing compiles out of a build.

Tests cover the risk points, one file each: `RunStats`, `IslandGrid` (+ a grid smoke test),
`EventBus`, `StateMachine`, structure exits, footprints, the stuck watch, the unit's bounce rule, run
teardown, harvest ledger, prestige formula + payout, perk roll, perk state + card, the modifier card
text, placement target, house capacity, the island's shape rules (with a seed sweep, so tuned rules
can never fail in play), island content, zone offers, the zone pick state, the island rise's schedule
and notes, and the coast painter (tile-by-tile repaint must equal a full repaint at every step).
Two shared fixtures keep them free of assets: `TestIsland` (a rectangle of cells, for the grid and
exit geometry) and `TestBiomes` (the prototype's biome profiles, built in code). Physics casts (the
autopilot's `BilliardAim`) have no Edit Mode test: they need a live physics scene. **Edit Mode runs
NO MonoBehaviour lifecycle callbacks** (no `[ExecuteAlways]` anywhere), so a test may never depend on
Unity invoking `Start`/`OnDestroy` — call the method by hand. Anything that must prove a callback
belongs in a PlayMode assembly (none yet).

---

## Systems & Entities Overview

| Component | Type | Responsibility |
|-----------|------|---------------|
| `RunManager` | MB | The one way a run starts and ends. `StartNewRun` = `EndRun` + fresh `RunContext` seeded from `StartConfigDef` (resources, baseline modifiers) → init resource/structure/spawn systems → `IslandSystem.GenerateForRun(seed, rules, start biome, house)` — the start zone brings its own content, the house included → `PierSystem.PlaceForRun` → `RunStartedEvent`. `EndRun` tears down pier → structures → spawn system (order load-bearing: spawners despawn their resting units, then SpawnSystem collects the roamers). `[ContextMenu("Restart Run")]` debug trigger, refused from build mode |
| `IslandGrid` | Plain C# | Grid data: sparse cells (`terrain` + `occupant`), placement validation (`CanPlace/Place/Remove` with footprint, allowed terrain, border), edge registry (`CanPlaceEdge/PlaceEdge/RemoveEdge`), world↔grid and world↔edge conversion; `CellBounds` / `WorldBounds` for the camera and the pier |
| `IslandSystem` | MB | Owns Grid + Generator + `Drawing` and the `biomes` list (validated at run start — a bad biome is reported and skipped). `GenerateForRun(seed, rules, startBiome, house)`; `ProposeZones()` (one populated offer per biome) and `CommitZone(offer, forRise)` — driven explicitly by `AgeSequencer`, not an event. Places every zone's content through `StructureSystem.PlaceInitial` (house first, then mountains and river, then objects). `forRise` commits the zone whole (grid, run, structures) but hides its land and switches its structures off in the same frame, so their `Start` waits for the rise to switch them on. Remembers `LastSection` / `LastContent` (what the rise brings up); publishes `IslandRepaintedEvent` once a frame. `[ContextMenu] Generate Island` previews one in the editor |
| `IslandGenerator` | Plain C# | Decides which cells exist, section by section (the prototype's `island_shapes.py`): `GenerateStart` / `Propose(n)` sample valid shapes grouped by family, `Populate` fills one with a biome's content, `Commit` makes it a section; a stale candidate throws. Knows nothing of tiles, terrain or structures. `Seed` / `Land` / `LastAttempts` for the logs |
| `IslandShape` / `IslandContent` | static | The generator's two halves: shape checks and operations on sets of signed cells (width, holes, bays, joins, connectivity; every set sorted before sampling, so a seed reproduces); a zone's content (the prototype's `island_resources.py`: reservations, mountains grown in 2×2 blocks, a river, the biome's objects — each only where the island stays reachable, planned bridges counted) |
| `IslandRng` | Plain C# | PCG32, the generator's only random source; `Derive(seed, parts…)` makes the independent streams content draws from |
| `IslandSection` / `IslandCandidate` / `IslandSectionContent` | Plain C# | A committed piece of the island (permanent cells, content or bare); a proposal, valid against the island version it was proposed on; what a zone holds — biome, features, and the generator-only reservations the game never sees |
| `ZoneOffer` / `ZoneOffers` | Plain C# | One zone on offer: biome + shape + content already generated + counts per `StructureDef` (the card's text). `ZoneOffers.Build` pairs biomes with shapes — pure, tested offline |
| `BiomeDef` / `IslandBiome` / `IslandRules` | SO / data | A biome asset (profile + tile set + the card's name, icon and preview colour); the profile the generator reads (terrain, budgets, `IslandObjectRule`s); the shape rules on `StartConfigDef` |
| `IslandDrawing` | Plain C# | The island as DRAWN: the ground + trim tilemaps painted from the grid, minus the cells held back (`HideLand` / `HideOnly` / `RevealLand` — the last repaints only the 3×3 — / `RevealAllLand`); per-cell tint (`TintLand`); `GroundTileAt` (the tile a rising cell will have when it lands). Plain so the rise's tuning window can draw an island of its own |
| `IslandTilePainter` | static | The autotiler: `PaintOf(land, cell)` decides a cell's ground or outline piece from its neighbours, each terrain in its biome's `IslandTileSet` (an outline takes the terrain of the land cell that defines it); `Repaint` (all) and `RepaintAround` (the 3×3 one cell can change) both go through it — tested to agree at every step |
| `IslandBend` | Plain C# | The land bent as one sheet during a rise: a height per grid corner in a small half-float texture, read per vertex through shader globals by "Little Peeps/Sprite Lit Bend" (and "Sprite Flat Color"). A material bends when its weight is above 0; `HeightAt` lifts what stands on the land. One bend at a time — whoever begins it ends it |
| `IslandRiseSchedule` | Plain C# | The rise as a function of time, worked out once: each cell's start (wave order modes, noise, jitter, crescendo; normalised so any zone takes the cascade's length), its phases (under water → hop → squash → settled), note rank, the structures' pops (after their land; the river runs in from its source), the breath from the join, the finale and the banner time. No engine calls — fully unit-tested |
| `IslandRise` | Plain C# | Plays a schedule on an `IslandDrawing`: pooled stand-in sprites for tiles in flight (swapped for the real tile as each settles), drying via tile colour, standing neighbours bounce, the new zone breathes — the land bending as one sheet (`IslandBend`), the old island pinned, what stands on it riding up by the height under its root and a den's animals by the height under their feet — structures pop by root scale (a den's animals with it), effects / notes / camera kick on forward play only. `Tick` (forward) / `Seek` (any time, both ways) / `SpeedUp` / `SkipToFinale` / `Finish`. The SAME code runs in the game and in the tuning window |
| `IslandRisePlayer` | MB | The game's driver of `IslandRise`: `Play(section, content, onDone)` on unscaled time, capped per frame by the profile's slow-frame setting; the tap's `SpeedUp` / `SkipToFinale`; finishes whatever cuts it short (nothing is ever left hidden). With a `WaterSystem` the tiles in the air push on the water. `[ContextMenu] Replay Last Zone` for Play Mode tuning |
| `IslandRiseProfile` | SO | Every knob of the rise: `timing` (plain `IslandRiseTiming`: wave, tile phases, act 2, river, finale + breath) and the looks (depth scale/tint, hop, squash, wetness, pops + per-`StructureDef` overrides, bounce, breath, pixel snap, water, effect/sound slots, shake, tap speed-up, slow frames). Read live — game and window alike |
| `SharedEmitter` | static | The one-shared-ParticleSystem-per-effect rule (World space, looping, no self-emission; `pausedPlay` also demands Use Unscaled Time): `Create` validates loudly and refuses, `EmitAt` moves + emits. Used by `HarvestVfxSystem` and the rise |
| `WaterSystem` | MB | The sea: Modern 2D Water spawned from a prefab at run time (never a scene object — the package's edit-mode code would churn the scene); the coast copied into the water's obstruction layer after every `IslandRepaintedEvent`; the south foam sized in art pixels and pulsing on real time; side foam along the east and west edges; coast copies that bend with the land during a rise. `HasWater` for the rise |
| `UnscaledShaderTimeFeature` | URP renderer feature | Shader time from unscaled time — always, since switching at a pause would jump every wave's phase — for every camera on the 2D renderer, so the sea keeps moving through build mode and the picks |
| `StructureSystem` | MB | Place / Sell / Remove / PickUp / Drop for BOTH cell and edge structures; `PlaceStructure` validates + spends, `PlaceInitial` is the free path for generated content (zone content, the starting house) and the pier — still validated, it warns and skips a blocked cell; `ClearAll` sweeps the run's registries on `EndRun`. Returns the created instance so owners (the pier) can track and move it |
| `PlacementController` | MB | Build-mode input router, active between `Begin()` / `End()` (called by `BuildModeState`): the panel selection chooses the tool — card → `PlaceTool`, sell button → `SellTool`, nothing → `MoveTool` (default). Right-click cancels the tool's action or clears the selection (`ToolCleared` → panel). Clicks over UI are ignored. Owns the inspector-authored `PlacementVisuals` |
| `IPlacementTool` / `PlaceTool` / `MoveTool` / `SellTool` | Plain C# | One tool active at a time. Contract: `Exit` leaves NOTHING behind (no ghost, no tint, no half-finished drag). `PlaceTool` is one instance per `StructureDef` (ghost, cell or edge); `MoveTool` is the only tool with state between clicks and the only one that holds a structure off the grid; `SellTool` refunds `sellRefundPercent`. Sell and Move ask the target's `CanSell` / `CanMove` |
| `PlacementTarget` | struct | THE answer to "what is under the cursor" — fence on an edge, structure on a cell, or nothing — and whether its def may be sold or moved; shared by Sell, Move pick-up and the hover highlight, so the fence-wins rule exists once |
| `PlacementVisuals` / `HoverHighlight` / `GridOverlay` | Plain (serialized on the controller) / Plain / MB | Everything build mode DRAWS that is not a real structure: ghost + territory halo (a quad per claimed cell — the footprint's shape plus its border) + hover/drag tints (visuals; a shadow is never tinted, the ghost has none and a carried structure hides its own); cursor-target tint with meaning chosen by the tool (highlight); one procedural quad mesh outlining every cell (overlay — thin quads, not `MeshTopology.Lines`, which URP 2D draws unreliably) |
| `UnitPool` | MB | Pool per UnitDef; `Get` / `Release` |
| `UnitSystem` | MB | Live-unit registry (`ActiveUnits`); fed by **direct `SpawnSystem` Add/Remove** (no events); home for future bulk ops |
| `SpawnSystem` | MB | Bridges Spawner ↔ UnitPool; the population cap per `UnitDef` (houses register their slots — never per profession); syncs UnitSystem; owns the **spawner registry** (`IStructureSpawner`: unit Spawners + AnimalSpawners); `DespawnAllAndResetSpawners` / `WarmupAllSpawners` (build mode, `IsBuildMode`); `ResetForNewRun`; injects the island, `RunStats` and itself into each spawned unit; `Despawn` hands a tool back and frees a tavern seat (`LeaveHold`). Sweeps for STUCK units every `stuckSweepInterval` and shelters each in the nearest house with a free slot; `CollectHousesWithFreeSlot(from, radius, buffer)` for the autopilot |
| `ResourceSystem` | MB | One `ReactiveValue<float>` per resource type, REUSED across runs (`Initialize` assigns into the existing slot — replacing it froze the bar after a prestige). `AddHarvest(source, worker, base, position)` is the single production gateway (applies `ResourceYield` + `ProductionGlobal`, books `harvested`, publishes `HarvestedEvent`); `AddResource` / `Spend` / `CanAfford` stay raw for spends and refunds |
| `PierSystem` | MB | Owns the pier for a run: `PlaceForRun()` (after island gen) drops it in the bottom-right corner; on `AgeStartedEvent` re-snaps to the new bottom-right corner via `StructureSystem` pick-up/drop (kept in place when the new slot has no room); `ClearForRun()` on teardown. Not generated content — the single owner of the pier's cell |
| `TapSystem` | MB | Click on the world: boosts units in an AoE radius around the cursor — speed only by default, `refreshStamina` (off) also refills stamina; a click on the pier publishes `PrestigeTriggeredEvent`; a `TapRadiusVisual` ring follows the cursor in live play. Early-returns at `timeScale 0`, which is what makes every pause also an input block. Re-binds on `RunStartedEvent` |
| `AgeSystem` | MB | Owns the ordered `List<AgeDef>` catalogue; `TransitionFrom(age)` / `NextAge` / `CanAdvance`. Current age lives on `RunContext`, so the system is stateless between runs; re-binds on `RunStartedEvent` |
| `AgeSequencer` | MB | Coroutine chain for an age transition, unscaled time, no fade: commit the zone for the rise → camera to it (`RefreshBounds` + `FocusOn` — the bounds would otherwise catch up only at the banner) → `IslandRisePlayer.Play` → "Age N" banner (its `CanvasGroup` faded in and out). Taps from `InputHandler`: ×3, then skip, then close the banner after `bannerTapGrace`. Without a rise player the zone simply appears. Signals completion via callback. Pure choreography: every step takes a KNOWN time — the zone and perk picks, which wait on a human, are states instead |
| `PerkSystem` | MB | `RollPerks(age, run)`: weighted draw WITHOUT replacement from `PerkCatalogueDef`, filtered by `minAge` and `perksChosen`, fewer than `choicesOffered` when fewer are eligible (never filler). `ApplyPerk` calls `perk.ApplyPerk(run)` and records it. Validates ids at startup (empty / duplicate = a future lost-perk bug). Never casts to `StatPerkDef` — a perk with behaviour is its own `PerkDef` subclass |
| `PrestigeSystem` | MB | Owns the payout. `Calculate` = `PrestigeFormula.Points(run, meta)`: an age term (`pointsPerAge × currentAge`) and a harvest term (`coefficient × weightedHarvest^exponent`), each paid only for what it BEATS of the profile's record. `ExecutePrestige` reads the payout and both gross terms BEFORE banking, saves (no-op today), then `RunManager.StartNewRun`. `CanPrestige` gates the pier on `pierUnlockAge` |
| `SaveSystem` | MB | JSON persistence of `MetaContext` (**stub**: `Load` returns a fresh context, `Save` does nothing) |
| `GameHotkeys` | MB | Discrete hotkeys → events (B build mode, X sell, Esc exit-to-menu), bindings editable in the inspector. Runs in `Update`, past UI raycasts and timeScale — consumers guard |
| `InputHandler` | MB | Raw input router (screen → world); read by `PlacementController`, `TapSystem` and `AgeSequencer` (the rise's taps — it reports clicks at any time scale). `CameraController` polls the devices itself |
| `CameraController` | MB | The camera's brain, on its own object: pans a `CameraTarget` that the Cinemachine camera follows (WASD / arrows, the screen edge, middle-drag) and zooms its ortho size (wheel), polling the devices itself on unscaled time. Clamped to `IslandGrid.WorldBounds` + a margin: `RefreshBounds` on `AgeStartedEvent` (and when `AgeSequencer` asks, at the start of a rise); `SetExtraBounds` / `ClearExtraBounds` widen the clamp to the zone offers; `FocusOn(point, offsetY)` sends the target to a point |
| `HarvestVfxSystem` | MB | Pickup particles for every harvest. **One shared emitter per `ResourceSourceDef`** (the `SharedEmitter` rule), made from `def.pickupFx` on first harvest and reused (one system emitting many particles = one draw call). Moves the emitter and `Emit`s — deliberately not `EmitParams`, so the authored Shape module keeps working. **Validates instead of silently fixing** a prefab that isn't World-space/looping. Skips off-screen harvests |
| `HarvestNumbers` | MB | The floating "+1.2k" — universal, one prefab and one set of curves for every resource. Pooled world-space `TextMeshPro`, driven by one flat loop over a fixed-size array (no coroutine, no MonoBehaviour per popup); `SetText` overloads format in place, nothing lands on the GC. Cap + recycle-nearest-death. This owns the plumbing; **the feel lives in `HarvestNumberMotionDef`**, an asset shared with the `HarvestFeedbackWindow` |
| `HarvestFade` / `HarvestNumberFormat` | static | One implementation each of a reaped node's fade + squash and of the number's spelling ("+1", "+2.5", "+1.2k"), shared by the game and the `HarvestFeedbackWindow` so the preview cannot drift |
| `CollisionTarget` | MB (base) | Collision callbacks → `IEntrance` (root) for every unit, the first that takes it in ends the hit → a tired unit stops there → working: `IHitGate` check (root + children) → `ICollisionEffect` dispatch (root); `SetColliderEnabled` toggles every collider under it, a `VisitZone` trigger included |
| `Structure` | MB : CollisionTarget | Placement identity: `def` (`StructureDef`) |
| `IEntrance` | interface | `TryEnter(unit)` — a building a unit can go INTO; decides by its own door rule and takes the unit off the field in one call, asked for every unit before anything else. Implemented by `Spawner` (tired or drunk) and `Tavern` (sober, seat free) |
| `IHitGate` | interface | `TryConsume(unit)` — decides AND spends in one call; asked before any effect, one refusal = plain bounce. Effect-agnostic, so a gate composes onto any target; one rationing gate per target. Implemented by `VisitZone` and `ForgeHeat` |
| `ICollisionEffect` | interface | `OnHit(unit, target)` — what a hit by a WORKING unit does once every gate let it through; the worker check lives inside. Implemented by `ResourceSource` and `ToolRack` |
| `IYieldScale` | interface | `Factor(unit)` — a per-hit multiplier on what a `ResourceSource` pays, UNDER the run's modifiers; asked only for a hit that pays, must not mutate. Implemented by `ForgeHeat` |
| `VisitZone` | MB, `IHitGate` | Per-visit hit ration (`hitsPerVisit` on the component, not in a def): `Dictionary<Unit,int>` filled on trigger enter, debited per hit, dropped on exit. Sits on its own child with the trigger AND a Static Rigidbody2D so its trigger events stay off the root; `Reset()` sets both up, `Awake` errors if the collider isn't a trigger |
| `ForgeHeat` | MB, `IHitGate`, `IYieldScale` | The smithy's heat: every paying hit adds heat (only hits the source actually pays for); reaching the cap OVERHEATS it — plain bounces until it has cooled all the way to zero; cooling never stops. Hot yield = `ForgeHotYield` (×1 until a perk) shaped by the `hotness` curve, sampled after the hit's own heat. All four numbers are stats, resolved at use; `HitPaid` for presentation |
| `ForgeHeatView` | MB | The heat bar in the smithy's window: a flat cover shrinks over a fixed gradient (the fill never moves or stretches); overheat darkens the fill and lights overlays that fade with the HEAT, not a clock. Authored cold, so the build ghost shows a cold forge. Presentation only |
| `IStructureSpawner` | interface | Build-mode contract shared by both spawner kinds: `ResetForBuildMode` (enter) / `Warmup` (placement + exit) |
| `Spawner` | MB, `IEntrance`, `IStructureSpawner` | The house. Per-slot spawn → travel → rest cycle, slot = `Free ↔ Occupied` (no lockout); the door (`TryEnter`) takes a tired or drunk unit into any free slot, whichever house launched it and whatever it works as; `TryShelter` is the same without the door rule (stuck rescue); launches through an open side (`StructureExits`); self-registers with SpawnSystem; base `capacity` × `HouseCapacity` materialised into slots at `Warmup`, grown on `RefreshFromStats`; rest duration through `SpawnerRecharge`; launch boost (`launchSpeedMultiplier` / `launchBoostDuration`) |
| `Tavern` | MB, `IEntrance` | Lets in any sober unit with a seat free (`capacity`), pays `payout` coins once on entry, holds it `stayDuration` (`Unit.EnterHold`), lets it out Drunk through an open side; with `autopilot` on, a tired guest leaves on autopilot. Sealed in → keeps its guests and tries again after another stay. Injected by StructureSystem (resources, grid, instance); `Forget(unit)` frees a seat on despawn; `Occupied` / `Capacity` for a future occupancy view |
| `Unit` | MB | Bouncing villager: stamina clock (`UnitDef.stamina` × `UnitStamina`, ticks whenever on the field) → `IsTired`; Drunk (`MakeDrunk`, timed) → `IsDrunk`; `TargetSpeed` (`UnitSpeed` × tired factor × drunk factor) held every step; profession for the outing (`Equip` / `Unequip`, `Type`); `Launch` (house — refills), `Resume` (out of a hold — refills nothing), `Boost(mult, duration, refreshStamina)` (tap), `EnterRest` (house: stamina 0, sober, tool back), `EnterHold` / `LeaveHold` (tavern), `OnBecameTired` (the one transition); the bounce rule (`minBounceAngle` bend + `bounceJitterDegrees` turn, `BendAwayFromNormal`), or the autopilot's turn to a house (`EnterAutopilot`, `autopilotRadius`, `autopilotMaxTurnDegrees`); `StuckWatch` → `IsStuck`; wedged kick; injected with the island, `RunStats` and `SpawnSystem` on spawn |
| `StuckWatch` | struct | Fixed windows: a window the unit never left a disc of `radius` = stuck; the verdict clears once it gets out. No dependency beyond `Vector2` — tested offline |
| `StructureExits` | static | Where a unit or an animal may leave a structure on the grid (`CollectAllowedDirections`, `IsOpenExit`, `TryPickDirection` + jitter) and where it is put (`ExitPoint`: the ray from the box centre out of the footprint, plus a clearance). Parameterised by grid + origin + footprint + border, not components — tested on a bare `IslandGrid` |
| `BilliardAim` | static | The autopilot's eyes: `TrySteer` — the way to a house with a free slot in sight from a bounce (radius, turn limit, ≥15° off the wall, a body-circle `CircleCast` whose first hit must be that free house; animals ignored). Decides nothing; shared buffers, main thread only |
| `AimProbe` | static | Autopilot diagnostics (cost per look, what blocks the view, where units end up, running TOTAL). Every method `[Conditional("AIM_PROBE")]` — compiled away without that scripting define |
| `ToolRack` | MB, `ICollisionEffect` | One tool of one `ProfessionDef` on a walk-through trigger: an Unassigned worker crossing it takes the tool for the outing (`Unit.Equip`); Available / Taken visual roots. No system injection: the tool only ever comes back through the unit holding it |
| `ProfessionDef` / `ProfessionView` | SO / MB | A profession per `UnitType` (Unassigned included): its `type` is what yields and stat scopes key on, its frames are the look; the view cycles those frames itself — the walk animation, no Animator, presentation only |
| `TiredView` | MB | The bars over a tired unit's head: polls `Unit.IsTired`, presentation only, sits under the visual root as a SIBLING of the model — it hides with the unit inside a house, and the walk frames, a flip and the shadow never touch it |
| `ResourceSource` | MB, `ICollisionEffect` | Every harvest, static or on an animal: grants `def.resource` per allowed-worker hit (yield via `ResourceSourceDef.TryGetYield` × any `IYieldScale` on the object, e.g. `ForgeHeat`); after `hitsToDeplete` does what `def.depletion` says — `Regrow` in place after `regrowTime` × `SourceRespawn` (swaps Ready/Harvested visual roots, colliders off unless `keepBodyWhileDepleted`), `Despawn` (destroyed — animals only), `Never`. On depletion the ready root **fades and squashes** (`fadeOutTime`; `fadeCurve` on alpha via `SpriteRenderer.color`, `fadeScaleY` on the root — per prefab, through `HarvestFade`) — gameplay ends at the hit, so the fade can never hand out a free harvest |
| `Animal` | MB | A den's animal: only the link back to its `AnimalSpawner`, so the den can refill the slot when it despawns. The harvest is the `ResourceSource` beside it; movement is `AnimalWander` |
| `AnimalWander` | MB | Kinematic wander: point in the owner's territory → walk straight → pause → repeat. A look-ahead probe (`AnimalSpawner.IsBlocked`: off-island, or an `animalsAvoid` structure) and a comfort distance to other animals stop it and send it off the other way — polled, since kinematic pairs get no callbacks. Without an owner it wanders a plain circle around its start |
| `AnimalSpawner` | MB, `IStructureSpawner` | Keeps ≤ `maxAnimals` animals in the structure's territory (land cells within `territoryRadiusCells`); each comes OUT through a free side (`StructureExits`, `releaseGap` + jitter) — sealed in, nobody leaves until a side opens; one replacement per `spawnCooldown`; an animal that regrows (a shorn alpaca) keeps its slot. Territory follows the building via `instance.Cell`. `Animals` (read-only) lets the island rise pop a new den's animals up with it |
| `Pier` | MB (marker) | Sits on the pier prefab's ROOT: `TapSystem` resolves a click with `GetComponentInParent<Pier>()` from whichever collider was hit |
| `SpriteShadow` | MB | A drop shadow built at Start from the `SpriteRenderer` beside it (black, translucent, one sorting step behind, a few pixels off) and re-synced every frame (sprite, flip, sorting). The ghost never Starts, so it never has one; a carried structure hides its own |
| `WaterReflection` | MB | The object's silhouette in the water: adds the package's `Reflector` at Start in sprite-pivot mode and raises the package's update flag once, so the reflection is actually placed. No edit-mode code — put THIS on prefabs, never the package's `Reflector` |
| `DualVisual` | MB | Shows one of two child roots — a fence by edge orientation, a forest by grid-row parity (neighbours interlock). The caller decides; the build ghost uses it too |
| `StatModifierText` | static | A modifier as card text ("+5% FOOD (FARMER)") — mechanical on purpose, durations named as durations so a sign never reads inverted. Behind `AgeDef.BonusText` and `StatPerkDef.GeneratedDescription`: an empty override keeps a card following the data |
| `ResourceFormat` / `RomanNumeral` | static | The one abbreviation rule for amounts (wallet and prices must shorten alike: floored, ≤ 4 digits + a suffix); ages in roman numerals everywhere the player sees one |
| `UIRoot` | MB (UI) | The UI's front door on the Canvas prefab: the two screens a state drives (`ZoneScreen`, `PerkScreen`) and the three age panels that need the run (`Initialize`). `GameBootstrap` holds this one reference. Not a singleton, no logic; errors in `Start` on a missing screen, warns on a missing panel |
| `UIVisibility` | MB (UI) | The one table of what is on screen: a row per `CanvasGroup` with the `UIMode`s it is visible in, every row applied on each `UIModeChangedEvent` (alpha, interactable and blocksRaycasts together). Boots into `Playing` in its own `Awake`, so its order against `GameBootstrap` cannot matter |
| `ResourcePanel` / `ResourceUnit` | MB (UI) | Top resource bar: one `ResourceUnit` per `ResourceIconSet` entry, bound to its `ReactiveValue`; the icon set's order IS the display order |
| `AgeAdvancePanel` / `AgeCostPanel` / `AgeTimelinePanel` | MB (UI) | The age label and the Next Age button — the one panel that BUYS an age, interactable while affordable; the price tag (one `ResourceUnit` per cost entry, same prefab as the bar; hidden past the last age); the ladder of ages still AHEAD, the one being bought highlighted at the bottom — a queue of what is coming, not a log (a bought age drops out and the rest slide down: layout only, a bottom-aligned Vertical Layout Group + RectMask2D; one `AgeTimelineCard` prefab for both states). The age comes from event payloads, never read back off `AgeSystem`, so subscriber order cannot matter |
| `BuildModeButton` / `BuildPanelUI` / `BuildCardUI` | MB (UI) | Toggle button (publishes the toggle, reflects `BuildModeButtonStateEvent`); the build palette (a card per `BuildPaletteDef` entry, locked below `requiredAge`, sell button, drives the controller's tool selection; opens and closes its cards on the UI mode, on screen per `UIVisibility`); one card (fixed root = slot, `AnimatedVisual` child moves, LitMotion; every size and lift from the shared `BuildCardMotionProfile` asset). `BuildPanelScroller` is the pixel-snapped drag / wheel strip — a drag swallows the click it started with |
| `ZoneSelectionUI` / `ZoneCardUI` | MB (UI) | The zone screen: a card per offer (biome name and icon, exact counts — "Forest ×14"); owns the preview and the camera focus (sticky, from hover; `focusOffsetY` keeps the zone clear of the cards); a plain click publishes `ZoneSelectedEvent` and locks every card; holds no run. Written apart from the perk card on purpose |
| `ZonePreviewOverlay` | MB | Draws the focused offer on the water: one quad mesh (fill in the biome's `previewColor` + an inset outline), built like `GridOverlay` in world space, so the object stays at the origin. `WorldBounds` / `WorldCentre` for the camera |
| `PerkSelectionUI` / `PerkCardUI` | MB (UI) | The perk screen: owns the cards and nothing else — publishes `PerkSelectedEvent`, holds no run. On screen per `UIVisibility` (`PerkPick`); stays ACTIVE and is hidden by its `CanvasGroup` (an inactive panel's `Awake` would run inside the reveal). A card confirms on HOLD (release, never press, even at `holdDuration 0`), unscaled time; frame = hover, scale = press (LitMotion), fill = hold — three independent visuals |
