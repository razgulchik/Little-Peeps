namespace LittlePeeps
{
    // Optional per-hit multiplier on what a ResourceSource pays out. ResourceSource multiplies the
    // def's base amount by every IYieldScale on its own GameObject before handing it to the
    // production gateway, so the factor sits UNDER the run's modifiers: a scale of ×1.5 and a
    // "+50% Metal" perk come out as ×2.25, the same way a bigger base yield would.
    //
    // Asked only for a hit that pays — after every IHitGate has let it through and the worker has
    // been matched against the def — so an implementation can read state a gate on the same object
    // has just updated (ForgeHeat). It must not mutate anything: it answers "how much", not "whether".
    public interface IYieldScale
    {
        float Factor(Unit unit);
    }
}
