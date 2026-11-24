using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Coordinates unit spawning: bridges Spawner components with UnitPool, enforces a global
    // active-unit cap per KIND of unit (cap = sum of all registered spawner capacities), and keeps
    // UnitSystem's live registry in sync via direct calls (no events). Also owns the registry of
    // ALL structure spawners (unit Spawners and AnimalSpawners, via IStructureSpawner), for the sheet
    // refresh and the houses a unit can be sent to.
    //
    // Nothing ever empties the field mid-run any more: building happens in the running game. A unit
    // vanishes off the island in one way, whatever the reason: in a PUFF, home to its own house (SendHome).
    // It leaves the GAME (Despawn) only at the run's end or with its own house when that is sold
    // (RemoveResidents), in the same puff.
    //
    // Population is counted per UnitDef — the kind of unit a house spawns — never per profession:
    // a rack changes what a unit does mid-outing, not what it is. Spawn and despawn must count
    // against the same key, and the def is the one thing about a unit that never changes.
    public class SpawnSystem : MonoBehaviour
    {
        [SerializeField] private UnitPool unitPool;
        [SerializeField] private UnitSystem unitSystem;
        [SerializeField] private IslandSystem islandSystem;   // injected into spawned units; kept for future island-aware behavior

        [Tooltip("The one place the scene wires the sound system: units, houses and resources all reach it " +
                 "through here. Empty = the island is silent.")]
        [SerializeField] private SoundSystem soundSystem;

        [Header("Stuck units")]
        [Tooltip("How often the live units are swept for tired ones that report themselves stuck " +
                 "(Unit.IsStuck), which are sent home: to the house they were born in, or the nearest with a " +
                 "free slot if that one is full. A unit that finds none stays where it is and is tried again " +
                 "on the next sweep.")]
        [SerializeField, Min(0.05f)] private float stuckSweepInterval = 0.5f;
        private float stuckSweepTimer;

        [Header("Puff")]
        [Tooltip("Played where a unit or an animal is taken off the island: a unit sent home (a tap, out of " +
                 "stamina, stuck), the residents of a sold house, the animals of a sold den. A ParticleSystem " +
                 "prefab built as a shared emitter (World space, no emission of its own). Empty = they just " +
                 "vanish.")]
        [SerializeField] private ParticleSystem puffFx;

        [Tooltip("The same puff, drawn smaller, for a coop's bird. Built like Puff Fx. Empty = the bird just " +
                 "vanishes.")]
        [SerializeField] private ParticleSystem smallPuffFx;

        [Tooltip("Particles per puff, of either size.")]
        [SerializeField, Min(1)] private int puffParticles = 8;

        [Tooltip("Played with every puff. Empty = silent.")]
        [SerializeField] private SoundDef puffSound;

        private ParticleSystem puffEmitter;
        private ParticleSystem smallPuffEmitter;

        private readonly Dictionary<UnitDef, int> capByDef = new();
        private readonly Dictionary<UnitDef, int> activeByDef = new();

        private readonly List<IStructureSpawner> spawners = new();   // live spawners (units + animals)
        private readonly List<Unit> despawnBuffer = new();  // reused snapshot for DespawnAll and RemoveResidents

        private RunStats stats;   // injected into spawned units so their speed reflects run modifiers

        // Exposed for spawners, which have no other route to the run's stats. Read at the point of use
        // and NEVER cached: Initialize rebinds this on every new run, and a cached copy would leave a
        // spawner applying the finished run's bonuses.
        public RunStats Stats => stats;

        // Read by the units this system spawns: their bounces, and every sound their actions make
        // (Unit.MakeSound) — a house's door, a tree's chop.
        public SoundSystem Sounds => soundSystem;

        // The game's rule switches, read by the units this system spawns. Handed in by GameBootstrap before
        // the first run and kept for every run of the session. Null in tests and in a scene without
        // bootstrap: every switch then reads as off.
        public GameSetup Setup { get; private set; }

        public void UseSetup(GameSetup setup) => Setup = setup;

        // Every unit in play — on the field, resting in a house or sitting in a tavern (Unit.IsOnField tells
        // them apart). For a construction site looking for the units standing on its spot.
        public IReadOnlyList<Unit> ActiveUnits => unitSystem != null ? unitSystem.ActiveUnits : System.Array.Empty<Unit>();

        // Injected by RunManager.StartNewRun so units spawned this run carry the run's stat sheet.
        //
        // Also the ONE subscription to that sheet. A house's capacity is materialised into slots at
        // warmup, so a perk or age that raises it mid-run has to be pushed to the houses already
        // standing — and this is the registry that knows them. The previous run's sheet dies with its
        // run, so the unsubscribe is bookkeeping rather than a leak fix; it is here so a re-Initialize
        // can never leave two sheets driving one registry.
        public void Initialize(RunContext run)
        {
            if (stats != null) stats.Changed -= RefreshSpawnersFromStats;
            stats = run.stats;
            if (stats != null) stats.Changed += RefreshSpawnersFromStats;
        }

        // Sheet changed mid-run: every live spawner re-resolves what it materialised from it.
        private void RefreshSpawnersFromStats()
        {
            for (int i = 0; i < spawners.Count; i++)
                spawners[i].RefreshFromStats();
        }

        private void Start()
        {
            if (islandSystem == null)
                Debug.LogWarning("SpawnSystem has no IslandSystem — spawned units won't receive an island reference. Wire the Island System field.", this);
            if (soundSystem == null)
                Debug.LogWarning("SpawnSystem has no SoundSystem — bounces, harvests and doors will be silent. Wire the Sound System field.", this);

            if (puffFx != null)
                puffEmitter = SharedEmitter.Create(puffFx, transform, "PuffFx",
                                                   $"SpawnSystem: '{puffFx.name}' (puff of a unit taken off the island)",
                                                   "SpawnSystem.puffParticles");

            if (smallPuffFx != null)
                smallPuffEmitter = SharedEmitter.Create(smallPuffFx, transform, "SmallPuffFx",
                                                        $"SpawnSystem: '{smallPuffFx.name}' (puff of a coop's bird)",
                                                        "SpawnSystem.puffParticles");
        }

        private void Update()
        {
            if (unitSystem == null) return;

            stuckSweepTimer -= Time.deltaTime;
            if (stuckSweepTimer > 0f) return;
            stuckSweepTimer = stuckSweepInterval;
            RescueStuckUnits();
        }

        // A stuck unit — wedged, sealed into a pocket or a pen; the unit itself decides, see StuckWatch —
        // that is TIRED cannot get home, so it is sent there (SendHome), rests and comes back out launched
        // with full stamina. Only a tired one: a working unit loses nothing it needs by being shut in — a
        // lumberjack chops its way out, anyone else bounces out the rest of its outing — and is sent home
        // once it tires there. No house free (every slot taken): the unit stays put and the next sweep
        // tries again. Going home keeps the unit active, so the list is safe to walk in place.
        private void RescueStuckUnits()
        {
            var units = unitSystem.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.IsStuck && unit.IsTired) SendHome(unit);
            }
        }

        // THE way a unit vanishes off the island, whatever the reason: a tap on a tired one, stamina run out
        // with GameSetup's despawnWhenTired on, stuck, the house it was resting in sold. It is in its own
        // house at once and rests there like a unit that walked in, with a puff where it was, so it reads as
        // sent off and not as lost. A house keeps no slot for its residents, so a unit that finds its own full
        // goes to the nearest with room. False when every slot is taken: the unit stays where it is, and
        // nothing is shown.
        public bool SendHome(Unit unit)
        {
            var house = HomeFor(unit);
            Vector2 from = unit.Whereabouts;
            if (house == null || !house.TryShelter(unit)) return false;

            Puff(from);
            return true;
        }

        private Spawner HomeFor(Unit unit)
        {
            var own = unit.BirthHouse;
            return own != null && own.HasFreeSlot ? own : NearestHouseWithFreeSlot(unit.Whereabouts);
        }

        // Only unit Spawners shelter; the registry also holds AnimalSpawners, which are skipped.
        private Spawner NearestHouseWithFreeSlot(Vector2 from)
        {
            Spawner best = null;
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < spawners.Count; i++)
            {
                if (spawners[i] is not Spawner house || !house.HasFreeSlot) continue;
                float sq = ((Vector2)house.transform.position - from).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = house;
                }
            }
            return best;
        }

        // Houses with a free slot within `radius` of `from` (measured to the house's root), for a caller
        // that sends a unit home on purpose — a unit on autopilot looking for one. Same filter as the stuck
        // rescue: unit Spawners only, AnimalSpawners skipped.
        public void CollectHousesWithFreeSlot(Vector2 from, float radius, List<Spawner> buffer)
        {
            buffer.Clear();
            float radiusSq = radius * radius;
            for (int i = 0; i < spawners.Count; i++)
            {
                if (spawners[i] is not Spawner house || !house.HasFreeSlot) continue;
                if (((Vector2)house.transform.position - from).sqrMagnitude <= radiusSq) buffer.Add(house);
            }
        }

        // A spawner registers/unregisters itself when it warms up / is destroyed.
        public void RegisterSpawner(IStructureSpawner spawner)
        {
            if (spawner != null && !spawners.Contains(spawner)) spawners.Add(spawner);
        }

        public void UnregisterSpawner(IStructureSpawner spawner)
        {
            spawners.Remove(spawner);
        }

        // A spawner registers/unregisters its capacity when placed/removed.
        public void RegisterCapacity(UnitDef def, int amount)
        {
            if (def == null) return;
            capByDef.TryGetValue(def, out var cur);
            capByDef[def] = cur + amount;
        }

        public void UnregisterCapacity(UnitDef def, int amount)
        {
            if (def == null) return;
            capByDef.TryGetValue(def, out var cur);
            capByDef[def] = Mathf.Max(0, cur - amount);
        }

        public bool CanSpawn(UnitDef def)
        {
            if (def == null) return false;
            capByDef.TryGetValue(def, out var cap);
            activeByDef.TryGetValue(def, out var active);
            return active < cap;
        }

        // Create a unit at position if the global cap for its kind allows it. The caller launches it.
        // No pool, nobody: the house keeps its slot free, as it does when the cap is full (and the
        // capacity tests run SpawnSystem exactly like that, with no unit prefab to pool).
        public Unit TrySpawn(UnitDef def, Vector2 position)
        {
            if (!CanSpawn(def) || unitPool == null) return null;

            var unit = unitPool.Get(def);
            if (unit == null) return null;

            unit.SetIsland(islandSystem);
            unit.SetStats(stats);
            unit.SetSpawns(this);
            unit.transform.position = position;
            activeByDef.TryGetValue(def, out var active);
            activeByDef[def] = active + 1;

            if (unitSystem != null) unitSystem.Add(unit);
            return unit;
        }

        // Out of the GAME, back to the pool: its house sold, run teardown. A unit that only vanishes off the
        // island goes home instead (SendHome). Every exit goes through here, so this is where a mid-outing
        // profession ends: Unequip before the pool takes the unit — the tool goes back on its rack — and
        // the count comes off the def TrySpawn put it on. A unit inside a tavern or a house gives its
        // place back the same way (LeaveBuilding).
        public void Despawn(Unit unit)
        {
            if (unit == null) return;

            unit.LeaveBuilding();
            unit.Unequip();

            if (unit.def != null)
            {
                activeByDef.TryGetValue(unit.def, out var active);
                activeByDef[unit.def] = Mathf.Max(0, active - 1);
            }

            if (unitSystem != null) unitSystem.Remove(unit);
            unitPool.Release(unit);
        }

        // A house is being sold (Spawner.EvictAll): the units born in it leave the game with it, each in a
        // puff, wherever they are — on the field, in a tavern, resting in this house or in another. There is
        // one resident per slot, so the island never holds more units than the houses left have room for.
        public void RemoveResidents(Spawner house)
        {
            if (unitSystem == null) return;

            // Snapshot first: Despawn mutates UnitSystem's list via Remove while we iterate.
            despawnBuffer.Clear();
            var units = unitSystem.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
                if (units[i].BirthHouse == house) despawnBuffer.Add(units[i]);

            for (int i = 0; i < despawnBuffer.Count; i++)
            {
                Puff(despawnBuffer[i].Whereabouts);
                Despawn(despawnBuffer[i]);
            }
            despawnBuffer.Clear();
        }

        // The puff effect and sound at `position`. Public for the sold den's animals, which are not units
        // and leave through their spawner's own teardown.
        public void Puff(Vector2 position)
        {
            SharedEmitter.EmitAt(puffEmitter, position, puffParticles);
            if (soundSystem != null) soundSystem.Play(puffSound);
        }

        // The same for a coop's bird: its own smaller art, because scaling the big sheet down would break
        // the pixel grid. Same sound.
        public void SmallPuff(Vector2 position)
        {
            SharedEmitter.EmitAt(smallPuffEmitter, position, puffParticles);
            if (soundSystem != null) soundSystem.Play(puffSound);
        }

        // Run teardown: every unit back to the pool and every scrap of per-run bookkeeping dropped, so
        // the next run starts from a clean sheet instead of inheriting caps and spawner entries.
        //
        // Call this AFTER StructureSystem.ClearAll, which tears the spawners down synchronously — by the
        // time we get here the registries should already be empty and the Clear calls are the backstop
        // that guarantees it.
        public void ResetForNewRun()
        {
            DespawnAll();
            capByDef.Clear();
            activeByDef.Clear();
            spawners.Clear();

            // Puffs still in flight belong to the island being thrown away. Clear, not Stop: the emitter
            // stays playing, ready for the new run.
            if (puffEmitter != null) puffEmitter.Clear();
            if (smallPuffEmitter != null) smallPuffEmitter.Clear();
        }

        private void DespawnAll()
        {
            if (unitSystem == null) return;

            // Snapshot first: Despawn mutates UnitSystem's list via Remove while we iterate.
            despawnBuffer.Clear();
            despawnBuffer.AddRange(unitSystem.ActiveUnits);
            for (int i = 0; i < despawnBuffer.Count; i++)
                Despawn(despawnBuffer[i]);
            despawnBuffer.Clear();
        }
    }
}
