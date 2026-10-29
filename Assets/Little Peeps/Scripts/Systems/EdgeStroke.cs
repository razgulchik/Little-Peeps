using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // One drawn fence stroke: the player holds the button and leads the cursor along the island's grid
    // lines, and a fence segment follows from cell corner to cell corner. The path is a chain of lattice
    // nodes (IslandGrid.WorldToNode); each pair of neighbours in it is one edge to fence.
    //
    // The head moves only when the cursor comes within CaptureFraction of ANOTHER node. Between nodes is a
    // dead zone where nothing changes, so a shaking hand neither adds nor drops a segment — this is the
    // stroke's own magnet. Stepping back onto the node before the head takes the last segment off, like
    // an eraser running back over the line. A jump of more than one step (a fast flick, a diagonal) is
    // filled in one step at a time, along the axis the stroke was already going first, so a corner turns
    // the way the line was heading.
    //
    // Where it starts is the edge PRESSED — the one the cursor ghost stood on. Its two ends are equally
    // good starts until the hand moves, so the path is laid only at the first move off that edge: setting
    // off along it, the pressed edge is the first segment, whichever end the press was nearer; setting
    // off across it, the stroke starts from the nearer end and the pressed edge drops out (a vertical
    // wall begun on a horizontal ghost). Moving onto either end of the pressed edge is not a move yet —
    // released there, it is still a plain click.
    //
    // The same path serves both fence tools: PlaceTool builds along it (Collect), SellTool sells the fences
    // it runs over (CollectFences).
    //
    // Pure C# over IslandGrid, so the path rules are tested with no scene. Building, selling, cost and
    // ghosts are the tools' and PlacementVisuals'.
    public sealed class EdgeStroke
    {
        // How close to a node, as a fraction of a cell, the cursor has to come to move the head there.
        // Under half a cell, so there is a gap between any two nodes' reach — the dead zone.
        private const float CaptureFraction = 0.35f;

        private readonly List<Vector2Int> nodes = new();   // empty until the first move off the pressed edge
        private readonly HashSet<Edge> seen = new();   // Collect's scratch, kept so a frame allocates nothing

        // A stroke is under way (the button is held).
        public bool Active { get; private set; }

        // The edge under the press — what a release with no segment builds.
        public Edge PressedEdge { get; private set; }

        // Segments drawn so far. Zero right after Begin — release then is a plain click.
        public int SegmentCount => nodes.Count > 0 ? nodes.Count - 1 : 0;

        public Edge SegmentAt(int i) => Edge.Between(nodes[i], nodes[i + 1]);

        private Vector2Int Head => nodes[nodes.Count - 1];

        // The two ends of the pressed edge.
        private Vector2Int PressedStart => PressedEdge.anchor;
        private Vector2Int PressedEnd => PressedEdge.End;

        // Start on the pressed edge — the one the cursor ghost stood on (EdgeAim).
        public void Begin(Edge pressed)
        {
            nodes.Clear();
            PressedEdge = pressed;
            Active = true;
        }

        public void Clear()
        {
            nodes.Clear();
            Active = false;
        }

        // Follow the cursor. True when the path changed.
        public bool Extend(IslandGrid grid, Vector2 world)
        {
            if (!Active) return false;

            Vector2Int target = grid.WorldToNode(world);
            if ((world - grid.NodeToWorld(target)).magnitude > grid.CellSize * CaptureFraction) return false;

            bool changed = false;
            if (nodes.Count == 0)
            {
                if (target == PressedStart || target == PressedEnd) return false;   // still on the pressed edge
                LayStart(target);
                changed = true;
            }
            if (target == Head) return changed;

            bool horizontalFirst = LeadsHorizontally(target - Head);
            while (Head != target)
            {
                Vector2Int d = target - Head;
                bool horizontal = d.x != 0 && (horizontalFirst || d.y == 0);
                Step(Head + (horizontal ? new Vector2Int(System.Math.Sign(d.x), 0)
                                        : new Vector2Int(0, System.Math.Sign(d.y))));
            }
            return true;
        }

        // The first move off the pressed edge, towards `target`: start from the pressed edge's nearer end,
        // and if the walk sets off along the pressed edge's own line, take the pressed edge in first. The
        // nearer end is never ambiguous — a node is never equally far from two neighbouring nodes — and
        // from it a step along the line always leads away from the other end.
        private void LayStart(Vector2Int target)
        {
            Vector2Int a = PressedStart, b = PressedEnd;
            Vector2Int near = Manhattan(target - a) <= Manhattan(target - b) ? a : b;
            nodes.Add(near);
            if (LeadsHorizontally(target - near) == PressedEdge.horizontal) nodes.Insert(0, near == a ? b : a);
        }

        private static int Manhattan(Vector2Int v) => Mathf.Abs(v.x) + Mathf.Abs(v.y);

        // The axis a multi-step jump fills first: the one the stroke was already moving along, or for the
        // very first move, the longer leg of the jump.
        private bool LeadsHorizontally(Vector2Int jump)
        {
            if (nodes.Count >= 2) return nodes[nodes.Count - 2].y == Head.y;
            return Mathf.Abs(jump.x) >= Mathf.Abs(jump.y);
        }

        // One unit step: back onto the previous node erases the last segment, anywhere else adds one.
        private void Step(Vector2Int next)
        {
            if (nodes.Count >= 2 && nodes[nodes.Count - 2] == next) nodes.RemoveAt(nodes.Count - 1);
            else nodes.Add(next);
        }

        // The stroke as fencing work, in drawing order, into `plan` (cleared first). An edge that already
        // has a fence is left out — the stroke just runs through it — and so is an edge the path crosses a
        // second time. Each segment left is Build if the grid takes a fence there, Blocked if not (inside
        // a structure, off the island). Returns how many are Build.
        public int Collect(IslandGrid grid, List<StrokeSegment> plan)
        {
            plan.Clear();
            seen.Clear();
            int build = 0;

            for (int i = 0; i < SegmentCount; i++)
            {
                Edge edge = SegmentAt(i);
                if (!seen.Add(edge)) continue;
                if (grid.GetEdge(edge) != null) continue;

                bool ok = grid.CanPlaceEdge(edge);
                if (ok) build++;
                plan.Add(new StrokeSegment(edge, ok ? StrokeSegmentState.Build : StrokeSegmentState.Blocked));
            }
            return build;
        }

        // The fences the stroke runs over, in drawing order, each once, into `fences` (cleared first) — what
        // a sell stroke takes. Edges with no fence are passed over.
        public void CollectFences(IslandGrid grid, List<Edge> fences)
        {
            fences.Clear();
            seen.Clear();
            for (int i = 0; i < SegmentCount; i++)
            {
                Edge edge = SegmentAt(i);
                if (seen.Add(edge) && grid.GetEdge(edge) != null) fences.Add(edge);
            }
        }

        // Only the first `affordable` Build segments go up; the ones after them turn OverBudget. Run after
        // Collect, with how many the build limit and the purse allow (StructureSystem.AffordableCount).
        public static void CapBudget(List<StrokeSegment> plan, int affordable)
        {
            for (int i = 0; i < plan.Count; i++)
            {
                if (plan[i].State != StrokeSegmentState.Build) continue;
                if (affordable > 0) affordable--;
                else plan[i] = new StrokeSegment(plan[i].Edge, StrokeSegmentState.OverBudget);
            }
        }
    }

    // What becomes of one segment of a stroke on release.
    public enum StrokeSegmentState
    {
        Build,       // goes up
        Blocked,     // the grid refuses this edge (inside a structure, off the island)
        OverBudget,  // placeable, but past the build limit or what the player can pay
    }

    public readonly struct StrokeSegment
    {
        public readonly Edge Edge;
        public readonly StrokeSegmentState State;

        public StrokeSegment(Edge edge, StrokeSegmentState state)
        {
            Edge = edge;
            State = state;
        }
    }
}
