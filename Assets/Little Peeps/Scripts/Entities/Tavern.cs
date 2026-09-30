using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LittlePeeps
{
    // The tavern: a solid building a SOBER worker walks into for a drink — working or tired — as long as
    // there is a free seat and its profession is served. Who is served, and what they pay, is the coin
    // source's worker yields, as for every source; "everyone" is the def listing every profession. A drunk
    // worker, or one the def doesn't list, bounces off it like off any wall.
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
    // With autopilot on, a guest that leaves tired AND drunk is put on autopilot (Unit.EnterAutopilot): it
    // leaves the ordinary way, and from then on finds its way home by itself while it stays drunk. The
    // tavern only switches it on. A working guest leaves without it.
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

        [Tooltip("The tavern's source: Coins as its resource, the coin pickup effect, and the worker yields — " +
                 "who is served and what they pay, once, the moment they come in (for being served, not for " +
                 "work, so a tired worker pays too). An unlisted worker is not let in. Credited through the " +
                 "production gateway like any harvest: bonuses, prestige and the coin effect come with it. " +
                 "Also the scope a 'more coins from taverns' perk would name.")]
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

        [Header("Autopilot")]
        [Tooltip("Tired guests leave on autopilot: while drunk, at every bounce they turn toward a house in " +
                 "sight, if there is one. The unit's Autopilot settings say how far it looks and how sharply " +
                 "it may turn.")]
        [FormerlySerializedAs("sendTiredHome")]
        [SerializeField] private bool autopilot;

        // One seat taken: who, and how long until it is let out. A struct in a list — the list is the
        // only state, its count is the occupancy.
        private struct Guest
        {
            public Unit unit;
            public float timeLeft;
        }

        private readonly List<Guest> guests = new();
        private readonly List<Vector2> exitDirs = new();   // reused per release

        // Injected by StructureSystem on placement, before Start. The instance's Cell follows a move.
        private ResourceSystem resourceSystem;
        private IslandGrid grid;
        private StructureInstance instance;

        public int Capacity => capacity;
        public int Occupied => guests.Count;

        public void Initialize(ResourceSystem resources, IslandGrid grid, StructureInstance instance)
        {
            resourceSystem = resources;
            this.grid = grid;
            this.instance = instance;
        }

        private void Start()
        {
            if (grid == null || instance == null || instance.Def == null)
                Debug.LogError($"Tavern on '{name}' has no grid context — it must be built through StructureSystem, " +
                               "and until then it lets nobody in.", this);
            if (coinSource == null)
                Debug.LogError($"Tavern on '{name}' has no coin source (ResourceSourceDef) — it serves nobody.", this);
            if (resourceSystem == null)
                Debug.LogError($"Tavern on '{name}' has no ResourceSystem — it pays nothing.", this);
        }

        // IEntrance — asked for every unit that hits the tavern. The door rule: sober, served (listed in the
        // coin source) and a seat free. Anyone else is refused and simply bounces (the door animation hangs
        // here later). Without grid context there would be no way back out, so nobody comes in at all.
        public bool TryEnter(Unit unit)
        {
            if (unit == null || unit.IsDrunk) return false;
            if (coinSource == null || !coinSource.TryGetYield(unit.Type, out float price)) return false;
            if (guests.Count >= capacity) return false;
            if (grid == null || instance == null || instance.Def == null) return false;

            guests.Add(new Guest { unit = unit, timeLeft = stayDuration });
            unit.EnterHold(this);
            Pay(unit, price);
            return true;
        }

        // Once, on entry. unit.Type still names the profession it came in with: the yield modifiers key on
        // it the same way they do for a harvest.
        private void Pay(Unit unit, float price)
        {
            if (resourceSystem == null || price <= 0f) return;
            resourceSystem.AddHarvest(coinSource, unit.Type, price, FxOrigin);
        }

        private Vector3 FxOrigin => fxAnchor != null ? fxAnchor.position : transform.position;

        // Scaled time, like a house's rest: a paused game (build mode, perk pick) keeps the guests inside.
        // Walked backwards so a guest let out can be removed in place.
        private void Update()
        {
            AimProbe.Tick();
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

        // Let `unit` out drunk through an open side, the house's way — on autopilot if it is tired and the
        // tavern sends its guests home that way. Drunk goes on FIRST, so the first velocity the unit gets is
        // already the drunk one. False = every side is closed; the unit stays exactly where it is (drunk
        // already, which changes nothing inside — the next try puts it on anew).
        private bool TryRelease(Unit unit)
        {
            unit.MakeDrunk(drunkDuration, drunkSpeedMultiplier);

            var footprint = instance.Def.Footprint;
            if (!StructureExits.TryPickDirection(grid, instance.Cell, footprint, instance.Def.border,
                                                 exitDirs, releaseJitterDegrees, out Vector2 dir))
                return false;

            unit.transform.position = StructureExits.ExitPoint(grid, instance.Cell, footprint, dir, unit.Radius + releaseGap);
            unit.Resume(dir);

            if (autopilot && unit.IsTired)
            {
                unit.EnterAutopilot();              // after Resume, which clears it
                AimProbe.Released(this, unit);
            }
            return true;
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
