// Identity of a modifiable game stat. Which scope dimensions an id actually uses is declared in
// StatMeta.ScopeOf below — RunStats normalises the lookup key by that mask in BOTH Add and Apply,
// so authored data and the query can never silently miss each other (e.g. a global stat that was
// accidentally given a unit scope still matches the unscoped query).
public enum StatId
{
    ProductionGlobal,   // global multiplier on all resource GAINS (harvest); no scope
    ResourceYield,      // scope: (UnitType worker, ResourceType) — amount harvested per hit
    UnitSpeed,          // scope: UnitType — movement speed

    // --- growth points (add as needed; each is one line here + one Apply() at the consumer) ---
    // HouseCapacity,   // scope: UnitType — worker slots per spawner (materialised → resolve at warmup)
    // SpawnerRecharge, // scope: UnitType — rest/lockout duration
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
}

public static class StatMeta
{
    // The scope mask for a stat. Keep in sync with the StatId comments above.
    public static StatScope ScopeOf(StatId id) => id switch
    {
        StatId.ResourceYield => StatScope.Unit | StatScope.Resource,
        StatId.UnitSpeed     => StatScope.Unit,
        _                    => StatScope.None,   // ProductionGlobal and any future global stat
    };
}
