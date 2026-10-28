using UnityEngine;

namespace LittlePeeps
{
    // The island's edge as a bumper: a unit that bounces off the coast sends a ripple over the water from where
    // it hit (BumpVfxSystem draws it from CoastBumpedEvent). The coast is not a CollisionTarget — nothing there
    // pays, lets a unit in or shakes, and SoundSystem tells the coast's bounce by exactly that — so it gets this
    // small listener of its own, on the Ground object: its composite outline collider IS the coast, and its
    // Rigidbody2D is there too, so the collision callbacks fire here.
    //
    // The collider runs along the edge of the land cells, but the art draws the coast's outline OUTSIDE them, on
    // the water cells (IslandTilePainter): a band under the island's south side, a column beside its east and
    // west sides, nothing on the north. The ripple starts where the water does, so the hit point is stepped that
    // far out, per side. The ripple's prefab sorts under the island's tiles, so the land hides the half of the
    // ring that would lie on it.
    //
    // The coast is ONE object round the whole island, so a cooldown per object would mute every ripple but one;
    // the throttle is per PLACE instead — a ripple near one shown a moment ago is skipped.
    public class CoastBump : MonoBehaviour
    {
        [Header("Waterline — art pixels between the land's edge and the water")]
        [Tooltip("Under the island's south side: the height of the outline's band.")]
        [SerializeField, Min(0)] private int southOutline = 4;

        [Tooltip("Beside its east and west sides: the width of the outline's columns.")]
        [SerializeField, Min(0)] private int sideOutline = 1;

        [Tooltip("Above its north side: the art draws no outline there.")]
        [SerializeField, Min(0)] private int northOutline;

        [Tooltip("Art pixels per world unit.")]
        [SerializeField, Min(1)] private int pixelsPerUnit = 16;

        [Header("Throttle")]
        [Tooltip("A ripple closer than this (world units) to one shown less than Min Interval ago is skipped.")]
        [SerializeField, Min(0f)] private float minDistance = 0.75f;

        [Tooltip("Seconds a shown ripple keeps its place to itself.")]
        [SerializeField, Min(0f)] private float minInterval = 0.25f;

        private readonly RecentPlaces recent = new(8);

        private void OnCollisionEnter2D(Collision2D collision)
        {
            // Unity sends collision messages to disabled behaviours too; unticked here means off.
            if (!isActiveAndEnabled || collision.contactCount == 0) return;
            if (!collision.gameObject.TryGetComponent<Unit>(out _)) return;

            // collision.collider is the unit's, and the unit is on the land: from the contact toward its body
            // points inland, so the water is the other way.
            var contact = collision.GetContact(0);
            Vector2Int toWater = -CollisionTarget.FaceOf(contact.normal,
                                                         (Vector2)collision.collider.bounds.center - contact.point);
            Vector2 at = Waterline(contact.point, toWater, southOutline, sideOutline, northOutline, pixelsPerUnit);

            if (!recent.TryClaim(at, Time.time, minDistance, minInterval)) return;
            EventBus<CoastBumpedEvent>.Publish(new CoastBumpedEvent { Position = at });
        }

        // Where the water starts past a hit on the coast, `toWater` being the side the water is on.
        public static Vector2 Waterline(Vector2 hit, Vector2Int toWater, int south, int side, int north, int pixelsPerUnit)
        {
            int pixels = toWater.y < 0 ? south : toWater.y > 0 ? north : side;
            return hit + (Vector2)toWater * (pixels / (float)pixelsPerUnit);
        }

        // The last few places that showed something — a throttle per place rather than per object.
        public sealed class RecentPlaces
        {
            private readonly Vector2[] places;
            private readonly float[] times;
            private int next;

            public RecentPlaces(int capacity)
            {
                places = new Vector2[capacity];
                times = new float[capacity];
                for (int i = 0; i < capacity; i++) times[i] = float.NegativeInfinity;
            }

            // True — and the place is remembered — unless one within `distance` was claimed less than `interval` ago.
            public bool TryClaim(Vector2 at, float now, float distance, float interval)
            {
                float reach = distance * distance;
                for (int i = 0; i < places.Length; i++)
                    if (now - times[i] < interval && (places[i] - at).sqrMagnitude < reach) return false;

                places[next] = at;
                times[next] = now;
                next = (next + 1) % places.Length;
                return true;
            }
        }
    }
}
