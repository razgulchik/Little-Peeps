using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Carrying: while a hold lasts, the units the cursor runs over stick to it — as many as the hand has room for
    // (Capacity) — and go where it goes; let go, they scatter evenly to all sides, boosted as the hand says (LetGo:
    // a tap's boost). WHEN a press is a hold is the bare hand's business (BareHand): it owns the button and the ring,
    // so nothing here asks whose the press is.
    //
    // The way the cursor goes counts from where the button went down, before the press is known to be a hold (Begin,
    // Follow): whoever the cursor has come over by then sticks first, the moment it is (PickUp) — drawn to it from
    // wherever they have got to, since nothing holds them until then.
    //
    // Where they come down is CarryTrail's answer: in the cell under the cursor if a unit can stand there, else
    // in the last such cell the cursor came over — beside the building, the forest or the rock they were let go
    // over. The sea lets go by itself: the moment the cursor leaves the island the units scatter at the shore, not
    // boosted, since nobody let them go, and nothing sticks again until the next press. An empty cursor may cross it
    // freely — and whoever it had come over before it did stays on its own side.
    //
    // Let go over a building with a door, each unit is first offered to it (CollisionTarget.TryTakeIn), by the
    // door's own rule: a house takes the tired and the drunk while it has room, a tavern the sober it serves.
    // Only on letting go — carrying a unit across a house, or losing it to the sea beside one, is no visit.
    //
    // A carried unit is off the field AS IT IS (Unit.PickUp): its stamina stands still, its tool stays with it. It
    // gets another place and the boost it leaves with, and nothing else — no rest.
    public class CarrySystem : MonoBehaviour
    {
        [SerializeField] private IslandSystem islandSystem;

        [Header("Feel")]
        [Tooltip("How far from the cursor several carried units hang, world units. One alone hangs right on it.")]
        [SerializeField, Min(0f)] private float clusterRadius = 0.2f;

        [Tooltip("Seconds a carried unit takes to catch up with the cursor.")]
        [SerializeField, Min(0.001f)] private float followTime = 0.05f;

        [Tooltip("How fast a unit let go flies to where it comes down, world units per second. Seen only when " +
                 "that is away from the cursor: let go over a building or a forest.")]
        [SerializeField, Min(0.1f)] private float flySpeed = 25f;

        [Tooltip("Gap between the body of a unit set down and the sides of its cell, where a wall, a fence or " +
                 "the shore may run.")]
        [SerializeField, Min(0f)] private float dropGap = 0.1f;

        [Header("Look")]
        [Tooltip("Where carried units are drawn: over everything that stands on the island.")]
        [SerializeField] private string sortingLayerName = "Overlay";
        [SerializeField] private int sortingOrder = 1;

        [Header("Sound")]
        [Tooltip("A unit that sticks answers with its tap voice (UnitDef.tapSound). Empty = silent.")]
        [SerializeField] private SoundSystem soundSystem;

        [Tooltip("Seconds between one voice and the next when several units stick at once. 0 = all at once.")]
        [SerializeField, Min(0f)] private float voiceStep = 0.05f;

        private struct Carried
        {
            public Unit unit;
            public Vector2 at;         // its body's centre, trailing the cursor
            public Vector2 velocity;   // SmoothDamp's own
        }

        // A unit let go, on its way to where it comes down.
        private struct Flight
        {
            public Unit unit;
            public Vector2 at;          // its body's centre
            public Vector2 to;          // where that comes down...
            public Vector2 direction;   // ...and which way the unit leaves from there, how boosted (Unit.Resume)
            public float speedMultiplier;
            public float boostDuration;
        }

        private RunContext runContext;

        private bool underWay;        // the button is down and the cursor's way is kept...
        private bool taking;          // ...and it is a hold: whoever the cursor comes over sticks
        private float clusterAngle;   // the side the first carried unit hangs on, radians: new with every hold
        private readonly CarryTrail trail = new();
        private readonly List<Unit> waiting = new();   // come over on this way before it was a hold, in order
        private readonly List<Carried> carried = new();
        private readonly List<Flight> flying = new();
        private readonly List<Unit> reached = new();   // UnitReach's answer

        public void Initialize(RunContext context) => runContext = context;

        // Re-bind after a prestige: the room in the hand is the new run's.
        private void OnEnable() => EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
        private void OnDisable() => EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
        private void OnRunStarted(RunStartedEvent e) => runContext = e.Run;

        // How many units the hand carries at once. The base is none: a run starts with a hand that only taps, until
        // something gives it room (StatId.CarryCapacity — the meta perk, then perks on top of it).
        public int Capacity => runContext != null
            ? Mathf.Max(0, runContext.stats.ApplyCount(0, StatId.CarryCapacity))
            : 0;

        private int Room => Capacity - carried.Count;

        // The button has gone down here, `reach` round it. Nobody sticks yet: it may be a tap.
        public void Begin(Vector2 at, float reach)
        {
            Scatter(1f, 0f);   // nobody, unless the press before it was never let go

            var grid = islandSystem.Grid;
            if (grid == null) return;

            underWay = true;
            trail.Begin(grid, at);
            if (trail.OnIsland) Collect(at, at, reach);
        }

        // The press is a hold: whoever the cursor has come over sticks to it, and from here on everyone it comes over.
        public void PickUp()
        {
            if (!underWay) return;

            taking = true;
            clusterAngle = Random.value * 2f * Mathf.PI;
            Take(waiting);
            waiting.Clear();
        }

        // The cursor has come to `cursor`, `reach` round it. The sea ends the hold.
        public void Follow(Vector2 cursor, float reach)
        {
            if (!underWay) return;

            var grid = islandSystem.Grid;

            // Off the island with nothing to lose: the way starts over wherever it comes back.
            if (!trail.OnIsland)
            {
                trail.Begin(grid, cursor);
                if (trail.OnIsland) Collect(cursor, cursor, reach);
                return;
            }

            Vector2 from = trail.Head;
            bool stayed = trail.Extend(grid, cursor);
            Collect(from, trail.Head, reach);
            if (stayed) return;

            waiting.Clear();
            if (carried.Count > 0) Scatter(1f, 0f);
        }

        // The button is up, or the hand has something else to do: whom the door under the cursor takes goes in, and
        // the rest leave at `speedMultiplier` times their speed, easing back over `boostDuration` seconds. Harmless
        // when nothing is under way.
        public void LetGo(float speedMultiplier, float boostDuration)
        {
            if (carried.Count > 0) OfferAtDoor(islandSystem.Grid);
            Scatter(speedMultiplier, boostDuration);
        }

        private void Update()
        {
            // Scaled time: on a frozen screen a unit still on its way down waits in the air.
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            MoveCarried(dt);
            MoveFlying(dt);
        }

        private void Collect(Vector2 from, Vector2 to, float reach)
        {
            if (Room <= 0) return;

            UnitReach.Collect(from, to, reach, reached);
            if (taking)
            {
                Take(reached);
                return;
            }

            for (int i = 0; i < reached.Count; i++)
                if (!waiting.Contains(reached[i])) waiting.Add(reached[i]);
        }

        // Those of `units` there is room for, in their order.
        private void Take(List<Unit> units)
        {
            int room = Room;
            for (int i = 0, taken = 0; i < units.Count && taken < room; i++)
            {
                var unit = units[i];
                if (unit == null || !unit.IsOnField) continue;
                Grab(unit, taken * voiceStep);
                taken++;
            }
        }

        private void Grab(Unit unit, float voiceDelay)
        {
            carried.Add(new Carried { unit = unit, at = unit.BodyCenter });
            unit.PickUp(this, SortingLayer.NameToID(sortingLayerName), sortingOrder);
            if (soundSystem != null && unit.def != null) soundSystem.Play(unit.def.tapSound, voiceDelay);
        }

        // The hold is over, and whoever is still carried is on its way down, to leave away from the side it hung on.
        private void Scatter(float speedMultiplier, float boostDuration)
        {
            underWay = false;
            taking = false;
            waiting.Clear();

            var grid = islandSystem.Grid;
            int count = carried.Count;
            for (int i = 0; i < count; i++)
            {
                var unit = carried[i].unit;
                Vector2 offset = SpreadOffset(i, count, clusterAngle, clusterRadius);
                flying.Add(new Flight
                {
                    unit = unit,
                    at = carried[i].at,
                    to = trail.DropPoint(grid, offset, unit.BodyRadius + dropGap),
                    direction = SpreadDirection(i, count, clusterAngle),
                    speedMultiplier = speedMultiplier,
                    boostDuration = boostDuration,
                });
            }
            carried.Clear();
        }

        // The building the cursor is over — by the cell, as the Sell tool reads it. One that takes a unit in has
        // it leave our list on the way (Unit.LeaveHand → Forget), so only a refused one is stepped over. A
        // construction site has no doors yet.
        private void OfferAtDoor(IslandGrid grid)
        {
            Vector2Int cell = grid.WorldToGrid(trail.Head);
            if (CarryTrail.IsFree(grid, cell)) return;

            var building = grid.GetCell(cell)?.occupant?.RuntimeObject;
            if (building == null) return;

            for (int i = 0; i < carried.Count;)
                if (!building.TryTakeIn(carried[i].unit)) i++;
        }

        private void MoveCarried(float dt)
        {
            for (int i = 0; i < carried.Count; i++)
            {
                var c = carried[i];
                Vector2 target = trail.Head + SpreadOffset(i, carried.Count, clusterAngle, clusterRadius);
                c.at = Vector2.SmoothDamp(c.at, target, ref c.velocity, followTime, Mathf.Infinity, dt);
                c.unit.transform.position = Feet(c.unit, c.at);
                carried[i] = c;
            }
        }

        // Backwards, so one that has come down can be taken off the list in place.
        private void MoveFlying(float dt)
        {
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var f = flying[i];
                f.at = Vector2.MoveTowards(f.at, f.to, flySpeed * dt);
                if (f.at != f.to)
                {
                    f.unit.transform.position = Feet(f.unit, f.at);
                    flying[i] = f;
                    continue;
                }

                flying.RemoveAt(i);
                f.unit.PutDown(Feet(f.unit, f.to), f.direction, f.speedMultiplier, f.boostDuration);
            }
        }

        // Where the unit stands for its body's centre to be at `body`.
        private static Vector2 Feet(Unit unit, Vector2 body) => body - unit.BodyOffset;

        // The unit is not ours any more: it came down, went in at a door, or left the game (Unit.LeaveHand).
        public void Forget(Unit unit)
        {
            for (int i = carried.Count - 1; i >= 0; i--)
                if (carried[i].unit == unit) carried.RemoveAt(i);
            for (int i = flying.Count - 1; i >= 0; i--)
                if (flying[i].unit == unit) flying.RemoveAt(i);
        }

        // Outward for the `index`-th of `count` units spread evenly all round, the first at `angle` (radians).
        public static Vector2 SpreadDirection(int index, int count, float angle)
        {
            float a = angle + index * (2f * Mathf.PI / Mathf.Max(1, count));
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        // Where that unit hangs, off the cursor: on a ring round it, or right on it when there is no other.
        public static Vector2 SpreadOffset(int index, int count, float angle, float radius)
            => count > 1 ? SpreadDirection(index, count, angle) * radius : Vector2.zero;
    }
}
