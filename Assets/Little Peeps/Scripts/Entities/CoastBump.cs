using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
    //
    // The coast also gives under the hit, as the squash of a structure does (BumpView): a small smooth bulge
    // toward the water that springs back, firm rather than soft. It is the land's own sheet moving (CoastBounce,
    // on the Ground tilemap's grid): the corners on the coast's grid line near the hit are pushed out, fading
    // smoothly with the distance along the coast, and the tiles stretch between them. Each hit is a spring of
    // its own — LeanView's, solved exactly per frame — and hits close together add up to no more than the peak.
    // Every corner moves in whole art pixels. Only the picture moves: the collider and whatever stands on the
    // land stay put.
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

        [Header("Bulge — the coast gives under the hit and springs back")]
        [Tooltip("How far the coast bulges out at the hit at the most, in art pixels. 0 = it does not.")]
        [SerializeField, Min(0)] private int bulgePixels = 2;

        [Tooltip("How far along the coast the bulge reaches on each side of the hit, in cells; it eases out to " +
                 "nothing there.")]
        [SerializeField, Min(0.5f)] private float bulgeReach = 2f;

        [Tooltip("Swings per second: high = a firm coast that snaps back.")]
        [SerializeField, Range(1f, 20f)] private float bulgeFrequency = 8f;

        [Tooltip("How fast the swing dies down: small = it wobbles back and forth, 1 = it bulges and settles " +
                 "back without swinging past.")]
        [SerializeField, Range(0.05f, 1f)] private float bulgeDamping = 0.35f;

        // Settled: closer to rest than this, and slower than this per second, in peaks.
        private const float Rest = 0.003f;

        // One hit's bulge: where it was (world), which side the water is on, and its spring, in peaks.
        private struct Bulge
        {
            public Vector2 At;
            public Vector2Int ToWater;
            public Vector2 Bend, Swing;
        }

        private readonly RecentPlaces recent = new(8);
        private readonly List<Bulge> bulges = new();
        private readonly Dictionary<Vector2Int, Vector2> pushes = new();   // per corner, in peaks; reused
        private readonly CoastBounce bounce = new();
        private Tilemap land;
        private BoundsInt landBounds;   // what the bounce was begun over

        private void Awake()
        {
            land = GetComponent<Tilemap>();
            if (land == null)
                Debug.LogWarning("CoastBump: no Tilemap on this object — the coast will not bulge.", this);
        }

        private void OnDisable()
        {
            bulges.Clear();
            bounce.End();
        }

        // Scaled time: a paused game holds the bulge where it is.
        private void Update()
        {
            if (bulges.Count == 0) return;

            float omega = 2f * Mathf.PI * bulgeFrequency;
            for (int i = bulges.Count - 1; i >= 0; i--)
            {
                var bulge = bulges[i];
                LeanView.Step(ref bulge.Bend, ref bulge.Swing, omega, bulgeDamping, Time.deltaTime);
                if (bulge.Bend.sqrMagnitude < Rest * Rest && bulge.Swing.sqrMagnitude < Rest * Rest * 100f)
                    bulges.RemoveAt(i);
                else
                    bulges[i] = bulge;
            }

            Draw();   // with none left, this flattens the coast and switches the layer off
        }

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
            StartBulge(contact.point, toWater);
        }

        // A kick from rest that peaks at exactly one full bulge, toward the water.
        private void StartBulge(Vector2 hit, Vector2Int toWater)
        {
            if (bulgePixels == 0 || land == null) return;

            // The island grows between hits: begun again over what the land covers now. The bulges keep their
            // place in the world, so the ones still moving carry on.
            if (!bounce.IsReady || land.cellBounds != landBounds)
            {
                landBounds = land.cellBounds;
                bounce.Begin(land, (Vector2Int)landBounds.min, (Vector2Int)landBounds.max - Vector2Int.one);
            }

            bulges.Add(new Bulge
            {
                At = hit,
                ToWater = toWater,
                Swing = (Vector2)toWater * LeanView.KickFor(bulgeFrequency, bulgeDamping)
            });
        }

        private void Draw()
        {
            pushes.Clear();
            foreach (var bulge in bulges)
                Spread(bounce.CornerOf(bulge.At), bulge.ToWater, bulge.Bend, bulgeReach, pushes);

            float peak = bulgePixels / (float)pixelsPerUnit;
            bounce.Clear();
            foreach (var push in pushes)
                bounce.Set(push.Key, Snap(Vector2.ClampMagnitude(push.Value, 1f) * peak, pixelsPerUnit));
            bounce.Apply();
        }

        // Adds `push` to every corner on the grid line the hit is on, within `reach` corners of it along the coast,
        // eased out with the distance. `at` is the hit in corners (CoastBounce.CornerOf: whole numbers are
        // corners); the coast runs along a grid line, so the hit is on one — up to the contact's slop.
        public static void Spread(Vector2 at, Vector2Int toWater, Vector2 push, float reach,
                                  Dictionary<Vector2Int, Vector2> into)
        {
            bool upright = toWater.x != 0;   // an east or west coast: its corners are a column
            float along = upright ? at.y : at.x;
            int line = Mathf.RoundToInt(upright ? at.x : at.y);

            for (int i = Mathf.CeilToInt(along - reach); i <= Mathf.FloorToInt(along + reach); i++)
            {
                float weight = Falloff(Mathf.Abs(i - along) / reach);
                if (weight <= 0f) continue;
                var corner = upright ? new Vector2Int(line, i) : new Vector2Int(i, line);
                into.TryGetValue(corner, out var sum);
                into[corner] = sum + push * weight;
            }
        }

        // 1 at the hit, easing to 0 at `t` = 1 with no kink at either end — the bulge's smooth edges.
        public static float Falloff(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t * t;
            return u * u;
        }

        // Rounded to whole art pixels.
        public static Vector2 Snap(Vector2 world, int pixelsPerUnit) =>
            new(Mathf.Round(world.x * pixelsPerUnit) / pixelsPerUnit,
                Mathf.Round(world.y * pixelsPerUnit) / pixelsPerUnit);

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
