namespace LittlePeeps
{
    // Identity of a modifiable game stat. Which scope dimensions an id actually uses is declared in
    // StatMeta.ScopeOf below — RunStats normalises the lookup key by that mask in BOTH Add and Apply,
    // so authored data and the query can never silently miss each other (e.g. a global stat that was
    // accidentally given a unit scope still matches the unscoped query).
    public enum StatId
    {
        ProductionGlobal,   // global multiplier on all resource GAINS (harvest); no scope
        ResourceYield,      // scope: (UnitType worker, ResourceType, source) — amount harvested per hit
        UnitSpeed,          // scope: UnitType — movement speed

        // Durations are plain numbers on the one formula, like every other stat: a modifier scales the
        // SECONDS, so "regrows faster" is authored as a NEGATIVE percent. Each is named after the field
        // it scales, never after a speed, so the sign is obvious from the name at the point of use.
        SpawnerRecharge,    // scope: UnitType — seconds a unit rests inside a spawner before launching
        UnitFatigueDelay,   // scope: UnitType — seconds a unit roams before it will enter a house
        SourceRespawn,      // scope: source — seconds a depleted resource source takes to regrow

        // --- growth points (add as needed; each is one line here + one Apply() at the consumer) ---
        // HouseCapacity,   // scope: UnitType — worker slots per spawner (materialised → resolve at warmup)
        // UnitLaunchBoost, // scope: UnitType — launch speed multiplier
    }

    // Which scope dimensions are meaningful for a stat. A new dimension (e.g. StructureType) is added
    // here and in RunStats.Key/MakeKey once, then every stat on it is free.
    [System.Flags]
    public enum StatScope
    {
        None     = 0,
        Unit     = 1 << 0,
        Resource = 1 << 1,

        // The SOURCE a resource came from, as a direct ResourceSourceDef reference. Needed because
        // ResourceType alone cannot tell two sources of the same resource apart: Market and Alpaka are
        // both Coins, Wheat and Boar are both Food. Without it, "gold from alpaca" would silently buff
        // the market too. Unlike the enum dimensions, this one has a meaningful EMPTY value — see
        // RunStats.Apply: an unset source means "any source", not "no source".
        Source   = 1 << 2,
    }

    public static class StatMeta
    {
        // The scope mask for a stat. Keep in sync with the StatId comments above.
        public static StatScope ScopeOf(StatId id) => id switch
        {
            StatId.ResourceYield    => StatScope.Unit | StatScope.Resource | StatScope.Source,
            StatId.UnitSpeed        => StatScope.Unit,
            StatId.SpawnerRecharge  => StatScope.Unit,
            StatId.UnitFatigueDelay => StatScope.Unit,

            // Source ONLY, deliberately not Resource as well: a source already fixes its resource, so
            // the second dimension would add nothing but a way to author a mismatched key. Left empty
            // it means every source, exactly as on ResourceYield.
            StatId.SourceRespawn    => StatScope.Source,

            // ProductionGlobal and any future global stat. NOTE this default is why a forgotten entry
            // above is dangerous: the stat silently becomes global instead of scoped.
            _                       => StatScope.None,
        };
    }
}
