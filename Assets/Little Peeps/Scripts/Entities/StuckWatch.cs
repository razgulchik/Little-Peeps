using UnityEngine;

namespace LittlePeeps
{
    // Tells a unit that is STUCK from one that is merely bouncing, by the one thing the two differ in:
    // whether it gets anywhere. Time is cut into fixed windows of `window` seconds; at the start of
    // each the current position becomes the anchor, and the window records how far from it the unit
    // ever got. A window that ends with the unit never further than `radius` from its anchor means it
    // spent the whole window inside a disc a free unit crosses in a fraction of a second — pinned
    // inside a regrown tree, wedged between colliders, sealed into a pocket. Bouncing alone never
    // trips it: a unit that leaves the disc at any point in the window passes, however many walls it
    // hit on the way.
    //
    // Windows are fixed rather than sliding so this costs three fields and one distance per step,
    // nothing to allocate or scan. The price is latency: the verdict lands between `window` and
    // 2·`window` after the unit actually stopped getting anywhere — it can get stuck mid-window, and
    // that window still holds its free travel and passes; the next one fails.
    //
    // A verdict holds until the unit proves it wrong: it clears the moment the unit is out of the disc
    // again (a boar shoved it free), or when a later window passes. The struct has no dependency
    // beyond Vector2, so the rule is testable offline.
    public struct StuckWatch
    {
        private Vector2 anchor;
        private float maxDistanceSq;   // furthest the unit got from the anchor this window
        private float elapsed;         // seconds into the current window
        private bool armed;            // false until the first Tick (or Reset) sets the anchor

        // The verdict of the last completed window, cleared early if the unit gets out of the disc.
        public bool IsStuck { get; private set; }

        // Start a fresh window at `position` with the verdict cleared. For when the unit is PLACED
        // somewhere — launched from a house, taken off the field to rest — so the move itself never
        // reads as travel, and a rescued unit doesn't carry its verdict into the house.
        public void Reset(Vector2 position)
        {
            anchor = position;
            maxDistanceSq = 0f;
            elapsed = 0f;
            armed = true;
            IsStuck = false;
        }

        // One physics step at `position`. Returns IsStuck as of after the step.
        public bool Tick(Vector2 position, float dt, float radius, float window)
        {
            if (!armed)
            {
                Reset(position);
                return false;
            }

            float radiusSq = radius * radius;
            float distanceSq = (position - anchor).sqrMagnitude;
            if (distanceSq > maxDistanceSq) maxDistanceSq = distanceSq;
            if (distanceSq > radiusSq) IsStuck = false;   // out of the disc: free, whatever the last window said

            elapsed += dt;
            if (elapsed < window) return IsStuck;

            IsStuck = maxDistanceSq <= radiusSq;
            anchor = position;
            maxDistanceSq = 0f;
            elapsed = 0f;
            return IsStuck;
        }
    }
}
