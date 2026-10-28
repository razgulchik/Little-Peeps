using UnityEngine;

namespace LittlePeeps
{
    // How a unit's hit on a CollisionTarget went — what the hit feedback shows (the squash, the waves, the
    // lean). Settled in ONE place, CollisionTarget.Resolve, by two questions to the target:
    //   did it work?            yes → Useful
    //   is it for this unit?    yes → Blocked (for it, but not now or not here), no → Useless
    // "For this unit" is any entrance or effect on the target that Serves the unit. Gates never take part:
    // a gate says whether now, never whom for. Ordered weakest to strongest, so a throttle can let a
    // stronger verdict through a weaker one's cooldown and never the other way round.
    public enum HitVerdict
    {
        Useless,   // nothing on the target is for this unit: a house wall to a working unit, a tree to a farmer
        Blocked,   // it is, but not now or not here: tired, a full house, an overheated forge, the market from outside
        Useful,    // it worked: paid, took the unit in, handed it a tool
    }

    // What an ICollisionEffect did with a hit. Weakest to strongest like the verdict: with several effects on
    // one target, the hit counts as the strongest of them.
    public enum HitOutcome
    {
        None,     // nothing: not for this unit, or not now
        Worked,   // paid, or handed something over
        UsedUp,   // worked, and that was the last of it — the source is depleted or gone, and its own harvest
                  // effect is what shows this hit
    }

    // How the unit reached the target.
    public enum HitContact
    {
        Bounce,   // off a solid collider — Point and Side say where
        Pass,     // through a trigger — the unit's own velocity says which way, Point where the middle is
        Ranged,   // from afar (an arrow) — no contact at all
    }

    // One hit, once CollisionTarget has run it (CollisionTarget.HitResolved). Raised after every entrance,
    // gate and effect has had its say, so a listener sees what the hit left behind — a source already
    // depleted, a unit already inside.
    public struct HitReport
    {
        public Unit Unit;
        public HitVerdict Verdict;
        public bool Final;          // the hit used the target up (HitOutcome.UsedUp)
        public HitContact Contact;
        public Vector2 Point;       // Bounce: where the unit touched. Pass: the middle of the trigger it crossed into
        public Vector2Int Side;     // Bounce only: the face it touched, as that face's outward axis; zero when
                                    // the physics reported no contact point
    }
}
