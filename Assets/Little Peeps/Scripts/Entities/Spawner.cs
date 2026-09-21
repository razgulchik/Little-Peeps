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
        // whatever it worked as — the only thing that can refuse is a full house, and then the unit
        // bounces on toward another structure.
        public void OnTiredHit(Unit unit) => TryShelter(unit);

        // Take `unit` in to rest in the first free slot; false when every slot is occupied. The one
        // door for a unit coming in from the field, whether it hit the house tired (OnTiredHit) or
        // SpawnSystem brought it here because it was stuck — the house takes either the same way, and
        // sends it back out launched after its rest.
        public bool TryShelter(Unit unit)
        {
            if (slots == null || unit == null) return false;

            foreach (var slot in slots)
            {
                if (slot.state != SlotState.Free) continue;
                OccupySlot(slot, unit);
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

            CollectAllowedDirections(grid, instance.Cell, instance.Def.Footprint, instance.Def.border, allowedDirs);
            if (allowedDirs.Count == 0) { dir = Vector2.zero; return false; }

            dir = allowedDirs[Random.Range(0, allowedDirs.Count)];
            if (launchJitterDegrees > 0f)
                dir = Rotate(dir, Random.Range(-launchJitterDegrees, launchJitterDegrees));
            return true;
        }

        // Fill `buffer` with one direction per OPEN perimeter cell. We walk the cells one step outside this
        // structure's claimed territory (footprint, plus its border if it has one); a cell is open when it
        // is land, not occupied by ANOTHER solid structure (or its border), and it shares at least one
        // unfenced edge with the territory. A passable occupant (field, bush, rack — see
        // StructureDef.passable) leaves the side open: the unit lands in its trigger and walks on through.
        //   - border 0 (the common case): the outer cells ARE the footprint's immediate neighbours, so a
        //     corner house simply has its off-island sides excluded and the unit lands on that open cell.
        //   - border >= 1: requiring the cell BEYOND the border ring to be open gives the "one clear cell
        //     from the map edge / a neighbour" rule for free; the unit still lands in the border ring.
        //   - a shaped footprint: the ring hugs the shape, so a notch's cells are exits too — the unit
        //     starts at the notch's wall and flies out through it.
        // Cells touching the territory only at a corner are not exits; a cell that touches it along
        // several edges is one exit, not several.
        //
        // Static and parameterised (rather than reading the injected `grid`/`instance` fields) so this
        // geometry can be exercised on a bare IslandGrid with no scene, GameObject or spawner behind it.
        public static void CollectAllowedDirections(IslandGrid grid, Vector2Int origin, Footprint footprint, int border, List<Vector2> buffer)
        {
            buffer.Clear();

            Vector2 center = grid.OriginToWorldCenter(origin, footprint.Size);
            Vector2Int s = footprint.Size;

            // The box grown by border + 1 holds every cell one step outside the claimed territory.
            for (int x = -border - 1; x <= s.x + border; x++)
                for (int y = -border - 1; y <= s.y + border; y++)
                {
                    if (footprint.Claims(x, y, border)) continue;
                    var outer = new Vector2Int(origin.x + x, origin.y + y);
                    if (!HasOpenEdgeToTerritory(grid, outer, footprint, border, x, y)) continue;
                    TryAddDirection(grid, buffer, center, outer);
                }
        }

        // Does `outer` (local (x, y) of the box) share at least one UNFENCED edge with a claimed cell?
        // Each side reads the edge between the two cells in its canonical form (see Edge): the bottom
        // edge belongs to the cell above it, the left edge to the cell right of it.
        private static bool HasOpenEdgeToTerritory(IslandGrid grid, Vector2Int outer, Footprint footprint, int border, int x, int y)
        {
            if (footprint.Claims(x, y - 1, border) && grid.GetEdge(new Edge(outer, true)) == null) return true;                                   // south neighbour: our bottom edge
            if (footprint.Claims(x, y + 1, border) && grid.GetEdge(new Edge(new Vector2Int(outer.x, outer.y + 1), true)) == null) return true;    // north neighbour: its bottom edge
            if (footprint.Claims(x - 1, y, border) && grid.GetEdge(new Edge(outer, false)) == null) return true;                                  // west neighbour: our left edge
            if (footprint.Claims(x + 1, y, border) && grid.GetEdge(new Edge(new Vector2Int(outer.x + 1, outer.y), false)) == null) return true;   // east neighbour: its left edge
            return false;
        }

        // Add the direction toward `outerCell` if that cell is open: on-island and free of solid occupants.
        private static void TryAddDirection(IslandGrid grid, List<Vector2> buffer, Vector2 center, Vector2Int outerCell)
        {
            var cell = grid.GetCell(outerCell);
            if (cell == null) return;                       // off-island (map edge, with the border as the clear cell)
            if (IsSolid(cell.occupant)) return;             // another solid structure or its border — don't launch into it

            Vector2 dir = grid.GridToWorld(outerCell) - center;
            if (dir.sqrMagnitude < 1e-6f) return;
            buffer.Add(dir.normalized);
        }

        // An occupant closes a side unless its def says units pass through it. No def (an occupant
        // registered without one, as the grid tests do) is read as solid — the safe default.
        private static bool IsSolid(StructureInstance occupant)
            => occupant != null && (occupant.Def == null || !occupant.Def.passable);

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

        // Place the unit just outside the structure along dir: where the launch ray leaves the building,
        // plus unit radius and launchGap. On the grid the building is its FOOTPRINT (ExitDistance), which
        // is right for any shape — the collider's box bounds are not: for an L or a U the box edge can lie
        // in a notch, inside the bounds yet outside the walls. A scene-placed spawner has no footprint and
        // keeps measuring against its collider box.
        private Vector2 SpawnPosition(Vector2 dir, Unit unit)
        {
            if (grid != null && instance != null && instance.Def != null)
            {
                var footprint = instance.Def.Footprint;
                Vector2 center = grid.OriginToWorldCenter(instance.Cell, footprint.Size);
                float structureEdge = ExitDistance(grid, instance.Cell, footprint, center, dir);
                return center + dir * (structureEdge + unit.Radius + launchGap);
            }

            if (structureCollider == null)
                return (Vector2)transform.position + dir * launchGap; // fallback: measure from center

            return BoxExit(structureCollider.bounds.center, structureCollider.bounds.extents, dir)
                 + dir * (unit.Radius + launchGap);
        }

        // Distance along `dir` (unit length) from `center` to the point where the ray leaves the LAST
        // footprint cell it crosses — the launch ray's exit from the building. For a rectangle that is the
        // box edge along dir; for a shape the ray may leave a cell, cross a notch and enter another, and
        // the exit is where it finally clears the walls. Zero when the ray crosses no footprint cell (a
        // centre that falls in a notch, aiming away from the walls).
        public static float ExitDistance(IslandGrid grid, Vector2Int origin, Footprint footprint, Vector2 center, Vector2 dir)
        {
            float cs = grid.CellSize;
            float exit = 0f;
            for (int x = 0; x < footprint.Size.x; x++)
                for (int y = 0; y < footprint.Size.y; y++)
                {
                    if (!footprint.Contains(x, y)) continue;
                    float x0 = (origin.x + x) * cs, y0 = (origin.y + y) * cs;
                    if (RayLeavesBox(center, dir, x0, y0, x0 + cs, y0 + cs, out float t) && t > exit) exit = t;
                }
            return exit;
        }

        // Slab test: does the ray center + dir * t (t >= 0) pass through the box [x0,x1]x[y0,y1], and at
        // what t does it leave? A ray starting inside leaves at its first wall.
        private static bool RayLeavesBox(Vector2 center, Vector2 dir, float x0, float y0, float x1, float y1, out float tExit)
        {
            float tEnter = 0f;
            tExit = float.PositiveInfinity;
            return Slab(center.x, dir.x, x0, x1, ref tEnter, ref tExit)
                && Slab(center.y, dir.y, y0, y1, ref tEnter, ref tExit);
        }

        private static bool Slab(float c, float d, float lo, float hi, ref float tEnter, ref float tExit)
        {
            if (Mathf.Abs(d) < 1e-6f) return c >= lo && c <= hi;   // parallel to this axis: inside its slab or never
            float t0 = (lo - c) / d, t1 = (hi - c) / d;
            if (t0 > t1) (t0, t1) = (t1, t0);
            if (t0 > tEnter) tEnter = t0;
            if (t1 < tExit) tExit = t1;
            return tExit >= tEnter;
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
