using System.Collections.Generic;
using UnityEngine;

// Placed on a building; drives a per-slot spawn -> travel -> return -> rest cycle.
// Each slot is an independent place for one little person: it launches a unit, goes on its
// OWN cooldown, then waits to accept ANY matching-type unit (units are shared per type, not
// owned). capacity = number of slots and is registered into SpawnSystem's global per-type cap.
[RequireComponent(typeof(Building))]
public class Spawner : MonoBehaviour
{
    [SerializeField] private SpawnSystem spawnSystem;

    [Header("Units")]
    [SerializeField] public UnitDef unitDef;
    [SerializeField] public int capacity = 1;

    [Header("Cycle timing (seconds)")]
    [SerializeField] private float restDuration = 3f;
    [SerializeField] private float lockoutDuration = 2f;

    [Header("Launch")]
    [SerializeField] private float launchSpeedMultiplier = 2.5f;
    [SerializeField] private float launchBoostDuration = 1f;
    [SerializeField] private float launchGap = 0.1f; // clearance between the building collider edge and the unit collider edge at launch

    private enum SlotState { Free, Cooldown, Occupied }

    // One slot = one independent place. Reference type so we mutate it in place inside foreach.
    private class Slot
    {
        public SlotState state;
        public float timer;   // counts down in Cooldown and Occupied
        public Unit unit;     // the resting unit (Occupied only)
    }

    private Building building;
    private Collider2D buildingCollider;
    private List<Slot> slots;   // null until BeginWarmup runs
    private bool registered;

    private void Awake()
    {
        building = GetComponent<Building>();
        buildingCollider = GetComponentInChildren<Collider2D>();
    }

    private void OnEnable()
    {
        EventBus<CollisionEvent>.Subscribe(OnCollision);
    }

    private void OnDisable()
    {
        EventBus<CollisionEvent>.Unsubscribe(OnCollision);
    }

    // Optional runtime injection (BuildingSystem calls this when placing a building at runtime).
    public void Initialize(SpawnSystem system)
    {
        spawnSystem = system;
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

        BeginWarmup();
    }

    // Called on placement (and later by BuildingSystem on build/move). Every slot holds its
    // unit resting INSIDE for restDuration before its first launch — so placing or moving a
    // building never produces an instant launch (anti-abuse). Safe to call again on move:
    // it won't re-register capacity or recreate slots, just restarts the rest delay.
    public void BeginWarmup()
    {
        if (spawnSystem == null || unitDef == null) return;

        if (!registered)
        {
            capacity = Mathf.Max(1, capacity);
            spawnSystem.RegisterCapacity(unitDef.unitType, capacity);
            registered = true;
        }

        if (slots == null)
        {
            slots = new List<Slot>(capacity);
            for (int i = 0; i < capacity; i++) slots.Add(new Slot());
        }

        foreach (var slot in slots)
            FillSlot(slot);
    }

    // Upgrade hook: grow the building to newCapacity slots at runtime. Each NEW slot spawns a
    // new unit that rests first, then launches — i.e. behaves exactly like a freshly built slot.
    // Existing units keep running. Call this from the upgrade system.
    public void IncreaseCapacity(int newCapacity)
    {
        // Not warmed up yet (called before Start): just raise the target; BeginWarmup uses it.
        if (!registered || slots == null)
        {
            capacity = Mathf.Max(capacity, newCapacity);
            return;
        }

        if (newCapacity <= capacity) return; // only growth here; downgrades go through DecreaseCapacity

        int delta = newCapacity - capacity;
        spawnSystem.RegisterCapacity(unitDef.unitType, delta); // raise the global cap first
        capacity = newCapacity;                                // keep in sync (OnDestroy unregisters `capacity`)

        for (int i = 0; i < delta; i++)
        {
            var slot = new Slot();
            slots.Add(slot);
            FillSlot(slot);
        }
    }

    // Downgrade hook: shrink the building to newCapacity slots. FILLER for now — not implemented.
    // TODO: when slot downgrades exist, pick which slots to remove, deal with their units
    // (resting ones via SpawnSystem.Despawn; roaming ones need a recall path), then
    // spawnSystem.UnregisterCapacity(unitDef.unitType, delta), trim `slots` and `capacity`.
    public void DecreaseCapacity(int newCapacity)
    {
        // TODO: implement when slot downgrades are introduced.
    }

    // Initialize / refill one slot: keep a resting unit (restart its rest), otherwise spawn one
    // into rest, or leave it Free if the global cap is full. Shared by BeginWarmup and upgrades.
    private void FillSlot(Slot slot)
    {
        if (slot.state == SlotState.Occupied && slot.unit != null)
        {
            // Already holds a unit (e.g. moved mid-rest) — just restart its rest delay.
            slot.timer = restDuration;
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
            switch (slot.state)
            {
                case SlotState.Cooldown:
                    slot.timer -= dt;
                    if (slot.timer <= 0f) slot.state = SlotState.Free;
                    break;

                case SlotState.Occupied:
                    slot.timer -= dt;
                    if (slot.timer <= 0f) LaunchFromSlot(slot, slot.unit);
                    break;
            }
        }
    }

    private void OnCollision(CollisionEvent e)
    {
        if (slots == null || e.Building != building || e.Unit == null || unitDef == null) return;
        if (e.Unit.Type != unitDef.unitType) return;

        // Put the unit in the first slot that has finished its cooldown and is free.
        foreach (var slot in slots)
        {
            if (slot.state != SlotState.Free) continue;
            OccupySlot(slot, e.Unit);
            return;
        }
        // No free slot — the unit bounces on toward another building.
    }

    // Take a unit into a slot to rest inside the building.
    private void OccupySlot(Slot slot, Unit unit)
    {
        slot.state = SlotState.Occupied;
        slot.timer = restDuration;
        slot.unit = unit;
        unit.EnterRest();
    }

    private void LaunchFromSlot(Slot slot, Unit unit)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;

        unit.transform.position = SpawnPosition(dir, unit);
        unit.Launch(dir, launchSpeedMultiplier, launchBoostDuration);

        slot.unit = null;
        slot.state = SlotState.Cooldown;
        slot.timer = lockoutDuration;
    }

    // Place the unit just outside the building collider along dir:
    // building edge (along dir) + unit radius + launchGap.
    private Vector2 SpawnPosition(Vector2 dir, Unit unit)
    {
        if (buildingCollider == null)
            return (Vector2)transform.position + dir * launchGap; // fallback: measure from center

        Vector2 center = buildingCollider.bounds.center;
        Vector2 extents = buildingCollider.bounds.extents;

        // Distance from the box center to its edge along dir.
        float ax = Mathf.Abs(dir.x);
        float ay = Mathf.Abs(dir.y);
        float tx = ax > 1e-4f ? extents.x / ax : float.PositiveInfinity;
        float ty = ay > 1e-4f ? extents.y / ay : float.PositiveInfinity;
        float buildingEdge = Mathf.Min(tx, ty);

        return center + dir * (buildingEdge + unit.Radius + launchGap);
    }

    private void OnDestroy()
    {
        if (!registered) return;

        // Despawn units currently resting here so they don't leak as hidden objects.
        // TODO: full global active-count reconciliation (also units this building launched
        // that are still roaming) belongs in BuildingSystem.RemoveBuilding.
        if (slots != null && spawnSystem != null)
        {
            foreach (var slot in slots)
            {
                if (slot.state == SlotState.Occupied && slot.unit != null)
                    spawnSystem.Despawn(slot.unit);
            }
        }

        if (spawnSystem != null && unitDef != null)
            spawnSystem.UnregisterCapacity(unitDef.unitType, capacity);
    }
}
