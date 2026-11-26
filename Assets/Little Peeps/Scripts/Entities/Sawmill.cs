using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // The sawmill's saw: a long solid bench whose two ENDS are its working faces, and only one of them at a
    // time. It waits for the left end or for the right end; a working lumberjack that strikes the end it is
    // waiting for is paid, and from then on it waits for the other end. Every other contact is a plain
    // bounce: the end it is not waiting for (nothing paid and nothing reset — it keeps waiting for the same
    // end), the long sides, a unit the def doesn't list. Waiting resets nothing either: an idle sawmill keeps
    // its end however long nobody comes. That is the whole tradeoff — the traffic has to reach BOTH ends; a
    // busy stream on one side pays once and then stands at a wall.
    //
    // The resource itself is the ResourceSource next to this, like every source's: its def (Never — the saw
    // never runs out) says who may saw and what one cut is worth, and it credits the payout and fires the
    // pickup effect. This component is its add-on, the mill's way:
    //   IHitGate — only a bounce on the awaited end goes through (see Strikes);
    //   Paid     — and only then does it switch ends. A unit the def doesn't list is let through by the gate
    //              and refused by the source, so it switches nothing; a tired lumberjack never gets this far
    //              (CollisionTarget stops it before the gates) — the doc's "non-Tired".
    //
    // "The end" is more than the end face: End Reach widens it along the bench, so a bounce off the top or the
    // bottom edge close enough to an end counts as a strike on that end too. Measured from the contact point
    // against the bench's own solid colliders, so it follows the body wherever the prefab puts it; the zones
    // are drawn as gizmos while the sawmill is selected. 0 = the end faces alone.
    //
    // The end belongs to the bench, not to a worker: every lumberjack strikes the same awaited end, so two of
    // them on opposite sides take turns. A new sawmill waits for the left end — where its saw stands in the
    // prefab. Left and right are the world's: structures are never rotated. What it LOOKS like (the carriage
    // travelling to the awaited end, the log coming apart) belongs to a view, off Awaited and Cut.
    [RequireComponent(typeof(ResourceSource))]
    public class Sawmill : MonoBehaviour, IHitGate, IRunState
    {
        public enum End { Left, Right }

        [Tooltip("How far along the bench each end reaches, in cells, measured inward from the end face. A " +
                 "bounce off the top or bottom edge within this distance of an end counts as a strike on that " +
                 "end. 0 = only the end faces. Capped at half the bench, so the two ends never overlap.")]
        [SerializeField, Min(0f)] private float endReach = 0.5f;

        private ResourceSource source;
        private Collider2D[] body;         // the bench's solid colliders — what the reach is measured against
        private End awaited = End.Left;

        // For presentation: the end the next paying hit has to land on — where the saw stands.
        public End Awaited => awaited;

        // A cut was paid at `struck`; Awaited is already the other end by then.
        public event Action<End> Cut;

        private void Awake()
        {
            // RequireComponent only adds the source when this component is added; a prefab made before that
            // can still lack it, and then there is nobody to pay.
            source = GetComponent<ResourceSource>();
            if (source == null)
                Debug.LogError($"Sawmill on '{name}' needs a ResourceSource on the same object — it pays nothing.", this);

            body = SolidColliders();
            if (body.Length == 0)
                Debug.LogError($"Sawmill on '{name}' has no solid collider — nothing to strike, and End Reach has " +
                               "no bench to measure along.", this);
        }

        private void OnEnable() { if (source != null) source.Paid += OnPaid; }
        private void OnDisable() { if (source != null) source.Paid -= OnPaid; }

        private void Start()
        {
            var def = source != null ? source.Def : null;
            if (def != null && def.depletion != Depletion.Never)
                Debug.LogError($"Sawmill on '{name}': '{def.name}' must be Never — the saw never runs out; used " +
                               "up, it would stop taking hits (and maybe lose its body) for the regrow time.", this);
        }

        // IHitGate — a plain look, nothing spent: the end switches on Paid, once the hit has really paid. The
        // bench is measured at the hit, not cached: a structure can be moved.
        public bool TryConsume(Unit unit, in HitReport hit)
        {
            Bench(body, out var bench);
            return Strikes(awaited, hit, bench.xMin, bench.xMax, endReach);
        }

        // Whether `hit` is a strike on `end` of a bench spanning benchMinX..benchMaxX in world x: a bounce on
        // the end's own face, or on any face within `reach` of that end (capped at half the bench). A
        // walk-through or an arrow has no face, and neither has a bounce the physics reported no contact point
        // for (Side zero — its Point means nothing then) — none of them is a strike.
        public static bool Strikes(End end, in HitReport hit, float benchMinX, float benchMaxX, float reach)
        {
            if (hit.Contact != HitContact.Bounce || hit.Side == Vector2Int.zero) return false;
            if (hit.Side == FaceOf(end)) return true;

            reach = Mathf.Clamp(reach, 0f, (benchMaxX - benchMinX) / 2f);
            if (reach <= 0f) return false;
            return end == End.Left ? hit.Point.x <= benchMinX + reach : hit.Point.x >= benchMaxX - reach;
        }

        // The end's face as its outward axis — what HitReport.Side holds for a hit on it.
        public static Vector2Int FaceOf(End end) => end == End.Left ? Vector2Int.left : Vector2Int.right;

        public static End Other(End end) => end == End.Left ? End.Right : End.Left;

        private void OnPaid()
        {
            var struck = awaited;
            awaited = Other(struck);
            Cut?.Invoke(struck);
        }

        // IRunState — a saved run: which end the saw waits for. Before the view's Start, which shows it.
        public string StateKey => "sawmill";

        public void SaveState(List<float> into) => into.Add(awaited == End.Right ? 1f : 0f);

        public void LoadState(IReadOnlyList<float> from)
        {
            if (from != null && from.Count >= 1) awaited = from[0] > 0.5f ? End.Right : End.Left;
        }

        private Collider2D[] SolidColliders()
        {
            var all = GetComponentsInChildren<Collider2D>(true);
            return Array.FindAll(all, c => !c.isTrigger);
        }

        // The world rectangle the bench's solid colliders cover. False (and an empty rect) with none.
        private static bool Bench(Collider2D[] colliders, out Rect bench)
        {
            bench = default;
            bool any = false;
            Bounds total = default;
            if (colliders != null)
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] == null) continue;
                    if (!any) total = colliders[i].bounds;
                    else total.Encapsulate(colliders[i].bounds);
                    any = true;
                }
            if (any) bench = Rect.MinMaxRect(total.min.x, total.min.y, total.max.x, total.max.y);
            return any;
        }

        // The two end zones, so the reach can be tuned by eye in Prefab Mode. In Play the awaited end is filled.
        private void OnDrawGizmosSelected()
        {
            if (!Bench(SolidColliders(), out var bench)) return;
            float reach = Mathf.Clamp(endReach, 0f, bench.width / 2f);
            const float face = 0.06f;   // a sliver for the end face itself, so reach 0 still shows where it counts
            DrawZone(End.Left, Rect.MinMaxRect(bench.xMin - face, bench.yMin, bench.xMin + reach, bench.yMax));
            DrawZone(End.Right, Rect.MinMaxRect(bench.xMax - reach, bench.yMin, bench.xMax + face, bench.yMax));
        }

        private void DrawZone(End end, Rect zone)
        {
            bool waited = Application.isPlaying && end == awaited;
            Gizmos.color = new Color(1f, 0.75f, 0.2f, waited ? 0.45f : 0.18f);
            Gizmos.DrawCube(zone.center, zone.size);
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(zone.center, zone.size);
        }
    }
}
