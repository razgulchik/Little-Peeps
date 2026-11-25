using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // A wall that has come up where units stand: the ones inside walk out through it, and nobody gets in.
    // A construction site is one, and so is the river the moment the bridge over it is sold (WaterBed).
    //
    // "Lets out, never lets in" is done by naming the exceptions, not by guessing directions: every insider
    // gets Physics2D.IgnoreCollision against the wall, and gets the wall back the moment its body has fully
    // left the area. Who is inside is plain geometry (ConstructionArea) over the live units, not a physics
    // query.
    //
    // A unit that leaves the field while it is let through (it goes home in a puff, it is carried away) has no
    // body in the physics to give the wall back to. It stays on the books until it has one again — a wall that
    // outlives it would otherwise be open to that unit for good — but is no longer inside.
    //
    // Plain C#: its owner calls it on physics time.
    public class OneWayWall
    {
        // How far past the area's edge a body must be before the wall closes behind it, so a unit that has
        // just slipped out is not caught by the wall it is still brushing.
        private const float ExitMargin = 0.05f;

        private readonly Collider2D wall;
        private readonly IReadOnlyList<Rect> area;
        private readonly SpawnSystem spawns;
        private readonly List<Unit> letThrough = new();

        public OneWayWall(Collider2D wall, IReadOnlyList<Rect> area, SpawnSystem spawns)
        {
            this.wall = wall;
            this.area = area;
            this.spawns = spawns;
        }

        // Nobody is on the books: the wall is whole, and there is nothing left to wait for.
        public bool IsWhole => letThrough.Count == 0;

        // Every live unit whose body touches the area is let through from now on. Safe to repeat.
        public void AdoptUnitsInside()
        {
            if (spawns == null || wall == null) return;

            var units = spawns.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null || !unit.IsOnField || unit.Body == null) continue;
                if (!ConstructionArea.Overlaps(area, unit.BodyCenter, unit.BodyRadius)) continue;

                Physics2D.IgnoreCollision(unit.Body, wall, true);
                if (!letThrough.Contains(unit)) letThrough.Add(unit);
            }
        }

        // Give the wall back to everyone who is out by now. True while somebody is still inside.
        public bool LetOut()
        {
            bool inside = false;
            for (int i = letThrough.Count - 1; i >= 0; i--)
            {
                var unit = letThrough[i];
                if (unit != null)
                {
                    if (unit.Body == null || !unit.Body.isActiveAndEnabled) continue;
                    if (unit.IsOnField && ConstructionArea.Overlaps(area, unit.BodyCenter, unit.BodyRadius + ExitMargin))
                    {
                        inside = true;
                        continue;
                    }
                    Physics2D.IgnoreCollision(unit.Body, wall, false);   // out: from now on it is a wall to this unit too
                }
                letThrough.RemoveAt(i);
            }
            return inside;
        }
    }
}
