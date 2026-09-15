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
        UnitFatigueDelay,   // scope: UnitType — seconds a unit works (after its boost settles) before it tires
        SourceRespawn,      // scope: source — seconds a depleted resource source takes to regrow

        // Forge heat (ForgeHeat). All unscoped: there is one forge. ForgeCoolingTime is a duration like
        // the three above — "cools faster" is a NEGATIVE percent.
        ForgeHeatPerHit,    // no scope — heat one paying hit adds to the forge
        ForgeMaxHeat,       // no scope — heat at which the forge overheats
        ForgeCoolingTime,   // no scope — seconds the forge takes to cool from full to zero
        ForgeHotYield,      // no scope — pure multiplier on forge yield at full heat; reads ×1 until a perk adds to it

        // The one MATERIALISED stat. Every stat above is read at the point of use, so a perk bought
        // mid-run is simply seen by the next read; this one is turned into slots and a global cap at
        // Spawner.Warmup and never asked for again, so a sheet change has to be PUSHED to the houses
        // already standing — RunStats.Changed → SpawnSystem → Spawner.RefreshFromStats. A count, so
        // it resolves through ApplyCount: rounded DOWN, "+1 slot" is authored as flat, and a percent
        // on a 1-slot house does nothing until it reaches +100%.
        HouseCapacity,      // scope: UnitType — worker slots per house (base: Spawner.capacity)

        // Market passage (VisitZone). Unscoped like the forge: there is one market, and the zone exists
        // only on it. Read on every entry, so it needs no push. A count like HouseCapacity — "+1 hit"
        // is authored as flat. How much a hit PAYS is not this stat's business: that stays with
        // ResourceYield scoped to the market's source.
        MarketVisitHits,    // no scope — counted hits one unit gets per market visit (base: VisitZone.hitsPerVisit)

        // --- growth points (add as needed; each is one line here + one Apply() at the consumer) ---
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
            StatId.HouseCapacity    => StatScope.Unit,

            // Source ONLY, deliberately not Resource as well: a source already fixes its resource, so
            // the second dimension would add nothing but a way to author a mismatched key. Left empty
            // it means every source, exactly as on ResourceYield.
            StatId.SourceRespawn    => StatScope.Source,

            // Listed although they fall through to None anyway, so the choice reads as made rather than
            // forgotten: the one forge needs no Source axis, and "the forge" is not a resource either.
            // The market's passage is the same case.
            StatId.ForgeHeatPerHit  => StatScope.None,
            StatId.ForgeMaxHeat     => StatScope.None,
            StatId.ForgeCoolingTime => StatScope.None,
            StatId.ForgeHotYield    => StatScope.None,
            StatId.MarketVisitHits  => StatScope.None,

            // ProductionGlobal and any future global stat. NOTE this default is why a forgotten entry
            // above is dangerous: the stat silently becomes global instead of scoped.
            _                       => StatScope.None,
        };
    }
}
