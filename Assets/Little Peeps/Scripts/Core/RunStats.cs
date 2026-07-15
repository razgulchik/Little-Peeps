using System.Collections.Generic;

// Per-run aggregator of stat modifiers — the "bonuses" layer of the base+modifiers stat system.
// Base values live in the configs (UnitDef / ResourceSourceDef / Spawner / ...); RunStats stores
// only the accumulated modifiers and applies the SINGLE stacking formula used everywhere:
//
//     final = (base + Σflat) * (1 + Σpercent)
//
// Lifecycle: created fresh on RunContext, so it resets automatically on prestige. It is NOT
// serialised — saves persist the SOURCES (ages bought, perks chosen, meta levels) and rebuild the
// sheet deterministically at run start, so stored values can never drift.
//
// Perf: a lookup is one O(1) Dictionary hit on a struct key (IEquatable → no boxing, no per-hit
// garbage). Modifiers change only a couple of times per run (age/perk), while reads can be per-hit
// (harvest). That's cheap enough as-is; if profiling ever proves otherwise, add a dirty-flag cache
// of computed values here — see TODO(perf) in Add — without touching any call site.
public class RunStats
{
    private readonly struct Key : System.IEquatable<Key>
    {
        public readonly StatId id;
        public readonly UnitType unit;
        public readonly ResourceType res;

        public Key(StatId id, UnitType unit, ResourceType res)
        {
            this.id = id;
            this.unit = unit;
            this.res = res;
        }

        public bool Equals(Key o) => id == o.id && unit == o.unit && res == o.res;
        public override bool Equals(object o) => o is Key k && Equals(k);
        public override int GetHashCode() => (((int)id * 397) ^ (int)unit) * 397 ^ (int)res;
    }

    private struct Accum
    {
        public float flat;
        public float percent;
    }

    private readonly Dictionary<Key, Accum> mods = new();

    // Zero out the scope dimensions a stat does not use, so authored data and queries always agree
    // on the key regardless of stray scope values. Add and Apply MUST both go through this.
    private static Key MakeKey(StatId id, UnitType u, ResourceType r)
    {
        var scope = StatMeta.ScopeOf(id);
        if ((scope & StatScope.Unit) == 0) u = default;
        if ((scope & StatScope.Resource) == 0) r = default;
        return new Key(id, u, r);
    }

    // Accumulate one modifier into its (scope-normalised) bucket.
    public void Add(StatModifier m)
    {
        var key = MakeKey(m.id, m.unitScope, m.resourceScope);
        mods.TryGetValue(key, out var a);
        a.flat += m.flat;
        a.percent += m.percent;
        mods[key] = a;
        // TODO(perf): if a dirty-flag value cache is added later, invalidate it here.
    }

    // Accumulate a whole authored list (e.g. AgeDef.modifiers). Null-safe.
    public void Add(IReadOnlyList<StatModifier> list)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++) Add(list[i]);
    }

    // The one stacking formula. Returns baseValue unchanged when nothing modifies this stat.
    public float Apply(float baseValue, StatId id, UnitType unit = default, ResourceType res = default)
    {
        return mods.TryGetValue(MakeKey(id, unit, res), out var a)
            ? (baseValue + a.flat) * (1f + a.percent)
            : baseValue;
    }

    // Convenience for pure-multiplier stats (e.g. ProductionGlobal): the factor with no base.
    public float Multiplier(StatId id, UnitType unit = default, ResourceType res = default)
        => Apply(1f, id, unit, res);
}
