using System;
using UnityEngine;

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

    public bool Equals(Edge other) => anchor == other.anchor && horizontal == other.horizontal;
    public override bool Equals(object obj) => obj is Edge other && Equals(other);
    public override int GetHashCode() => (anchor.GetHashCode() * 397) ^ (horizontal ? 1 : 0);
    public override string ToString() => $"Edge({anchor}, {(horizontal ? "H" : "V")})";
}
