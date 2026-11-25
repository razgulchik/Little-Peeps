using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Placed on a structure; drives a per-slot spawn -> travel -> return -> rest cycle.
    // Each slot is an independent place for one little person: it launches a unit, then stands
    // free for ANY tired or drunk unit — houses provide population, not professions, so a house neither
    // keeps a slot for the units it launched nor cares what they work as when they come back (the tool
    // goes back to its rack on entry, see Unit.EnterRest). There is no lockout after a launch: the unit
    // that just left has full stamina and is sober, and the house's door (TryEnter) lets only tired or
    // drunk units in, so nothing can duck straight back in — a free slot simply takes the next such unit
    // that hits the house.
    // The units born here are its RESIDENTS all the same (Unit.BirthHouse), one per slot: this is the house
    // they are sent home to (SpawnSystem.SendHome), and they leave the game with it when it is sold (EvictAll).
    // `capacity` is the BASE slot count; the run's HouseCapacity modifier is applied on top at Warmup,
    // and the result — slots.Count — is what is registered into SpawnSystem's global cap under this
    // house's `unitDef` (population is counted per kind of unit) and what OnDestroy gives back.
    [RequireComponent(typeof(Structure))]
    public class Spawner : MonoBehaviour, IEntrance, IStructureSpawner
    {
        [SerializeField] private SpawnSystem spawnSystem;

        [Header("Units")]
        [SerializeField] public UnitDef unitDef;    // what this house spawns — the population key; what those units DO is the racks' business
        [SerializeField] public int capacity = 1;   // base; never mutated at runtime — see ResolveCapacity

        // Slots this house actually has (base + run modifier), 0 before Warmup.
        public int SlotCount => slots != null ? slots.Count : 0;

        [Header("Cycle timing (seconds)")]
        [SerializeField] private float restDuration = 3f;

        [Header("Launch")]
        [SerializeField] private float launchSpeedMultiplier = 2.5f;
        [SerializeField] private float launchBoostDuration = 1f;
        [SerializeField] private float launchGap = 0.1f; // clearance between the structure collider edge and the unit collider edge at launch
        [SerializeField] private float launchJitterDegrees = 12f; // random spread applied to the chosen cell direction so launches don't all run on exact lines

        [Header("Sound")]
        [Tooltip("A unit goes in to rest. Empty = silent.")]
        [SerializeField] private SoundDef enterSound;
        [Tooltip("A unit leaves. Empty = silent.")]
        [SerializeField] private SoundDef exitSound;

        private enum SlotState { Free, Occupied }

        // One slot = one independent place. Reference type so we mutate it in place inside foreach.
        private class Slot
        {
            public SlotState state;
            public float timer;   // rest left (Occupied only)
            public Unit unit;     // the resting unit (Occupied only)
        }

        private Collider2D structureCollider;
        private List<Slot> slots;   // null until Warmup runs
        private bool registered;

        // Grid context injected on placement. `grid` is the single source of truth for which perimeter
        // directions are open; `instance.Cell` is this spawner's origin. When both are null (a spawner
        // placed directly in the scene, not via StructureSystem) we fall back to the old random-direction
        // launch so those still work.
        private IslandGrid grid;
        private StructureInstance instance;

        private readonly List<Vector2> allowedDirs = new();   // reused per launch — directions toward open perimeter cells

        private void Awake()
        {
            structureCollider = GetComponentInChildren<Collider2D>();
        }

        // Optional runtime injection (StructureSystem calls this when placing a structure at runtime).
        // The grid + instance let the spawner pick launch directions from open neighbouring cells; both
        // are null for scene-placed spawners, which then keep the legacy random-direction behaviour.
        public void Initialize(SpawnSystem system, IslandGrid grid, StructureInstance instance)
        {
            spawnSystem = system;
            this.grid = grid;
            this.instance = instance;
        }

        private void Start()
        {
            if (spawnSystem == null)
            {
                Debug.LogError($"Spawner on '{name}' has no SpawnSystem assigned.", this);
                return;
            }
            if (unitDef == null)
            {
                Debug.LogError($"Spawner on '{name}' has no UnitDef assigned.", this);
                return;
            }

            Warmup();
        }

        // IStructureSpawner — called once, on placement. Every slot holds its unit resting INSIDE for
        // restDuration before its first launch — so placing a house never produces an instant launch
        // (anti-abuse). Safe to call again: it won't re-register capacity or recreate slots, just restarts
        // the rest delay.
        public void Warmup()
        {
            if (spawnSystem == null || unitDef == null) return;

            // Registration and slot creation are ONE step, so slots.Count is by construction the number
            // the global cap was raised by — the number Unregister must give back, and the number
            // IncreaseCapacity measures growth from.
            if (!registered)
            {
                int resolved = ResolveCapacity();
                spawnSystem.RegisterCapacity(unitDef, resolved);
                spawnSystem.RegisterSpawner(this);
                registered = true;

                slots = new List<Slot>(resolved);
                for (int i = 0; i < resolved; i++) slots.Add(new Slot());
            }

            foreach (var slot in slots)
                FillSlot(slot);
        }

        // IStructureSpawner — the sheet changed mid-run (age, perk): re-resolve and grow to match.
        // Before Warmup there is nothing to refresh — Warmup resolves from the sheet itself. Only
        // growth is applied: DecreaseCapacity is still a stub, so a NEGATIVE HouseCapacity modifier
        // bought mid-run leaves the standing houses as they are and only shows on houses warmed up
        // after it. A future per-building upgrade raises `capacity` and calls this — same path.
        public void RefreshFromStats()
        {
            if (!registered) return;
            IncreaseCapacity(ResolveCapacity());
        }

        // Slots this house gets: the base with the run modifier applied. The one stat NOT read at the
        // point of use — the result is materialised into slots and the global cap, so a later sheet
        // change reaches this house only through RefreshFromStats. Never below one slot: a house with
        // nobody in it is not a house, whatever a penalty says. Unscoped: the house has no profession
        // to key on (see StatId).
        private int ResolveCapacity()
        {
            var stats = spawnSystem != null ? spawnSystem.Stats : null;
            int resolved = stats != null
                ? stats.ApplyCount(capacity, StatId.HouseCapacity)
                : capacity;
            return Mathf.Max(1, resolved);
        }

        // Grow to newCapacity slots at runtime. Each NEW slot spawns a unit that rests first, then
        // launches — exactly like a freshly built slot, so a perk never fires a unit the instant it is
        // bought. Existing units keep running. Growth only; downgrades go through DecreaseCapacity.
        private void IncreaseCapacity(int newCapacity)
        {
            if (slots == null || newCapacity <= slots.Count) return;

            int delta = newCapacity - slots.Count;
            spawnSystem.RegisterCapacity(unitDef, delta);   // raise the global cap first, or TrySpawn refuses

            for (int i = 0; i < delta; i++)
            {
                var slot = new Slot();
                slots.Add(slot);
                FillSlot(slot);
            }
        }

        // Downgrade hook: shrink the structure to newCapacity slots. FILLER for now — not implemented.
        // TODO: when slot downgrades exist, pick which slots to remove, deal with their units
        // (resting ones via SpawnSystem.Despawn; roaming ones need a recall path), then
        // spawnSystem.UnregisterCapacity(unitDef, delta), trim `slots`.
        public void DecreaseCapacity(int newCapacity)
        {
            // TODO: implement when slot downgrades are introduced.
        }

        // How long a unit rests inside before launching, with the run modifier applied. The stats sheet
        // is asked for at the point of use, never cached: it belongs to the run, while a spawner placed
        // in one run outlives it. A perk that shortens the rest is a NEGATIVE percent — this is the
        // delay in seconds, not a rate. Unscoped, like HouseCapacity.
        private float ResolveRestDuration()
        {
            var stats = spawnSystem != null ? spawnSystem.Stats : null;
            return stats != null
                ? stats.Apply(restDuration, StatId.SpawnerRecharge)
                : restDuration;
        }

        // Initialize / refill one slot: keep a resting unit (restart its rest), otherwise spawn one
        // into rest, or leave it Free if the global cap is full. Shared by Warmup and upgrades.
        private void FillSlot(Slot slot)
        {
            if (slot.state == SlotState.Occupied && slot.unit != null)
            {
                // Already holds a unit (Warmup called again) — just restart its rest delay.
                slot.timer = ResolveRestDuration();
                return;
            }

            var unit = spawnSystem.TrySpawn(unitDef, transform.position);
            if (unit != null)
            {
                unit.SetBirthHouse(this);
                OccupySlot(slot, unit);   // rest inside first, launch only after restDuration
            }
            else
                slot.state = SlotState.Free;
        }

        private void Update()
        {
            if (slots == null) return;

            float dt = Time.deltaTime;
            foreach (var slot in slots)
            {
                if (slot.state != SlotState.Occupied) continue;
                slot.timer -= dt;
                if (slot.timer <= 0f) LaunchFromSlot(slot, slot.unit);
            }
        }

        // IEntrance — whom the house is for: a tired unit, a drunk one or both, a slot free or not (TryEnter
        // adds the slot). So a tired unit at a full house reads Blocked, a working sober one Useless.
        public bool Serves(Unit unit) => unit != null && (unit.IsTired || unit.IsDrunk);

        // IEntrance — CollisionTarget.HandleHit asks this for EVERY unit that hits THIS structure, working
        // or tired. The house's door rule (design doc): a unit that is tired, drunk or both comes in; one
        // that is neither bounces off, which is what keeps a unit that was just launched from ducking
        // straight back in. A drunk one comes in even with stamina left and loses its outing to the house
        // reset (Unit.EnterRest) — the tavern → house cycle is legitimate economy, not an exploit. Dispatch
        // is local, so no target filter is needed (the target is already our structure). No unit-type
        // check either: any house takes any such worker, whichever house launched it and whatever it
        // worked as — past the door rule, the only thing that can refuse is a full house, and then the
        // unit bounces on toward another structure.
        public bool TryEnter(Unit unit)
        {
            bool entered = Serves(unit) && TryShelter(unit);
            if (entered) AimProbe.Entered(this, unit);
            return entered;
        }

        // Take `unit` in to rest in the first free slot; false when every slot is occupied. The one
        // way in, whether the unit came through the door (TryEnter) or was sent home (SpawnSystem.SendHome)
        // — the house takes either the same way, and sends it back out launched after its rest. No door
        // rule here: TryEnter applies it before calling this, and SendHome's callers pick their own units.
        public bool TryShelter(Unit unit)
        {
            if (slots == null || unit == null) return false;

            foreach (var slot in slots)
            {
                if (slot.state != SlotState.Free) continue;
                OccupySlot(slot, unit);
                unit.MakeSound(enterSound);
                return true;
            }
            return false;
        }

        // Whether TryShelter would succeed right now — for a caller choosing between houses.
        public bool HasFreeSlot
        {
            get
            {
                if (slots == null) return false;
                foreach (var slot in slots)
                    if (slot.state == SlotState.Free) return true;
                return false;
            }
        }

        // Take a unit into a slot to rest inside the structure.
        private void OccupySlot(Slot slot, Unit unit)
        {
            slot.state = SlotState.Occupied;
            slot.timer = ResolveRestDuration();
            slot.unit = unit;
            unit.EnterRest(this);
        }

        private static void FreeSlot(Slot slot)
        {
            slot.unit = null;
            slot.state = SlotState.Free;
            slot.timer = 0f;
        }

        // The unit left the game from inside (Unit.LeaveBuilding, from SpawnSystem.Despawn): free its slot
        // without launching it — it is on its way back to the pool.
        public void Forget(Unit unit)
        {
            if (slots == null) return;

            foreach (var slot in slots)
                if (slot.unit == unit) FreeSlot(slot);
        }

        private void LaunchFromSlot(Slot slot, Unit unit)
        {
            if (!TryPickLaunch(unit, out Vector2 point, out Vector2 dir))
            {
                // Surrounded on every side (edges / neighbours / fences): keep the unit resting inside and
                // try again after another rest.
                slot.timer = ResolveRestDuration();
                return;
            }

            unit.transform.position = point;
            unit.Launch(dir, launchSpeedMultiplier, launchBoostDuration);
            unit.MakeSound(exitSound);

            FreeSlot(slot);
        }

        // Choose where the unit is put down and where it heads: an OPEN perimeter cell (an exit, see
        // StructureExits), one at random, with a little jitter on the heading. The gap is the unit's radius
        // plus launchGap, from the building's FOOTPRINT. False = the house is sealed in on every side.
        // Without grid context (scene-placed spawner) a random direction — that path is never "blocked".
        private bool TryPickLaunch(Unit unit, out Vector2 point, out Vector2 dir)
        {
            if (grid == null || instance == null || instance.Def == null)
            {
                dir = RandomDirection();
                point = ColliderExit(dir, unit);
                return true;
            }

            return StructureExits.TryPickExit(grid, instance.Cell, instance.Def.Footprint, instance.Def.border,
                                              allowedDirs, unit.Radius + launchGap, launchJitterDegrees,
                                              out point, out dir);
        }

        private static Vector2 RandomDirection()
        {
            Vector2 d = Random.insideUnitCircle.normalized;
            return d.sqrMagnitude < 1e-4f ? Vector2.up : d;
        }

        // A scene-placed spawner has no footprint, so it measures against its collider box: where the launch
        // ray leaves the box, plus unit radius and launchGap.
        private Vector2 ColliderExit(Vector2 dir, Unit unit)
        {
            if (structureCollider == null)
                return (Vector2)transform.position + dir * launchGap; // fallback: measure from center

            return BoxExit(structureCollider.bounds.center, structureCollider.bounds.extents, dir)
                 + dir * (unit.Radius + launchGap);
        }

        // The point where a ray from the box centre along dir crosses the box edge.
        private static Vector2 BoxExit(Vector2 center, Vector2 extents, Vector2 dir)
        {
            float ax = Mathf.Abs(dir.x);
            float ay = Mathf.Abs(dir.y);
            float tx = ax > 1e-4f ? extents.x / ax : float.PositiveInfinity;
            float ty = ay > 1e-4f ? extents.y / ay : float.PositiveInfinity;
            return center + dir * Mathf.Min(tx, ty);
        }

        // The house is being sold in a running game (StructureSystem's live removal, before the Teardown).
        // Its residents leave the game with it, wherever they are; a stranger resting here is sent home and
        // loses only the rest it has had. The order is the point:
        //   - out of the registry first, so nobody is sent back in here;
        //   - residents before strangers, because a resident resting in another house frees a slot there.
        // So every stranger finds room: the houses left keep their own residents only, one per slot.
        public void EvictAll()
        {
            if (!registered) return;

            spawnSystem.UnregisterSpawner(this);
            spawnSystem.RemoveResidents(this);

            foreach (var slot in slots)
                if (slot.state == SlotState.Occupied && spawnSystem.SendHome(slot.unit)) FreeSlot(slot);
        }

        // IStructureSpawner — hand everything back synchronously, before the object is destroyed.
        // See the interface for why the deferred OnDestroy is too late (run teardown, a sale).
        public void Teardown() => Unregister();

        private void OnDestroy() => Unregister();

        // Idempotent by design: `registered` is cleared FIRST, so the OnDestroy that follows a Teardown
        // (and any re-entrant path) does nothing. Double-running this would take this house's slots off
        // the global cap twice.
        private void Unregister()
        {
            if (!registered) return;
            registered = false;

            // Despawn units currently resting here so they don't leak as hidden objects. In a run teardown
            // only: whoever is out on the island is collected right after (SpawnSystem.ResetForNewRun). A
            // sale finds nobody here — EvictAll has already seen everyone off.
            if (slots != null && spawnSystem != null)
            {
                foreach (var slot in slots)
                {
                    if (slot.state == SlotState.Occupied && slot.unit != null)
                        spawnSystem.Despawn(slot.unit);
                }
            }

            // slots.Count, not `capacity`: the base is not what was registered once a modifier or a
            // mid-run growth has been applied, and slots are created in the same step as the
            // registration, so their count IS the registered amount.
            if (spawnSystem != null && unitDef != null && slots != null)
                spawnSystem.UnregisterCapacity(unitDef, slots.Count);

            if (spawnSystem != null)
                spawnSystem.UnregisterSpawner(this);
        }
    }
}
