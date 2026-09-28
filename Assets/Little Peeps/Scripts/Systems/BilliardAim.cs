using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Aims a unit like a billiard ball: which way to send it out of a structure so that it walks into one of
    // the given houses — straight, or banked off the island's coast up to maxBounces times.
    //
    // Only the coast may be banked off. It is the one obstacle that never changes during play: trees fall and
    // regrow, animals walk, and a house with a free slot would swallow the unit on contact. So anything but
    // the coast spoils the shot — a tree, a building, a fence, a house that is not a candidate. Other units
    // are never in the way (units pass through each other) and animals are ignored: they move, and one that
    // walks into the shot knocks the unit off it; it then carries on as any tired drunk does and finds a
    // house on its own.
    //
    // The prediction is exact because the bounce is: friction 0 and bounciness 1 mirror it, Unit holds the
    // speed, the unit's own correction is the shared Unit.BendAwayFromNormal, and the random turn stays off
    // for the first Unit.ExactBounces — which caps maxBounces. What is left: a bounce so shallow that the
    // solver slides instead (under Physics2D.bounceThreshold — such shots are thrown out), the contact
    // offset, the corners of a stepped coast, the fixed step. So of all the directions that work, the one
    // taken is the middle of the widest run of them (PickWindow): the shot with the most room for error.
    //
    // Casts the unit's body circle against exactly what the unit collides with (its layer's collision mask,
    // no triggers). No allocations after the first use: the buffers live here, one aim per owner. The trace
    // (Trace) starts from any point and direction, so an autopilot re-aiming at each bounce can reuse it.
    public sealed class BilliardAim
    {
        public const int Steps = 360;                  // directions tried from a structure, one per degree
        private const float StepDegrees = 360f / Steps;
        private const float MaxLeg = 200f;             // longer than any island: every leg ends on the coast
        private const float Skin = 0.02f;              // off the wall after a bounce, so the next cast starts clear
        private const float BounceMargin = 1.5f;       // × Physics2D.bounceThreshold: slower into the wall = a slide

        private readonly int[] houseAt = new int[Steps];
        private readonly int[] bouncesAt = new int[Steps];
        private readonly List<RaycastHit2D> hits = new();
        private readonly List<int> reachable = new();

        // The current aim's context, set by TryAimFromStructure.
        private IReadOnlyList<Spawner> houses;
        private GameObject coast;
        private Transform self;
        private ContactFilter2D filter;
        private float radius, speed, minNormalSpeed, minBounceAngle;
        private int maxBounces;

        // Aim `unit` out of `structure` at one of `houses`. Every direction (one per degree) that leaves
        // through an open side is traced; of the houses some direction reaches, one is picked at random, and
        // of its directions the fewest bounces, then the middle of the widest run. `bodyStart` is where the
        // unit's BODY centre goes (subtract Unit.BodyOffset for the transform), `dir` the way it walks.
        // False when no direction reaches any of the houses. `self` is the structure's root: its own
        // colliders are ignored on the first leg, which starts against its wall.
        public bool TryAimFromStructure(Unit unit, IslandGrid grid, StructureInstance structure, Transform self,
                                        GameObject coast, IReadOnlyList<Spawner> houses, int maxBounces,
                                        float gap, out Vector2 bodyStart, out Vector2 dir)
        {
            bodyStart = dir = Vector2.zero;
            if (unit == null || grid == null || structure == null || structure.Def == null) return false;
            if (coast == null || houses == null || houses.Count == 0) return false;

            Prepare(unit, self, coast, houses, maxBounces);

            var footprint = structure.Def.Footprint;
            for (int a = 0; a < Steps; a++)
            {
                houseAt[a] = -1;
                bouncesAt[a] = 0;

                Vector2 d = Direction(a);
                Vector2 start = StructureExits.ExitPoint(grid, structure.Cell, footprint, d, radius + gap);
                if (!StructureExits.IsOpenExit(grid, structure.Cell, footprint, structure.Def.border, grid.WorldToGrid(start)))
                    continue;

                houseAt[a] = Trace(start, d, true, out bouncesAt[a]);
            }

            reachable.Clear();
            for (int a = 0; a < Steps; a++)
                if (houseAt[a] >= 0 && !reachable.Contains(houseAt[a])) reachable.Add(houseAt[a]);
            if (reachable.Count == 0) return false;

            int house = reachable[Random.Range(0, reachable.Count)];
            if (!PickWindow(houseAt, bouncesAt, house, out int best)) return false;

            dir = Direction(best);
            bodyStart = StructureExits.ExitPoint(grid, structure.Cell, footprint, dir, radius + gap);
            return true;
        }

        private void Prepare(Unit unit, Transform self, GameObject coast, IReadOnlyList<Spawner> houses, int maxBounces)
        {
            this.houses = houses;
            this.coast = coast;
            this.self = self;
            this.maxBounces = Mathf.Clamp(maxBounces, 0, unit.ExactBounces);

            radius = unit.BodyRadius;
            speed = unit.CruiseSpeed;
            minBounceAngle = unit.MinBounceAngle;
            minNormalSpeed = Physics2D.bounceThreshold * BounceMargin;

            filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(unit.BodyLayer));
        }

        // Follow one shot of the body circle from `origin` along `dir`. Returns the index in `houses` of the
        // house it walks into, or -1: stopped by anything else, one bounce too many, or a bounce too shallow
        // to be one. `bounces` = coast bounces on the way. `ignoreSelf` drops the structure's own colliders
        // on the first leg.
        private int Trace(Vector2 origin, Vector2 dir, bool ignoreSelf, out int bounces)
        {
            bounces = 0;
            for (bool firstLeg = true; ; firstLeg = false)
            {
                if (!NearestHit(origin, dir, ignoreSelf && firstLeg, out RaycastHit2D hit)) return -1;

                if (hit.collider.gameObject != coast) return IndexOfHouse(hit.collider);
                if (bounces == maxBounces) return -1;

                Vector2 n = hit.normal;
                if (Vector2.Dot(n, dir) > 0f) n = -n;                               // facing the unit
                if (-Vector2.Dot(dir, n) * speed < minNormalSpeed) return -1;      // would slide, not bounce

                dir = Unit.BendAwayFromNormal(Vector2.Reflect(dir, n), n, minBounceAngle, 1f);
                origin = hit.centroid + n * Skin;
                bounces++;
            }
        }

        // The closest thing the circle runs into that counts. Hits are not assumed to come sorted.
        private bool NearestHit(Vector2 origin, Vector2 dir, bool ignoreSelf, out RaycastHit2D nearest)
        {
            nearest = default;
            float best = float.PositiveInfinity;

            int count = Physics2D.CircleCast(origin, radius, dir, filter, hits, MaxLeg);
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                if (h.distance >= best || IsIgnored(h.collider, ignoreSelf)) continue;
                best = h.distance;
                nearest = h;
            }
            return best < float.PositiveInfinity;
        }

        private bool IsIgnored(Collider2D c, bool ignoreSelf)
            => c.GetComponentInParent<Animal>() != null
            || (ignoreSelf && self != null && c.transform.IsChildOf(self));

        private int IndexOfHouse(Collider2D c)
        {
            var house = c.GetComponentInParent<Spawner>();
            if (house == null) return -1;
            for (int i = 0; i < houses.Count; i++)
                if (houses[i] == house) return i;
            return -1;
        }

        private static Vector2 Direction(int step)
        {
            float r = step * StepDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        // Of the directions that reach `house`, keep those with the fewest bounces; of those, return the
        // middle of the longest unbroken run — the ring wraps, the last direction and the first are
        // neighbours. The middle of the widest run is the shot with the most room for error either way.
        // False if no direction reaches the house.
        public static bool PickWindow(int[] houseAt, int[] bouncesAt, int house, out int index)
        {
            int n = houseAt.Length;
            index = -1;

            int fewest = int.MaxValue;
            for (int i = 0; i < n; i++)
                if (houseAt[i] == house && bouncesAt[i] < fewest) fewest = bouncesAt[i];
            if (fewest == int.MaxValue) return false;

            // Scan from just past a direction outside the window, so the wrap never splits a run. Every
            // direction in it (a lone structure in open ground) — any will do.
            int start = -1;
            for (int i = 0; i < n; i++)
                if (!InWindow(i)) { start = i + 1; break; }
            if (start < 0) { index = 0; return true; }

            int bestFrom = 0, bestLen = 0, runFrom = 0, runLen = 0;
            for (int k = 0; k < n; k++)
            {
                int i = (start + k) % n;
                if (!InWindow(i)) { runLen = 0; continue; }

                if (runLen == 0) runFrom = i;
                runLen++;
                if (runLen > bestLen) { bestLen = runLen; bestFrom = runFrom; }
            }

            index = (bestFrom + (bestLen - 1) / 2) % n;
            return true;

            bool InWindow(int i) => houseAt[i] == house && bouncesAt[i] == fewest;
        }
    }
}
