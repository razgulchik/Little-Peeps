using UnityEngine;

namespace LittlePeeps
{
    // The quarry's rule: a hit pays by how hard it lands. The factor is the unit's speed at the moment of the
    // hit over the speed its UnitDef gives it — the def's own number, before any perk. A plain unit walking
    // into it gets the authored yield (×1), a drunk one less (×0.6 with the tavern's 0.6), one fresh out of a
    // tap or a house several times more (×5, ×4 as the scene and the house tune them), easing back down as
    // the boost does. Every perk that makes units faster — walking (UnitSpeed) or tapped (TapBoostMultiplier)
    // — makes them hit harder here too: deliberately, the quarry rewards speed in every form, so it grows
    // with the run. No floor, no cap, no rounding: resources are fractional, and the pickup number shows one
    // decimal.
    //
    // The speed is read off the unit's body as the hit is dispatched. Physics has bounced it by then, and a
    // bounce off a STATIC body (the units' Bounciness 1, no friction) keeps the speed and only turns it, so
    // that is the speed it hit with. Off a moving body it would not be (see Unit.FixedUpdate) — this belongs
    // on a structure, not on an animal.
    //
    // An IYieldScale on the ResourceSource's own object: the factor multiplies the def's base amount, under
    // the run's modifiers, exactly like a bigger authored yield would.
    [RequireComponent(typeof(ResourceSource))]
    public class ImpactYield : MonoBehaviour, IYieldScale
    {
        public float Factor(Unit unit) =>
            unit != null ? For(unit.Velocity.magnitude, unit.def != null ? unit.def.speed : 0f) : 1f;

        // The factor for a hit at `impactSpeed` by a unit whose def walks at `baseSpeed`. A def with no speed
        // gives nothing to measure against: such a hit pays the plain amount.
        public static float For(float impactSpeed, float baseSpeed) =>
            baseSpeed > 0f ? impactSpeed / baseSpeed : 1f;
    }
}
