using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // A water cell with both a band and a column of the outline (land to the north and to one side) gets one
    // wave piece of its own instead of the two laid over each other — where the band's foam meets the wall's,
    // two stacked pieces cross each other's lines. Three shapes, named after where they come up; each also
    // comes mirrored, for the other side.
    public enum CoastWaveCorner
    {
        None,
        Bay,    // under the band's middle, beside a wall: a bay's corner, or a step down the coast
        Step,   // under the band's rounded end, beside a wall that carries on down: a one-cell step
        Notch,  // under the band's rounded end, beside the top of a column: two land cells meeting at a corner
    }

    // The waves breaking along the island's coast: the artist's frames of the foam, and how long each shows.
    // They are drawn as a third layer of the coastline outline, under the island (IslandTilePainter): the trim's
    // own pieces on the trim's own water cells, so the waves follow every bay and staircase the outline does,
    // and there are none along the north coast, which has no outline.
    //
    // A frame is the artist's picture of the LEFT edge of an island two cells high, sliced into the cells that
    // carry foam: down the side, the column's top, middle and closing stub; along the bottom, the band's rounded
    // end and its middle. The right edge is the left one mirrored — the trim's right pieces are its left ones
    // mirrored to the pixel — and the band's middle reads the same both ways, so these five are every piece
    // there is. One set for every terrain: the foam is the sea's, not the land's.
    //
    // A frame's inner corners (CoastWaveCorner) are drawn for the left side too: land to the north and to the
    // east, the band's foam and the wall's meeting in one cell. Optional, all frames or none: without one a
    // corner cell shows its band's and its column's waves stacked.
    [CreateAssetMenu(menuName = "LittlePeeps/CoastWaveSet")]
    public class CoastWaveSet : ScriptableObject
    {
        // One frame's art. Every slot must be filled, an empty-looking one too (a frame where the foam has
        // drawn back from the side): a frame change swaps each piece's tile for the next frame's, so a piece
        // with no art on one frame would leave its cells and never come back.
        [Serializable]
        public class Frame
        {
            [Tooltip("Beside land to the east whose corner above is rounded: the top of the column.")]
            public Sprite leftEdgeTop;
            [Tooltip("Beside land to the east: the column's full height. Also a bay's right wall.")]
            public Sprite leftEdgeMid;
            [Tooltip("Under the land's bottom-left corner, beside nothing: the stub closing the column.")]
            public Sprite leftEdgeBottom;
            [Tooltip("Under land to the north, where the band's left end is rounded off.")]
            public Sprite bottomEdgeLeft;
            [Tooltip("Under land to the north: the band's middle.")]
            public Sprite bottomEdgeMid;

            [Header("Inner corners (optional: every frame or none)")]
            [Tooltip("Under the band's middle, beside land to the east: a bay's corner.")]
            public Sprite cornerBay;
            [Tooltip("Under the band's rounded left end, beside land to the east: a one-cell step down the coast.")]
            public Sprite cornerStep;
            [Tooltip("Under the band's rounded right end, beside the top of a column to the east: two land cells " +
                     "meeting only at a corner.")]
            public Sprite cornerNotch;

            public Sprite Of(CoastWaveCorner corner)
            {
                switch (corner)
                {
                    case CoastWaveCorner.Bay:   return cornerBay;
                    case CoastWaveCorner.Step:  return cornerStep;
                    case CoastWaveCorner.Notch: return cornerNotch;
                    default:                    return null;
                }
            }

            // The art of `piece` in this frame, and whether it is drawn mirrored: the right edge's pieces are
            // the left edge's turned over. Null for None.
            public Sprite Of(IslandTrimPiece piece, out bool mirrored)
            {
                mirrored = piece == IslandTrimPiece.RightEdgeTop || piece == IslandTrimPiece.RightEdgeMid ||
                           piece == IslandTrimPiece.RightEdgeBottom || piece == IslandTrimPiece.BottomEdgeRight;
                switch (piece)
                {
                    case IslandTrimPiece.LeftEdgeTop:
                    case IslandTrimPiece.RightEdgeTop:     return leftEdgeTop;
                    case IslandTrimPiece.LeftEdgeMid:
                    case IslandTrimPiece.RightEdgeMid:     return leftEdgeMid;
                    case IslandTrimPiece.LeftEdgeBottom:
                    case IslandTrimPiece.RightEdgeBottom:  return leftEdgeBottom;
                    case IslandTrimPiece.BottomEdgeLeft:
                    case IslandTrimPiece.BottomEdgeRight:  return bottomEdgeLeft;
                    case IslandTrimPiece.BottomEdgeMid:    return bottomEdgeMid;
                    default:                               return null;
                }
            }
        }

        [Serializable]
        public struct Step
        {
            [Tooltip("Which frame shows: its place in the Frames list, 0 = the first.")]
            [Min(0)] public int frame;
            [Tooltip("For how long, in real seconds.")]
            [Min(0f)] public float seconds;

            public Step(int frame, float seconds)
            {
                this.frame = frame;
                this.seconds = seconds;
            }
        }

        [Tooltip("The artist's frames, each sliced into the five cells of a left edge that carry foam.")]
        public Frame[] frames = Array.Empty<Frame>();

        [Tooltip("The loop the frames play in, one step after another and round again, in real time: the waves " +
                 "keep breaking through every pause. The whole coast shows the same step at once.")]
        public Step[] steps =
        {
            new(0, 0.5f), new(1, 0.2f), new(2, 0.2f), new(3, 0.2f), new(2, 0.2f), new(1, 0.2f),
        };

        public int FrameAt(double seconds) => FrameAt(steps, seconds);

        // Which inner corner a water cell with these two outline pieces is, and whether it is drawn mirrored (a
        // wall to the west). None for a cell with one piece or none — and for the one pair left over, a band
        // rounded at its left end over the top of a column, which only a strip one cell wide would put there.
        public static CoastWaveCorner CornerOf(IslandTrimPiece horizontal, IslandTrimPiece vertical, out bool mirrored)
        {
            mirrored = false;
            switch (horizontal, vertical)
            {
                case (IslandTrimPiece.BottomEdgeMid, IslandTrimPiece.LeftEdgeMid):    return CoastWaveCorner.Bay;
                case (IslandTrimPiece.BottomEdgeLeft, IslandTrimPiece.LeftEdgeMid):   return CoastWaveCorner.Step;
                case (IslandTrimPiece.BottomEdgeRight, IslandTrimPiece.LeftEdgeTop):  return CoastWaveCorner.Notch;
                case (IslandTrimPiece.BottomEdgeMid, IslandTrimPiece.RightEdgeMid):   mirrored = true; return CoastWaveCorner.Bay;
                case (IslandTrimPiece.BottomEdgeRight, IslandTrimPiece.RightEdgeMid): mirrored = true; return CoastWaveCorner.Step;
                case (IslandTrimPiece.BottomEdgeLeft, IslandTrimPiece.RightEdgeTop):  mirrored = true; return CoastWaveCorner.Notch;
                default:                                                              return CoastWaveCorner.None;
            }
        }

        // The frame showing `seconds` into the loop. A step of no length never shows; with no steps at all, or
        // none that lasts, the first one's frame stands still (0 without any).
        public static int FrameAt(IReadOnlyList<Step> steps, double seconds)
        {
            if (steps == null || steps.Count == 0) return 0;

            double loop = 0;
            foreach (var step in steps) loop += Math.Max(0f, step.seconds);
            if (loop <= 0) return steps[0].frame;

            double into = seconds % loop;
            if (into < 0) into += loop;   // C#'s % keeps the sign of a negative time
            foreach (var step in steps)
            {
                into -= Math.Max(0f, step.seconds);
                if (into < 0) return step.frame;
            }
            return steps[steps.Count - 1].frame;   // rounding at the loop's very end
        }
    }
}
