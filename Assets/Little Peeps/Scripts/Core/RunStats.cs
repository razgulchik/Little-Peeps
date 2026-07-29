using System.Collections.Generic;

namespace LittlePeeps
{
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
    // garbage) — two for a source-scoped stat queried with a source, which is the price of the
    // "any source" bucket in Apply. Modifiers change only a couple of times per run (age/perk), while
    // reads can be per-hit (harvest). That's cheap enough as-is; if profiling ever proves otherwise,
    // add a dirty-flag cache of computed values here — see TODO(perf) in Add — without touching any
    // call site.
    public class RunStats
    {
        private readonly struct Key : System.IEquatable<Key>
        {
            public readonly StatId id;
            public readonly UnitType unit;
            public readonly ResourceType res;
            public readonly ResourceSourceDef src;   // null = "any source"; see Apply

            public Key(StatId id, UnitType unit, ResourceType res, ResourceSourceDef src)
            {
                this.id = id;
                this.unit = unit;
                this.res = res;
                this.src = src;
            }

            // ReferenceEquals, never ==: UnityEngine.Object overloads == with the "a destroyed object
            // equals null" rule, which would let a key quietly change meaning mid-run. Plain identity is
            // all this needs — RunStats never dereferences the source, it only tells sources apart.
            public bool Equals(Key o) => id == o.id && unit == o.unit && res == o.res
                                      && ReferenceEquals(src, o.src);
            public override bool Equals(object o) => o is Key k && Equals(k);

            // RuntimeHelpers.GetHashCode is the IDENTITY hash: pure managed, unlike GetInstanceID()
            // which is a native call — that matters both for the per-hit cost here and because the
            // offline test harness has no native Unity to call into.
            public override int GetHashCode()
            {
                int h = (((int)id * 397) ^ (int)unit) * 397 ^ (int)res;
                return h * 397 ^ (ReferenceEquals(src, null)
                    ? 0
                    : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(src));
            }
        }

        private struct Accum
        {
            public float flat;
            public float percent;
        }

        private readonly Dictionary<Key, Accum> mods = new();

        // Zero out the scope dimensions a stat does not use, so authored data and queries always agree
        // on the key regardless of stray scope values. Add and Apply MUST both go through this.
        private static Key MakeKey(StatId id, UnitType u, ResourceType r, ResourceSourceDef s)
        {
            var scope = StatMeta.ScopeOf(id);
            if ((scope & StatScope.Unit) == 0) u = default;
            if ((scope & StatScope.Resource) == 0) r = default;
            if ((scope & StatScope.Source) == 0) s = null;
            return new Key(id, u, r, s);
        }

        // Accumulate one modifier into its (scope-normalised) bucket.
        public void Add(StatModifier m)
        {
            var key = MakeKey(m.id, m.unitScope, m.resourceScope, m.sourceScope);
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
        //
        // Two buckets can contribute: the one for this exact source, and the source-agnostic one an
        // author leaves by not filling sourceScope in. They are SUMMED and the formula runs ONCE, so
        // percents from both still stack additively — running the formula twice would multiply them
        // instead, and "+50% from trees" alongside "+50% from anything" would come out as x2.25.
        public float Apply(float baseValue, StatId id, UnitType unit = default,
                           ResourceType res = default, ResourceSourceDef source = null)
        {
            var key = MakeKey(id, unit, res, source);

            float flat = 0f, percent = 0f;
            if (mods.TryGetValue(key, out var exact))
            {
                flat = exact.flat;
                percent = exact.percent;
            }

            // key.src is the NORMALISED source, so this is skipped both when the stat has no Source
            // dimension and when the caller passed none. In either case MakeKey already collapsed the
            // two keys into one, and adding that same bucket a second time would double the bonus.
            if (!ReferenceEquals(key.src, null)
                && mods.TryGetValue(new Key(id, key.unit, key.res, null), out var anySource))
            {
                flat += anySource.flat;
                percent += anySource.percent;
            }

            return (baseValue + flat) * (1f + percent);
        }

        // Convenience for pure-multiplier stats (e.g. ProductionGlobal): the factor with no base.
        public float Multiplier(StatId id, UnitType unit = default, ResourceType res = default,
                                ResourceSourceDef source = null)
            => Apply(1f, id, unit, res, source);
    }
}
