using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The autopilot's eyes: whether a unit has a straight, clear way to a house, like a billiard ball lined
    // up on a pocket. It decides nothing — it answers "which way to a house in sight", and the unit on
    // autopilot acts on it at every bounce (Unit.OnCollisionEnter2D). Nothing is predicted past one
    // straight leg, so nothing can go stale on the way: a unit knocked off by an animal, or bouncing off a
    // house that filled up meanwhile, just looks again at its next bounce, from where it is.
    //
    // A way is clear when the first thing the unit's body circle runs into along it is a house with a free
    // slot — the one aimed at or any other; it takes a drunk in on contact. Animals are ignored: they move,
    // and one that walks into the way only gives the unit its next bounce. Other units are never in the way
    // (units pass through each other). Houses are tried in random order, at the middle of their collider:
    // the house's width is the room for error.
    //
    // Casts the unit's body circle against exactly what the unit collides with (its layer's collision mask,
    // no triggers). Static, buffers shared: every caller runs on the main thread, one at a time, and none
    // keeps anything between calls.
    public static class BilliardAim
    {
        private const float MinWallAngle = 15f;   // a way off a wall leaves at least this far off its surface — shallower slides along it
        private const float Skin = 0.02f;         // off the wall before looking, so the cast starts clear of it

        private static readonly List<RaycastHit2D> hits = new();
        private static readonly List<Spawner> houses = new();   // the houses in reach, in the order they are tried

        // Off a wall, at the moment of a bounce: `bodyAt` is the unit's body centre, `wallNormal` points away
        // from the wall, `reflected` is where the physics sends the unit. The way to a house with a free slot
        // within `radius` that leaves at least MinWallAngle off the wall and no more than maxTurnDegrees off
        // `reflected`, with a free house the first thing on it.
        public static bool TrySteer(Unit unit, SpawnSystem spawns, float radius, Vector2 bodyAt, Vector2 wallNormal,
                                    Vector2 reflected, float maxTurnDegrees, out Vector2 dir)
        {
            dir = reflected;
            if (unit == null || spawns == null) return false;

            float bodyRadius = unit.BodyRadius;
            var filter = FilterFor(unit);
            float minOut = Mathf.Sin(MinWallAngle * Mathf.Deg2Rad);
            float minAlign = Mathf.Cos(Mathf.Clamp(maxTurnDegrees, 0f, 180f) * Mathf.Deg2Rad);
            Vector2 start = bodyAt + wallNormal * Skin;
            CollectHouses(spawns, bodyAt, radius);

            for (int i = 0; i < houses.Count; i++)
            {
                Vector2 to = HouseCenter(houses[i]) - start;
                if (to.sqrMagnitude < 1e-6f) continue;
                Vector2 d = to.normalized;

                if (Vector2.Dot(d, wallNormal) < minOut) continue;   // into the wall, or along it
                if (Vector2.Dot(d, reflected) < minAlign) continue;   // turned too far off the bounce
                if (!IsClear(start, d, to.magnitude, bodyRadius, filter)) continue;

                dir = d;
                return true;
            }
            return false;
        }

        // The houses with a free slot within `radius` of `from`, shuffled.
        private static void CollectHouses(SpawnSystem spawns, Vector2 from, float radius)
        {
            spawns.CollectHousesWithFreeSlot(from, radius, houses);
            AimProbe.Nearby(houses.Count);
            for (int i = houses.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (houses[i], houses[j]) = (houses[j], houses[i]);
            }
        }

        // Whether the first thing the circle runs into from `origin` along `dir` (within `distance`) is a
        // house with a free slot. Skipped: animals, and hits at distance 0 — what the circle already touches
        // as it starts, the wall it is leaving. Hits are not assumed to come sorted.
        private static bool IsClear(Vector2 origin, Vector2 dir, float distance, float radius, ContactFilter2D filter)
        {
            int count = Physics2D.CircleCast(origin, radius, dir, filter, hits, distance);
            AimProbe.Cast(count);

            Collider2D nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                if (h.distance <= 0f || h.distance >= best || h.collider.GetComponentInParent<Animal>() != null) continue;
                best = h.distance;
                nearest = h.collider;
            }
            if (nearest == null)
            {
                AimProbe.Blocked(null, null);
                return false;
            }

            var house = nearest.GetComponentInParent<Spawner>();
            bool clear = house != null && house.HasFreeSlot;
            if (!clear) AimProbe.Blocked(nearest, house);
            return clear;
        }

        private static ContactFilter2D FilterFor(Unit unit)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(unit.BodyLayer));
            return filter;
        }

        // Where a way aims: the middle of the house's body, the widest target it offers.
        private static Vector2 HouseCenter(Spawner house)
        {
            var body = house.GetComponentInChildren<Collider2D>();
            return body != null ? (Vector2)body.bounds.center : (Vector2)house.transform.position;
        }
    }

    // AimProbe — autopilot diagnostics, kept for balance testing: what one look costs (ms, casts; every
    // autopilot bounce is one), what blocks the view, and where the unit ends up — logged per event, with a
    // running TOTAL line. Off by default: every method is [Conditional("AIM_PROBE")], so without that
    // scripting define (Project Settings → Player → Scripting Define Symbols) each call compiles away,
    // arguments included, and costs nothing. Statistics start over on every Play Mode entry.
    //
    // A unit is followed from the release until it leaves the field. It enters a house ON AN AIMED LEG when
    // the collision that takes it in is the next one after a bounce turned it at a house — the house itself
    // may or may not be counted, depending on which callback runs first.
    public static class AimProbe
    {
        private const string Define = "AIM_PROBE";

        private struct Shot
        {
            public Unit unit;
            public Rigidbody2D body;
            public float time;
            public int attempts, steered;
            public int aimedAt;   // collision count when last put on a straight way; -1 = never
        }

        private static readonly List<Shot> shots = new();
        private static readonly Dictionary<string, int> blockers = new();
        private static readonly List<string> lookBlockers = new();
        private static readonly System.Diagnostics.Stopwatch watch = new();

        private static int lookCasts, lookHits, lookNearby;   // the current look, since Begin
        private static int releases, attempts, steered, noHouse, blocked;
        private static int houseAimed, houseDrifted, leftOtherwise, leftDespawned, leftTavern, leftRescued;
        private static int events, timed, maxCasts;
        private static double totalMs, maxMs, totalToHouse;
        private static long totalCasts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            shots.Clear();
            blockers.Clear();
            lookBlockers.Clear();
            lookCasts = lookHits = lookNearby = 0;
            releases = attempts = steered = noHouse = blocked = 0;
            houseAimed = houseDrifted = leftOtherwise = leftDespawned = leftTavern = leftRescued = 0;
            events = timed = maxCasts = 0;
            totalMs = maxMs = totalToHouse = 0;
            totalCasts = 0;
        }

        // A look starts (Unit.TryAutopilot).
        [System.Diagnostics.Conditional(Define)]
        public static void Begin()
        {
            lookCasts = lookHits = lookNearby = 0;
            lookBlockers.Clear();
            watch.Restart();
        }

        // From BilliardAim, during a look.
        [System.Diagnostics.Conditional(Define)]
        public static void Nearby(int houses) => lookNearby = houses;

        [System.Diagnostics.Conditional(Define)]
        public static void Cast(int hitCount)
        {
            lookCasts++;
            lookHits += hitCount;
        }

        // What stood first in the way; `nearest` null = the cast hit nothing, the house itself missed.
        [System.Diagnostics.Conditional(Define)]
        public static void Blocked(Collider2D nearest, Spawner house)
        {
            string name;
            if (nearest == null) name = "(nothing hit — the house missed)";
            else if (house != null) name = "a full house";
            else
            {
                var structure = nearest.GetComponentInParent<Structure>();
                name = (structure != null ? structure.name : nearest.name).Replace("(Clone)", "").Trim();
            }
            lookBlockers.Add(name);
        }

        // The tavern let `unit` out on autopilot.
        [System.Diagnostics.Conditional(Define)]
        public static void Released(Tavern tavern, Unit unit)
        {
            releases++;
            shots.Add(new Shot { unit = unit, body = unit.GetComponent<Rigidbody2D>(), time = Time.time, aimedAt = -1 });
            Debug.Log($"[AimProbe] {tavern.name} lets out {Who(unit)} on autopilot", tavern);
        }

        // Right after an autopilot look at a bounce.
        [System.Diagnostics.Conditional(Define)]
        public static void Steered(Unit unit, bool found)
        {
            string cost = Cost();
            attempts++;
            if (found) steered++;
            if (lookNearby == 0) noHouse++;
            blocked += lookBlockers.Count;
            foreach (var name in lookBlockers)
                blockers[name] = blockers.TryGetValue(name, out int n) ? n + 1 : 1;

            int i = IndexOf(unit);
            if (i >= 0)
            {
                var shot = shots[i];
                shot.attempts++;
                if (found) { shot.steered++; shot.aimedAt = unit.BouncesSinceRelease; }
                shots[i] = shot;
            }
            Debug.Log($"[AimProbe] autopilot {Who(unit)} at collision #{unit.BouncesSinceRelease}: {cost}, nearby houses " +
                      $"{lookNearby} → {(found ? "STEERED at a house" : "none in sight, plain bounce")}", unit);
        }

        // From Spawner.TryEnter — the door, not TryShelter (a stuck rescue is "left the field another way").
        [System.Diagnostics.Conditional(Define)]
        public static void Entered(Spawner house, Unit unit)
        {
            int i = IndexOf(unit);
            if (i < 0) return;
            var shot = shots[i];
            shots.RemoveAt(i);

            int collisions = unit.BouncesSinceRelease;
            bool aimed = shot.aimedAt >= 0 && collisions <= shot.aimedAt + 1;
            if (aimed) houseAimed++; else houseDrifted++;
            totalToHouse += Time.time - shot.time;
            Resolve(shot, $"entered {house.name} {(aimed ? "ON AN AIMED LEG" : "after drifting")} at collision " +
                          $"#{collisions} ({shot.steered} of {shot.attempts} bounces steered)");
        }

        // From Tavern.Update; several taverns calling it in one frame is harmless.
        [System.Diagnostics.Conditional(Define)]
        public static void Tick()
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i];
                if (shot.unit != null && shot.unit.isActiveAndEnabled && shot.body != null && shot.body.simulated) continue;

                shots.RemoveAt(i);
                leftOtherwise++;
                string where;
                if (shot.unit == null || !shot.unit.gameObject.activeInHierarchy) { leftDespawned++; where = "DESPAWNED"; }
                else if (shot.unit.IsHeld) { leftTavern++; where = "into a TAVERN"; }
                else { leftRescued++; where = "RESCUED into a house (stuck)"; }
                Resolve(shot, $"left the field another way: {where} ({shot.steered} of {shot.attempts} bounces steered)");
            }
        }

        private static string Cost()
        {
            watch.Stop();
            double ms = watch.Elapsed.TotalMilliseconds;

            events++;
            if (events == 1) return $"{ms:F2} ms (first: JIT warm-up, left out of avg/max), {lookCasts} casts";

            timed++;
            totalMs += ms;
            if (ms > maxMs) maxMs = ms;
            totalCasts += lookCasts;
            if (lookCasts > maxCasts) maxCasts = lookCasts;
            return $"{ms:F2} ms, {lookCasts} casts, {lookHits} hits";
        }

        private static void Resolve(Shot shot, string how)
        {
            Debug.Log($"[AimProbe] {Who(shot.unit)} {how}, after {Time.time - shot.time:F1} s", shot.unit);

            int housed = houseAimed + houseDrifted;
            double avgMs = timed > 0 ? totalMs / timed : 0;
            double avgCasts = timed > 0 ? (double)totalCasts / timed : 0;
            double avgToHouse = housed > 0 ? totalToHouse / housed : 0;
            Debug.Log($"[AimProbe] TOTAL units {releases}, steer attempts {attempts} (steered {steered}) → house on an " +
                      $"aimed leg {houseAimed}, house after drifting {houseDrifted} (avg {avgToHouse:F1} s to a house), " +
                      $"left otherwise {leftOtherwise} (despawned {leftDespawned}, tavern {leftTavern}, rescued " +
                      $"{leftRescued}), in flight {shots.Count} | per aim: avg {avgMs:F2} ms / max {maxMs:F2} ms, casts " +
                      $"avg {avgCasts:F1} / max {maxCasts} | LOOKS: no house in radius {noHouse} of {attempts}, ways " +
                      $"blocked {blocked} by {TopBlockers()}");
        }

        private static string TopBlockers()
        {
            if (blockers.Count == 0) return "-";
            var list = new List<KeyValuePair<string, int>>(blockers);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var parts = new List<string>();
            for (int i = 0; i < list.Count && i < 5; i++) parts.Add($"{list[i].Key} ×{list[i].Value}");
            return string.Join(", ", parts);
        }

        private static int IndexOf(Unit unit)
        {
            for (int i = 0; i < shots.Count; i++)
                if (shots[i].unit == unit) return i;
            return -1;
        }

        private static string Who(Unit unit) => unit != null ? $"{unit.name} ({unit.GetInstanceID()})" : "(gone)";
    }
}
