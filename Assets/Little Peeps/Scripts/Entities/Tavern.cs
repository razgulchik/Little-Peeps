using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The tavern: a solid building any SOBER worker walks into for a drink — working or tired, whatever
    // its profession — as long as there is a free seat. A drunk worker bounces off it like off any wall.
    //
    // It serves, it does not employ: the coins are paid ONCE, the moment the worker comes in (the
    // "service exception" — a tired worker, who may not work, still pays). Inside, the worker is HELD,
    // not rested (Unit.EnterHold): it keeps its population slot, its profession and the tool it took from
    // a rack, and its stamina clock stands still — it neither drains nor refills. After stayDuration it
    // is let out through any open side, exactly the way a house launches (StructureExits), only walking
    // rather than thrown, and Drunk: slower for drunkDuration, otherwise the same worker it went in as. A
    // tired one leaves tired AND drunk. A house takes a drunk worker in and resets it (Unit.EnterRest),
    // so tavern → house is a legitimate loop the doc wants balanced, not forbidden.
    //
    // With sendTiredHome on, a guest that leaves tired AND drunk is not let out at random: the tavern aims
    // it at a random house nearby with a free slot (BilliardAim — straight, or banked off the coast), and
    // it walks there. No shot found, the ordinary release. A working guest always leaves the ordinary way.
    //
    // Sealed in on every side, the tavern keeps its guests and tries again after another stay — never
    // removes or teleports them, like a house. Only reachable through StructureSystem (it needs the grid
    // for the exits); a guest leaves the game from inside only through SpawnSystem.Despawn (build mode,
    // run teardown), which tells the tavern through the unit (Unit.LeaveHold → Forget). Selling or moving
    // happens in build mode only, by which time every guest is gone.
    [RequireComponent(typeof(Structure))]
    public class Tavern : MonoBehaviour, IEntrance
    {
        [Header("Service")]
        [Tooltip("Workers inside at once. A full tavern is a plain wall.")]
        [SerializeField, Min(1)] private int capacity = 2;

        [Tooltip("Seconds a worker stays inside before it is let out.")]
        [SerializeField, Min(0f)] private float stayDuration = 4f;

        [Tooltip("Coins paid once, the moment a worker comes in — for serving it, not for work, so a tired " +
                 "worker pays too. Credited through the production gateway like any harvest: bonuses, " +
                 "prestige and the coin effect come with it.")]
        [SerializeField, Min(0f)] private float payout = 1f;

        [Tooltip("What the payout counts as: a ResourceSourceDef with Coins as its resource and the coin " +
                 "pickup effect. Its worker yields are NOT read — the payout above is. Also the scope a " +
                 "'more coins from taverns' perk would name.")]
        [SerializeField] private ResourceSourceDef coinSource;

        [Tooltip("Where the coin effect and the floating number leave from. Empty = this transform.")]
        [SerializeField] private Transform fxAnchor;

        [Header("Drunk")]
        [Tooltip("Seconds of simulation time a worker stays Drunk after leaving. A house sobers it up sooner.")]
        [SerializeField, Min(0f)] private float drunkDuration = 10f;

        [Tooltip("Speed of a drunk worker as a fraction of what it would otherwise be — multiplied with " +
                 "the tired factor when both hold. Keep tired × drunk above ~0.12: slower than that, a " +
                 "worker covers less than a cell per stuck window and is rescued home as stuck.")]
        [SerializeField, Range(0.1f, 1f)] private float drunkSpeedMultiplier = 0.6f;

        [Header("Release")]
        [Tooltip("Clearance between the building's edge and the worker's collider as it steps out.")]
        [SerializeField, Min(0f)] private float releaseGap = 0.1f;

        [Tooltip("Random spread on the chosen side's direction, so guests don't all leave on exact lines.")]
        [SerializeField, Min(0f)] private float releaseJitterDegrees = 12f;

        [Header("Send home")]
        [Tooltip("Tired AND drunk guests are aimed at a random house nearby with a free slot and walk there — " +
                 "straight, or banked off the island's coast. Nothing but the coast is banked off: a shot " +
                 "that would touch anything else is not taken. No shot found → the ordinary release.")]
        [SerializeField] private bool sendTiredHome;

        [Tooltip("Houses within this distance (world units, from the tavern's root to the house's) are the " +
                 "candidates.")]
        [SerializeField, Min(0f)] private float homeSearchRadius = 12f;

        [Tooltip("Most coast bounces a shot may take; fewer is always preferred. Also capped by the unit's " +
                 "Exact Bounces — past those a bounce gets its random turn and no aim holds.")]
        [SerializeField, Range(0, 3)] private int maxCoastBounces = 3;

        // One seat taken: who, and how long until it is let out. A struct in a list — the list is the
        // only state, its count is the occupancy.
        private struct Guest
        {
            public Unit unit;
            public float timeLeft;
        }

        private readonly List<Guest> guests = new();
        private readonly List<Vector2> exitDirs = new();   // reused per release
        private readonly List<Spawner> nearbyHouses = new();   // reused per aimed release
        private readonly BilliardAim aim = new();

        // Injected by StructureSystem on placement, before Start. The instance's Cell follows a move.
        private ResourceSystem resourceSystem;
        private SpawnSystem spawnSystem;     // the houses, for sendTiredHome
        private IslandSystem islandSystem;   // the coast, for sendTiredHome
        private IslandGrid grid;
        private StructureInstance instance;

        public int Capacity => capacity;
        public int Occupied => guests.Count;

        public void Initialize(ResourceSystem resources, SpawnSystem spawns, IslandSystem island,
                               IslandGrid grid, StructureInstance instance)
        {
            resourceSystem = resources;
            spawnSystem = spawns;
            islandSystem = island;
            this.grid = grid;
            this.instance = instance;
        }

        private void Start()
        {
            if (grid == null || instance == null || instance.Def == null)
                Debug.LogError($"Tavern on '{name}' has no grid context — it must be built through StructureSystem, " +
                               "and until then it lets nobody in.", this);
            if (coinSource == null)
                Debug.LogError($"Tavern on '{name}' has no coin source (ResourceSourceDef) — it pays nothing.", this);
            if (resourceSystem == null)
                Debug.LogError($"Tavern on '{name}' has no ResourceSystem — it pays nothing.", this);
            if (sendTiredHome && (spawnSystem == null || islandSystem == null || islandSystem.GroundTilemap == null))
                Debug.LogWarning($"Tavern on '{name}' sends tired guests home, but has no houses or no coast to " +
                                 "aim with — they will leave the ordinary way.", this);
        }

        // IEntrance — asked for every unit that hits the tavern. The door rule: anyone sober with a seat
        // free. A drunk unit is refused and simply bounces (the door animation hangs here later). Without
        // grid context there would be no way back out, so nobody comes in at all.
        public bool TryEnter(Unit unit)
        {
            if (unit == null || unit.IsDrunk) return false;
            if (guests.Count >= capacity) return false;
            if (grid == null || instance == null || instance.Def == null) return false;

            guests.Add(new Guest { unit = unit, timeLeft = stayDuration });
            unit.EnterHold(this);
            Pay(unit);
            return true;
        }

        // Once, on entry. unit.Type still names the profession it came in with: the yield modifiers key on
        // it the same way they do for a harvest.
        private void Pay(Unit unit)
        {
            if (resourceSystem == null || coinSource == null || payout <= 0f) return;
            resourceSystem.AddHarvest(coinSource, unit.Type, payout, FxOrigin);
        }

        private Vector3 FxOrigin => fxAnchor != null ? fxAnchor.position : transform.position;

        // Scaled time, like a house's rest: a paused game (build mode, perk pick) keeps the guests inside.
        // Walked backwards so a guest let out can be removed in place.
        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = guests.Count - 1; i >= 0; i--)
            {
                var guest = guests[i];
                guest.timeLeft -= dt;

                if (guest.timeLeft <= 0f)
                {
                    if (TryRelease(guest.unit)) { guests.RemoveAt(i); continue; }
                    guest.timeLeft = stayDuration;   // sealed in: keep it inside, try again after another stay
                }

                guests[i] = guest;
            }
        }

        // Let `unit` out drunk: aimed at a house if it is a tired guest and sendTiredHome is on, otherwise
        // through an open side, the house's way. Drunk goes on FIRST — the aim reads the drunk walking speed,
        // and the first velocity the unit gets is already it. False = every side is closed; the unit stays
        // exactly where it is (drunk already, which changes nothing inside — the next try puts it on anew).
        private bool TryRelease(Unit unit)
        {
            unit.MakeDrunk(drunkDuration, drunkSpeedMultiplier);

            if (sendTiredHome && unit.IsTired && unit.IsDrunk && TryAimHome(unit, out Vector2 bodyStart, out Vector2 homeDir))
            {
                unit.transform.position = bodyStart - unit.BodyOffset;
                unit.Resume(homeDir);
                return true;
            }

            var footprint = instance.Def.Footprint;
            if (!StructureExits.TryPickDirection(grid, instance.Cell, footprint, instance.Def.border,
                                                 exitDirs, releaseJitterDegrees, out Vector2 dir))
                return false;

            unit.transform.position = StructureExits.ExitPoint(grid, instance.Cell, footprint, dir, unit.Radius + releaseGap);
            unit.Resume(dir);
            return true;
        }

        // A shot home for a tired drunk (see BilliardAim): the candidates are the houses within
        // homeSearchRadius that have a free slot right now; one the shot can reach is picked at random.
        private bool TryAimHome(Unit unit, out Vector2 bodyStart, out Vector2 dir)
        {
            bodyStart = dir = Vector2.zero;
            if (spawnSystem == null || islandSystem == null || islandSystem.GroundTilemap == null) return false;

            spawnSystem.CollectHousesWithFreeSlot(transform.position, homeSearchRadius, nearbyHouses);
            return aim.TryAimFromStructure(unit, grid, instance, transform, islandSystem.GroundTilemap.gameObject,
                                           nearbyHouses, maxCoastBounces, releaseGap, out bodyStart, out dir);
        }

        // The unit left the game from inside (Unit.LeaveHold, from SpawnSystem.Despawn): free its seat
        // without letting it out — it is on its way back to the pool.
        public void Forget(Unit unit)
        {
            for (int i = guests.Count - 1; i >= 0; i--)
                if (guests[i].unit == unit) guests.RemoveAt(i);
        }
    }
}
