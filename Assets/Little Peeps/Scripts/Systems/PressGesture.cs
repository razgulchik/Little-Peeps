using System;
using UnityEngine;

namespace LittlePeeps
{
    // When a press of the left button is a hold and no longer a tap (PressGesture). Numbers to tune by hand.
    [Serializable]
    public sealed class PressTiming
    {
        [Tooltip("Seconds. A press let go sooner than this is a tap however far the cursor went meanwhile: a click " +
                 "at a running unit is often made on the move.")]
        [Min(0f)] public float tapUnder = 0.25f;

        [Tooltip("Seconds. A press held this long is a hold even if the cursor stands still. Until then a slow " +
                 "click is still a tap.")]
        [Min(0f)] public float holdFrom = 0.45f;

        [Tooltip("Screen pixels. Between the two times a press is a hold once the cursor has gone this far from " +
                 "where the button went down.")]
        [Min(0f)] public float dragPixels = 24f;
    }

    // One press of the left button, and the question it asks: a tap or a hold? Nobody can tell while the button
    // goes down, so a press starts UNDECIDED and turns into a hold the moment it can be nothing else — once, and
    // for good. Let go before that, it was a tap.
    //
    // It takes both the time and the travel to tell, because neither is enough alone (the numbers: PressTiming):
    //   - a click lasts a tenth of a second or two, and nobody picks a unit up, takes it somewhere and lets go in
    //     that: shorter than tapUnder is a tap, wherever the cursor went meanwhile;
    //   - past that the travel does tell: a click stands about still, a cursor that has gone dragPixels is taking
    //     something along. The farthest it has been counts, so coming back to the start is still a drag;
    //   - and one that stands still is given until holdFrom: a slow click, not a hold yet.
    //
    // Pure C#: it is fed the time and the cursor, so it is tested with no scene and no mouse.
    public sealed class PressGesture
    {
        // The most one step may count for. A hitch between two frames must not turn a click into a hold: the button
        // may well have come up during it, and that is only told after.
        private const float MaxStep = 0.05f;

        private Vector2 from;
        private float heldFor;
        private float travelled;   // the farthest the cursor has been from `from`

        // The button is down...
        public bool IsDown { get; private set; }

        // ...and it is a hold.
        public bool IsHold { get; private set; }

        // Down, and not told apart yet.
        public bool IsUndecided => IsDown && !IsHold;

        // The button went down with the cursor at `screen` (pixels).
        public void Begin(Vector2 screen)
        {
            from = screen;
            heldFor = 0f;
            travelled = 0f;
            IsDown = true;
            IsHold = false;
        }

        // One frame of it. True on the one it turns into a hold.
        public bool Advance(PressTiming timing, float deltaTime, Vector2 screen)
        {
            if (!IsUndecided) return false;

            heldFor += Mathf.Clamp(deltaTime, 0f, MaxStep);
            travelled = Mathf.Max(travelled, (screen - from).magnitude);

            IsHold = heldFor >= timing.holdFrom
                  || (heldFor >= timing.tapUnder && travelled >= timing.dragPixels);
            return IsHold;
        }

        // The button is up, or nobody asks any more.
        public void End()
        {
            IsDown = false;
            IsHold = false;
        }
    }
}
