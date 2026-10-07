namespace LittlePeeps
{
    // Identity of a modifiable game stat. Which scope dimensions an id actually uses is declared in
    // StatMeta.ScopeOf below — RunStats normalises the lookup key by that mask in BOTH Add and Apply,
    // so authored data and the query can never silently miss each other (e.g. a global stat that was
    // accidentally given a unit scope still matches the unscoped query).
    public enum StatId
    {
        ProductionGlobal,   // global multiplier on all resource GAINS (harvest); no scope
        ResourceYield,      // scope: (profession, ResourceType, source) — amount harvested per hit
        UnitSpeed,          // scope: profession — movement speed

        // Durations are plain numbers on the one formula, like every other stat: a modifier scales the
        // SECONDS, so "regrows faster" is authored as a NEGATIVE percent. Each is named after the field
        // it scales, never after a speed, so the sign is obvious from the name at the point of use.
        //
        // Neither of these two can be scoped by profession: a house is type-agnostic — it rests and
        // launches whoever comes home — and stamina is resolved at launch, when a villager is still
        // Unassigned and has no profession to key on yet. The profession comes from a rack later.
        SpawnerRecharge,    // no scope — seconds a unit rests inside a spawner before launching
        UnitStamina,        // no scope — seconds of field work a unit gets per outing before it tires
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
        // on a 1-slot house does nothing until it reaches +100%. Unscoped like SpawnerRecharge: the
        // house does not know or care what its occupants do for a living.
        HouseCapacity,      // no scope — worker slots per house (base: Spawner.capacity)

        // Market passage (VisitZone). Unscoped like the forge: there is one market, and the zone exists
        // only on it. Read on every entry, so it needs no push. A count like HouseCapacity — "+1 hit"
        // is authored as flat. How much a hit PAYS is not this stat's business: that stays with
        // ResourceYield scoped to the market's source.
        MarketVisitHits,    // no scope — counted hits one unit gets per market visit (base: VisitZone.hitsPerVisit)

        // Animal dens and the stable (AnimalSpawner). A count like the two above — "+1 animal" is
        // authored as flat. Read at the point of use, not materialised: a den compares its live animals
        // against it on every check, so a raise is seen without a push. Scoped by the ANIMAL's own
        // source (Boar, Fox, Alpaka), which is what lets "more creatures in the woods" leave the stable's
        // alpacas alone; left empty it reaches every den and the stable.
        DenCapacity,        // scope: source — animals one den keeps out at once (base: AnimalSpawner.maxAnimals)

        // How many of a building may stand at once (StructureDef.LimitIn). A count like the three above —
        // "+1 house" is authored as flat. Read at the point of use (the palette, every placement click),
        // so a raise needs no push. Only a structure with a maxCount above zero has a limit to raise: the
        // modifier never turns an unlimited one limited. Scoped by the building; empty = every limited one.
        StructureLimit,     // scope: structure — how many may stand (base: StructureDef.maxCount)

        // The player's tap (TapSystem). Unscoped: there is one tap, and it reaches whoever is inside the
        // circle, whatever their profession. Read at the point of use — the radius every frame for the
        // cursor ring and on every click, the multiplier on every click — so a perk needs no push.
        TapRadius,          // no scope — radius of the boost circle, world units (base: TapSystem.tapRadius)
        TapBoostMultiplier, // no scope — speed multiplier a tapped unit leaves at (base: TapSystem.boostSpeedMultiplier)

        // --- growth points (add as needed; each is one line here + one Apply() at the consumer) ---
        // UnitLaunchBoost, // scope: UnitType — launch speed multiplier
    }

    // Which scope dimensions are meaningful for a stat. A new dimension (e.g. StructureType) is added
    // here and in RunStats.Key/MakeKey once, then every stat on it is free.
    [System.Flags]
    public enum StatScope
    {
        None     = 0,

        // The worker's PROFESSION (Unit.Type — what it does, not what it was born as; see UnitType).
        // Only stats read while the unit is out working can carry it: a worker has no profession
        // before it crosses a rack, so anything resolved at the house or at launch stays unscoped.
        // Like Source it can be left open — StatModifier.anyProfession — but the open value is a flag
        // beside the enum, because the enum's zero (Unassigned) is a profession of its own.
        Unit     = 1 << 0,
        Resource = 1 << 1,

        // The SOURCE a resource came from, as a direct ResourceSourceDef reference. Needed because
        // ResourceType alone cannot tell two sources of the same resource apart: Market and Alpaka are
        // both Coins, Wheat and Boar are both Food. Without it, "gold from alpaca" would silently buff
        // the market too. Unlike the enum dimensions, this one has a meaningful EMPTY value — see
        // RunStats.Apply: an unset source means "any source", not "no source".
        Source   = 1 << 2,

        // The BUILDING, as a direct StructureDef reference — for a stat that belongs to a kind of
        // structure rather than to a unit or a resource (its build limit). Empty means "any structure",
        // exactly like Source.
        Structure = 1 << 3,
    }

    public static class StatMeta
    {
        // The scope mask for a stat. Keep in sync with the StatId comments above.
        public static StatScope ScopeOf(StatId id) => id switch
        {
            StatId.ResourceYield    => StatScope.Unit | StatScope.Resource | StatScope.Source,
            StatId.UnitSpeed        => StatScope.Unit,

            // Deliberately NOT Unit, although each has a unit in hand at the point of use: the house
            // and the launch happen before any profession exists (see the StatId comments).
            StatId.SpawnerRecharge  => StatScope.None,
            StatId.UnitStamina      => StatScope.None,
            StatId.HouseCapacity    => StatScope.None,

            // Source ONLY, deliberately not Resource as well: a source already fixes its resource, so
            // the second dimension would add nothing but a way to author a mismatched key. Left empty
            // it means every source, exactly as on ResourceYield.
            StatId.SourceRespawn    => StatScope.Source,

            // Source only, for the same reason, and the source is the ANIMAL, not the den: the den has no
            // def of its own on the stat axis, while the animal's def already tells a boar from an alpaca.
            StatId.DenCapacity      => StatScope.Source,

            StatId.StructureLimit   => StatScope.Structure,

            // Listed although they fall through to None anyway, so the choice reads as made rather than
            // forgotten: the one forge needs no Source axis, and "the forge" is not a resource either.
            // The market's passage and the tap are the same case.
            StatId.ForgeHeatPerHit  => StatScope.None,
            StatId.ForgeMaxHeat     => StatScope.None,
            StatId.ForgeCoolingTime => StatScope.None,
            StatId.ForgeHotYield    => StatScope.None,
            StatId.MarketVisitHits  => StatScope.None,
            StatId.TapRadius        => StatScope.None,
            StatId.TapBoostMultiplier => StatScope.None,

            // ProductionGlobal and any future global stat. NOTE this default is why a forgotten entry
            // above is dangerous: the stat silently becomes global instead of scoped.
            _                       => StatScope.None,
        };
    }
}
