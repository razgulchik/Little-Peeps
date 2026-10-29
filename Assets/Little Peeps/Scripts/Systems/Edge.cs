using System;
using UnityEngine;

namespace LittlePeeps
{
    // Canonical identity for a grid EDGE — the boundary line between two cells — so fences can be placed
    // "on the ribs" of the island grid instead of inside a cell. Anchored to the SAME integer lattice as
    // cell corners, so cells and edges share one coordinate space with no offset (lattice point (x,y) is
    // at world (x*cs, y*cs); cell (x,y) is the square to its north-east).
    //
    //  - Horizontal edge (anchor a): segment a -> (a.x+1, a.y); borders cells (a.x, a.y) above and
    //    (a.x, a.y-1) below; world midpoint ((a.x+0.5)*cs, a.y*cs).
    //  - Vertical edge   (anchor a): segment a -> (a.x, a.y+1); borders cells (a.x, a.y) right and
    //    (a.x-1, a.y) left; world midpoint (a.x*cs, (a.y+0.5)*cs).
    //
    // Each physical edge has exactly ONE (anchor, horizontal) form, so an Edge is a safe dictionary key.
    public readonly struct Edge : IEquatable<Edge>
    {
        public readonly Vector2Int anchor;
        public readonly bool horizontal;

        public Edge(Vector2Int anchor, bool horizontal)
        {
            this.anchor = anchor;
            this.horizontal = horizontal;
        }

        // The far end of the segment: one node east of the anchor, or one north. The near end is the anchor.
        public Vector2Int End => anchor + (horizontal ? Vector2Int.right : Vector2Int.up);

        // The edge joining two NEIGHBOURING lattice nodes (cell corners, IslandGrid.WorldToNode), in
        // either order. Anything but a unit step is a caller bug — there is no single edge between them.
        public static Edge Between(Vector2Int a, Vector2Int b)
        {
            if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) != 1)
                throw new ArgumentException($"Nodes {a} and {b} are not neighbours.");
            return a.y == b.y
                ? new Edge(new Vector2Int(Mathf.Min(a.x, b.x), a.y), true)
                : new Edge(new Vector2Int(a.x, Mathf.Min(a.y, b.y)), false);
        }

        public bool Equals(Edge other) => anchor == other.anchor && horizontal == other.horizontal;
        public override bool Equals(object obj) => obj is Edge other && Equals(other);
        public override int GetHashCode() => (anchor.GetHashCode() * 397) ^ (horizontal ? 1 : 0);
        public override string ToString() => $"Edge({anchor}, {(horizontal ? "H" : "V")})";
    }
}
