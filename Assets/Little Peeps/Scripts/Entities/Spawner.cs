using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Placed on a structure; drives a per-slot spawn -> travel -> return -> rest cycle.
    // Each slot is an independent place for one little person: it launches a unit, then stands
    // free for ANY tired unit — houses provide population, not professions, so a house neither owns
    // the units it launched nor cares what they work as when they come back (the tool goes back to
    // its rack on entry, see Unit.EnterRest). There is no lockout after a launch: the unit that just
    // left has full stamina, and CollisionTarget never routes a working unit to a shelter, so nothing
    // can duck straight back in — a free slot simply takes the next tired unit that hits the house.
    // `capacity` is the BASE slot count; the run's HouseCapacity modifier is applied on top at Warmup,
    // and the result — slots.Count — is what is registered into SpawnSystem's global cap under this
    // house's `unitDef` (population is counted per kind of unit) and what OnDestroy gives back.
    [RequireComponent(typeof(Structure))]
    public class Spawner : MonoBehaviour, IShelter, IStructureSpawner
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
        // directions are open; `instance.Cell` is this spawner's origin and auto-updates when it's moved
        // (StructureSystem.DropStructure writes the new cell into the same instance we hold here). When
        // both are null (a spawner placed directly in the scene, not via StructureSystem) we fall back to
        // the old random-direction launch so those still work.
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

        // IStructureSpawner — called on placement and build-mode exit (and later by StructureSystem
        // on build/move). Every slot holds its unit resting INSIDE for restDuration before its first
        // launch — so placing or moving a structure never produces an instant launch (anti-abuse).
        // Safe to call again on move: it won't re-register capacity or recreate slots, just restarts
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

            // Placed during build mode: register + reserve slots, but don't pull resting units from the
            // pool yet. Build-mode exit re-runs Warmup (flag cleared) and fills the slots then, so units
            // appear on exit together with the animals — nothing spawns while the player is building.
            if (spawnSystem.IsBuildMode) return;

            foreach (var slot in slots)
                FillSlot(slot);
        }

        // IStructureSpawner — build-mode enter (called via SpawnSystem): the units this spawner
        // referenced were just returned to the pool by DespawnAll, so our slot references are now
        // stale. Reset slots to Free (keep capacity registered) so the next Warmup re-spawns fresh
        // resting units.
        public void ResetForBuildMode()
        {
            if (slots == null) return;
            foreach (var slot in slots)
            {
                slot.state = SlotState.Free;
                slot.unit = null;
                slot.timer = 0f;
            }
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
                // In build mode the slot is reserved but left empty, exactly like a house placed there:
                // the exit warmup fills every slot at once, nothing materialises mid-build.
                if (!spawnSystem.IsBuildMode) FillSlot(slot);
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
                // Already holds a unit (e.g. moved mid-rest) — just restart its rest delay.
                slot.timer = ResolveRestDuration();
                return;
            }

            var unit = spawnSystem.TrySpawn(unitDef, transform.position);
            if (unit != null)
                OccupySlot(slot, unit);   // rest inside first, launch only after restDuration
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

        // IShelter — CollisionTarget.HandleHit calls this when a TIRED unit hits THIS structure; a
        // working unit's hit is routed past every shelter, so no stamina check is needed here. Dispatch
        // is local, so no target filter is needed either (the target is already our structure). No
        // unit-type check either: any house takes any tired worker, whichever house launched it and
        // whatever it worked as — the only thing that can refuse is a full house.
        public void OnTiredHit(Unit unit)
        {
            if (slots == null || unit == null) return;

            // Put the unit in the first free slot.
            foreach (var slot in slots)
            {
                if (slot.state != SlotState.Free) continue;
                OccupySlot(slot, unit);
                return;
            }
            // No free slot — the unit bounces on toward another structure.
        }

        // Take a unit into a slot to rest inside the structure.
        private void OccupySlot(Slot slot, Unit unit)
        {
            slot.state = SlotState.Occupied;
            slot.timer = ResolveRestDuration();
            slot.unit = unit;
            unit.EnterRest();
        }

        private void LaunchFromSlot(Slot slot, Unit unit)
        {
            if (!TryPickSpawnDirection(out Vector2 dir))
            {
                // Surrounded on every side (edges / neighbours / fences): keep the unit resting inside and
                // try again after another rest.
                slot.timer = ResolveRestDuration();
                return;
            }

            unit.transform.position = SpawnPosition(dir, unit);
            unit.Launch(dir, launchSpeedMultiplier, launchBoostDuration);

            slot.unit = null;
            slot.state = SlotState.Free;
            slot.timer = 0f;
        }

        // Choose a launch direction toward an OPEN perimeter cell. Without grid context (scene-placed
        // spawner) fall back to a random direction — that path is never "blocked". Otherwise gather the
        // open perimeter directions and pick one at random (+ a little jitter); false = none are open.
        private bool TryPickSpawnDirection(out Vector2 dir)
        {
            if (grid == null || instance == null || instance.Def == null)
            {
                dir = RandomDirection();
                return true;
            }

            CollectAllowedDirections(grid, instance.Cell, instance.Def.size, instance.Def.border, allowedDirs);
            if (allowedDirs.Count == 0) { dir = Vector2.zero; return false; }

            dir = allowedDirs[Random.Range(0, allowedDirs.Count)];
            if (launchJitterDegrees > 0f)
                dir = Rotate(dir, Random.Range(-launchJitterDegrees, launchJitterDegrees));
            return true;
        }

        // Fill `buffer` with one direction per OPEN perimeter cell. We walk the cells one step outside this
        // structure's claimed territory (footprint, plus its border if it has one) on each cardinal side; a
        // side is open when that outer cell is land, not occupied by ANOTHER structure (or its border), and
        // the boundary edge to it carries no fence.
        //   - border 0 (the common case): the outer cells ARE the footprint's immediate neighbours, so a
        //     corner house simply has its off-island sides excluded and the unit lands on that open cell.
        //   - border >= 1: requiring the cell BEYOND the border ring to be open gives the "one clear cell
        //     from the map edge / a neighbour" rule for free; the unit still lands in the border ring.
        //
        // Static and parameterised (rather than reading the injected `grid`/`instance` fields) so this
        // geometry can be exercised on a bare IslandGrid with no scene, GameObject or spawner behind it.
        public static void CollectAllowedDirections(IslandGrid grid, Vector2Int origin, Vector2Int size, int border, List<Vector2> buffer)
        {
            buffer.Clear();

            Vector2Int o = origin;
            Vector2Int s = size;
            int b = border;

            // Inclusive bounds of the claimed territory box (footprint + border on every side).
            int minX = o.x - b, minY = o.y - b;
            int maxX = o.x + s.x + b - 1, maxY = o.y + s.y + b - 1;

            Vector2 center = grid.OriginToWorldCenter(o, s);

            for (int x = minX; x <= maxX; x++)
            {
                TryAddDirection(grid, buffer, center, new Vector2Int(x, minY - 1), new Edge(new Vector2Int(x, minY), true));      // south
                TryAddDirection(grid, buffer, center, new Vector2Int(x, maxY + 1), new Edge(new Vector2Int(x, maxY + 1), true));  // north
            }
            for (int y = minY; y <= maxY; y++)
            {
                TryAddDirection(grid, buffer, center, new Vector2Int(minX - 1, y), new Edge(new Vector2Int(minX, y), false));     // west
                TryAddDirection(grid, buffer, center, new Vector2Int(maxX + 1, y), new Edge(new Vector2Int(maxX + 1, y), false)); // east
            }
        }

        // Add the direction toward `outerCell` if that cell is open: on-island, unoccupied, and not fenced
        // off across `boundary` (the grid edge between the claimed territory and the outer cell).
        private static void TryAddDirection(IslandGrid grid, List<Vector2> buffer, Vector2 center, Vector2Int outerCell, Edge boundary)
        {
            var cell = grid.GetCell(outerCell);
            if (cell == null) return;                       // off-island (map edge, with the border as the clear cell)
            if (cell.occupant != null) return;              // another structure or its border — don't crowd it
            if (grid.GetEdge(boundary) != null) return;     // a fence blocks egress this way

            Vector2 dir = grid.GridToWorld(outerCell) - center;
            if (dir.sqrMagnitude < 1e-6f) return;
            buffer.Add(dir.normalized);
        }

        private static Vector2 RandomDirection()
        {
            Vector2 d = Random.insideUnitCircle.normalized;
            return d.sqrMagnitude < 1e-4f ? Vector2.up : d;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        // Place the unit just outside the structure collider along dir:
        // structure edge (along dir) + unit radius + launchGap.
        private Vector2 SpawnPosition(Vector2 dir, Unit unit)
        {
            if (structureCollider == null)
                return (Vector2)transform.position + dir * launchGap; // fallback: measure from center

            Vector2 center = structureCollider.bounds.center;
            Vector2 extents = structureCollider.bounds.extents;

            // Distance from the box center to its edge along dir.
            float ax = Mathf.Abs(dir.x);
            float ay = Mathf.Abs(dir.y);
            float tx = ax > 1e-4f ? extents.x / ax : float.PositiveInfinity;
            float ty = ay > 1e-4f ? extents.y / ay : float.PositiveInfinity;
            float structureEdge = Mathf.Min(tx, ty);

            return center + dir * (structureEdge + unit.Radius + launchGap);
        }

        // IStructureSpawner — hand everything back synchronously, before the object is destroyed.
        // See the interface for why the deferred OnDestroy is too late during a run teardown.
        public void Teardown() => Unregister();

        private void OnDestroy() => Unregister();

        // Idempotent by design: `registered` is cleared FIRST, so the OnDestroy that follows a Teardown
        // (and any re-entrant path) does nothing. Double-running this would both halve the global cap
        // twice and push an already-pooled unit into the pool a second time — UnitPool.Release has no
        // guard of its own, so the same Unit would then be handed out to two callers.
        private void Unregister()
        {
            if (!registered) return;
            registered = false;

            // Despawn units currently resting here so they don't leak as hidden objects.
            // TODO: full global active-count reconciliation (also units this structure launched
            // that are still roaming) belongs in StructureSystem.RemoveStructure.
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
