using System;
using UnityEngine;

namespace LittlePeeps
{
    // Anything a bouncing unit can collide with that triggers effects — structures, resource
    // nodes, animals, etc. Owns the collision callbacks and routes each hit in two steps. First every
    // IEntrance on the target, whatever the unit's state: a building that lets the unit in ends the
    // hit there, and whom it lets in is its own door rule (a house takes the tired and the drunk).
    // Then, for a WORKING unit only, the ICollisionEffect components, after every IHitGate on the target
    // has let the hit through — a drunk unit still works. A tired unit nobody let in stops between the
    // two — that one check is the whole "tired units can't work" rule, so no effect or gate has to check
    // stamina. Structure derives from this; an object can also use CollisionTarget directly + effect
    // components like ResourceSource.
    //
    // Every hit ends in one HitReport (HitResolved): how it went — Useful, Blocked, Useless — for whatever
    // shows it. The verdict is settled here and nowhere else, because only here is the whole hit in view: a
    // view listening to the physics itself would see the bounce but not what it did, and the hit changes
    // what there is to see (a tree stops being ready, a forge overheats on this very hit).
    //
    // The Rigidbody2D must sit on this (root) GameObject so the collision callbacks fire here;
    // the body colliders may live on children (fetched via GetComponentsInChildren). A VisitZone
    // child brings its own Rigidbody2D and so keeps its trigger events to itself — see there.
    public class CollisionTarget : MonoBehaviour
    {
        [Header("Sound")]
        [Tooltip("What a unit bouncing off this sounds like: wood, stone, something soft. Empty = the " +
                 "SoundSystem's default bounce. A hit that pays or takes the unit in plays its own sound " +
                 "instead (SoundSystem.PlayHit).")]
        [SerializeField] private SoundDef bounceSound;

        public SoundDef BounceSound => bounceSound;

        // Every hit, once, after the rules have run — see HitReport. What the hit feedback hangs off.
        public event Action<HitReport> HitResolved;

        // A unit on its way out of a trigger here — the end of a Pass. Not a hit: nothing is resolved on the
        // way out, it only tells a view still waiting on that crossing that it is over (LeanView).
        public event Action<Unit> PassEnded;

        private Collider2D[] colliders;
        private ICollisionEffect[] effects;
        private IHitGate[] gates;
        private IEntrance[] entrances;

        protected virtual void Awake()
        {
            colliders = GetComponentsInChildren<Collider2D>();
            effects = GetComponents<ICollisionEffect>();
            gates = GetComponentsInChildren<IHitGate>();
            entrances = GetComponents<IEntrance>();
        }

        // Obstacle path: unit bounces off (collider isTrigger = false). collision.collider is the unit's, so
        // from the contact toward its body is the way out — what settles the face it hit.
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.gameObject.TryGetComponent<Unit>(out var unit)) return;

            var report = new HitReport { Contact = HitContact.Bounce };
            if (collision.contactCount > 0)
            {
                var contact = collision.GetContact(0);
                report.Point = contact.point;
                report.Side = FaceOf(contact.normal, (Vector2)collision.collider.bounds.center - contact.point);
            }
            HandleHit(unit, report);
        }

        // Interactable path: unit passes through (collider isTrigger = true). Here `other` is the
        // raw collider, which on a unit sits on a CHILD of the Unit root — so search up the
        // hierarchy (unlike OnCollisionEnter2D, where collision.gameObject is the Rigidbody root).
        private void OnTriggerEnter2D(Collider2D other)
        {
            var unit = other.GetComponentInParent<Unit>();
            if (unit != null) HandleHit(unit, new HitReport { Contact = HitContact.Pass, Point = MiddleOf(other) });
        }

        // Looked up only while someone listens — on most targets nobody waits for a crossing to end.
        private void OnTriggerExit2D(Collider2D other)
        {
            if (PassEnded == null) return;
            var unit = other.GetComponentInParent<Unit>();
            if (unit != null) PassEnded(unit);
        }

        // The middle of the trigger `other` has just crossed into: of this target's own triggers, the one nearest
        // to it — it overlaps that one. A VisitZone's trigger is not this target's (its own Rigidbody2D keeps its
        // events), so it is skipped even when the unit is inside it too.
        private Vector2 MiddleOf(Collider2D other)
        {
            Collider2D crossed = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (!c.isTrigger || !c.enabled || !RoutesHere(c)) continue;
                float distance = other.Distance(c).distance;
                if (distance < nearest)
                {
                    nearest = distance;
                    crossed = c;
                }
            }
            return crossed != null ? (Vector2)crossed.bounds.center : (Vector2)transform.position;
        }

        // Whether this collider's contacts come to this GameObject: through the Rigidbody2D on it, or, with no
        // body at all, by sitting on it.
        private bool RoutesHere(Collider2D c)
        {
            var body = c.attachedRigidbody;
            return body != null ? body.gameObject == gameObject : c.gameObject == gameObject;
        }

        // Entrances first, for every unit: one that lets the unit in has taken it off the field, so the
        // hit ends there. Past them a tired unit stops — a resource or a market is a plain wall to a
        // unit that is done for the day, and the gates are not even asked, so it can neither spend a
        // market visit nor heat the forge for nothing. Working: Work (a refused hit is a plain bounce
        // with no effect). Every way through ends in Resolve — one report per hit.
        private void HandleHit(Unit unit, HitReport report)
        {
            for (int i = 0; i < entrances.Length; i++)
                if (entrances[i].TryEnter(unit))
                {
                    Resolve(unit, HitOutcome.Worked, report);
                    return;
                }

            Resolve(unit, unit.IsTired ? HitOutcome.None : Work(unit), report);
        }

        // A hit that came from afar instead of by contact — an arrow. Nobody steps through a door from
        // across the field, so the entrances are skipped; and whether the unit was working was settled
        // when it let go (a tired unit never shoots — UnitAbilities), so that is not asked again either.
        // From the gates on it is the same hit a bounce would be: same payout, same depletion.
        public void HitFromRange(Unit unit)
        {
            if (unit != null) Resolve(unit, Work(unit), new HitReport { Contact = HitContact.Ranged });
        }

        // The working half of a hit: gates first (a refused hit has no effect), only then the effects — and
        // the hit counts as the most any of them did.
        private HitOutcome Work(Unit unit)
        {
            for (int i = 0; i < gates.Length; i++)
                if (!gates[i].TryConsume(unit)) return HitOutcome.None;

            var outcome = HitOutcome.None;
            for (int i = 0; i < effects.Length; i++)
            {
                var done = effects[i].OnHit(unit, this);
                if (done > outcome) outcome = done;
            }
            return outcome;
        }

        // The hit's verdict, told to whoever shows it (see HitVerdict). Whom the target is for is asked only
        // when nothing worked, and after the fact — safe, because a refused hit has changed nothing.
        private void Resolve(Unit unit, HitOutcome outcome, HitReport report)
        {
            report.Unit = unit;
            report.Verdict = outcome != HitOutcome.None ? HitVerdict.Useful
                           : Serves(unit) ? HitVerdict.Blocked
                           : HitVerdict.Useless;
            report.Final = outcome == HitOutcome.UsedUp;
            HitResolved?.Invoke(report);
        }

        // Whether anything here is for this unit at all, the moment aside: a door it would go through, a
        // source that pays it. Gates don't take part — they say whether now, never whom for.
        private bool Serves(Unit unit)
        {
            for (int i = 0; i < entrances.Length; i++)
                if (entrances[i].Serves(unit)) return true;
            for (int i = 0; i < effects.Length; i++)
                if (effects[i].Serves(unit)) return true;
            return false;
        }

        // The face a hit landed on, as its outward axis. Unity may report the contact normal either way
        // round; `outward` — anything pointing off the target, toward the unit — settles which. The larger
        // axis wins, so a corner hit goes to the face it struck more squarely.
        public static Vector2Int FaceOf(Vector2 normal, Vector2 outward)
        {
            if (Vector2.Dot(normal, outward) < 0f) normal = -normal;
            if (Mathf.Abs(normal.x) > Mathf.Abs(normal.y)) return new Vector2Int(normal.x > 0f ? 1 : -1, 0);
            return new Vector2Int(0, normal.y < 0f ? -1 : 1);
        }

        // Enable/disable every collider on the target (ResourceSource uses it on depletion). Includes a
        // VisitZone's trigger: with Physics2D.callbacksOnDisable on, disabling it exits every unit inside.
        public void SetColliderEnabled(bool enabled)
        {
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = enabled;
        }
    }
}
