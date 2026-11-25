using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Whom the cursor's ring touches on the way from one point to another: every unit on the field whose body it
    // comes over, each once, in the order it was reached — those reached at once (all of them, when the cursor has
    // not moved) nearest first. The one answer to "who is in the ring": whom a tap pushes and whom a hold picks up
    // are the same units.
    //
    // Never through the UI: the units under a panel are out of sight.
    //
    // Static, buffers shared: there is one cursor, and no caller keeps anything here between calls.
    public static class UnitReach
    {
        private struct Reached
        {
            public Unit unit;
            public float along;   // how far along the way it was reached
            public float off;     // squared distance from where the way began: the order of those reached at once
        }

        // Whatever the units' bodies are; triggers are fields and racks, never a unit.
        private static readonly ContactFilter2D Bodies = new() { useTriggers = false };

        private static readonly List<Reached> reached = new();
        private static readonly List<RaycastHit2D> hits = new();
        private static readonly List<Collider2D> overlaps = new();

        // Into `into`, cleared first.
        public static void Collect(Vector2 from, Vector2 to, float radius, List<Unit> into)
        {
            into.Clear();
            if (InputHandler.IsPointerOverUI()) return;

            reached.Clear();
            Vector2 move = to - from;
            float length = move.magnitude;
            if (length > 0f)
            {
                // What the circle already overlaps as it starts is reported too, at distance 0.
                int count = Physics2D.CircleCast(from, radius, move / length, Bodies, hits, length);
                for (int i = 0; i < count; i++) Add(hits[i].collider, hits[i].distance, from);
            }
            else
            {
                int count = Physics2D.OverlapCircle(to, radius, Bodies, overlaps);
                for (int i = 0; i < count; i++) Add(overlaps[i], 0f, from);
            }

            reached.Sort(static (a, b) => a.along != b.along ? a.along.CompareTo(b.along) : a.off.CompareTo(b.off));
            for (int i = 0; i < reached.Count; i++) into.Add(reached[i].unit);
        }

        // The collider lives on a child ("Physics"), so resolve the Unit via GetComponentInParent.
        private static void Add(Collider2D body, float along, Vector2 from)
        {
            var unit = body.GetComponentInParent<Unit>();
            if (unit == null || !unit.IsOnField) return;
            for (int i = 0; i < reached.Count; i++)
                if (reached[i].unit == unit) return;

            reached.Add(new Reached { unit = unit, along = along, off = (unit.BodyCenter - from).sqrMagnitude });
        }
    }
}
