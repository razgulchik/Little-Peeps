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
    // garbage) — two for a source-scoped stat queried with a source, two for a profession-scoped one,
    // two for a structure-scoped one, four for ResourceYield, which carries both unit and source: the
    // price of the "any" buckets in Apply. Modifiers change only a couple of times per run (age/perk), while
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
            public readonly StructureDef st;         // null = "any structure"; see Apply

            public Key(StatId id, UnitType unit, ResourceType res, ResourceSourceDef src, StructureDef st)
            {
                this.id = id;
                this.unit = unit;
                this.res = res;
                this.src = src;
                this.st = st;
            }

            // ReferenceEquals, never ==: UnityEngine.Object overloads == with the "a destroyed object
            // equals null" rule, which would let a key quietly change meaning mid-run. Plain identity is
            // all this needs — RunStats never dereferences the source, it only tells sources apart.
            public bool Equals(Key o) => id == o.id && unit == o.unit && res == o.res
                                      && ReferenceEquals(src, o.src) && ReferenceEquals(st, o.st);
            public override bool Equals(object o) => o is Key k && Equals(k);

            // RuntimeHelpers.GetHashCode is the IDENTITY hash: pure managed, unlike GetInstanceID()
            // which is a native call — that matters both for the per-hit cost here and because the
            // offline test harness has no native Unity to call into.
            public override int GetHashCode()
            {
                int h = (((int)id * 397) ^ (int)unit) * 397 ^ (int)res;
                h = h * 397 ^ Identity(src);
                return h * 397 ^ Identity(st);
            }

            private static int Identity(object o) =>
                ReferenceEquals(o, null) ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }

        private struct Accum
        {
            public float flat;
            public float percent;
        }

        private readonly Dictionary<Key, Accum> mods = new();

        // The "any profession" bucket's unit (StatModifier.anyProfession). Outside the enum on purpose
        // and never serialised: it exists only inside keys, so no asset, no query and no workerYields
        // entry can name it — a query always arrives with a real profession and picks this bucket up in
        // Apply alongside its own.
        private const UnitType AnyUnit = (UnitType)(-1);

        // Raised after a modifier, or a whole authored list, has landed. Almost nothing needs it: a stat
        // read at the point of use — every duration, speed and yield — simply sees the new value on its
        // next read. It exists for the MATERIALISED stat (HouseCapacity), whose value was turned into
        // slots long before the perk was bought and would otherwise never be asked for again. Fires
        // once per Add CALL, not once per modifier, so an age's whole list costs listeners one refresh.
        public event System.Action Changed;

        // The one place a scope tuple is turned into a key. Two corrections happen here, and Add and
        // Apply MUST both go through them or authored data and queries stop meeting:
        //
        //   1. dimensions the stat does not use are zeroed, so stray authored values cannot shift a key —
        //      an "any profession" flag included, which on such a stat is just one more stray value;
        //   2. a resource that came with a source is REPLACED by that source's own.
        private static Key MakeKey(StatId id, UnitType u, bool anyUnit, ResourceType r, ResourceSourceDef s,
                                   StructureDef st)
        {
            var scope = StatMeta.ScopeOf(id);
            if ((scope & StatScope.Unit) == 0) u = default;
            else if (anyUnit) u = AnyUnit;
            if ((scope & StatScope.Resource) == 0) r = default;
            if ((scope & StatScope.Source) == 0) s = null;
            if ((scope & StatScope.Structure) == 0) st = null;

            // A source fixes its own resource — Tree is Wood, Wheat is Food — so on a stat carrying both
            // dimensions the authored pair can DISAGREE, and (Food, Tree) would file the modifier under
            // a combination the read side never asks for: a bonus that is bought, saved, and silently
            // does nothing. Derive instead of trusting, on both sides, and the two still meet. It also
            // makes the Resource field redundant whenever a source is filled in, which is exactly what
            // StatModifierDrawer relies on to hide it.
            //
            // Unity's == here rather than the ReferenceEquals used everywhere else in this class: this
            // reference is about to be DEREFERENCED, so a destroyed asset must read as "no source"
            // instead of throwing. Key's identity comparisons stay on ReferenceEquals, for the reason
            // given there.
            if ((scope & StatScope.Resource) != 0 && s != null) r = s.resource;

            return new Key(id, u, r, s, st);
        }

        // Accumulate one modifier into its (scope-normalised) bucket.
        public void Add(StatModifier m)
        {
            Accumulate(m);
            Changed?.Invoke();
        }

        // Accumulate a whole authored list (e.g. AgeDef.modifiers). Null-safe; an empty list is not a
        // change and raises nothing.
        public void Add(IReadOnlyList<StatModifier> list)
        {
            if (list == null || list.Count == 0) return;
            for (int i = 0; i < list.Count; i++) Accumulate(list[i]);
            Changed?.Invoke();
        }

        private void Accumulate(StatModifier m)
        {
            var key = MakeKey(m.id, m.unitScope, m.anyProfession, m.resourceScope, m.sourceScope, m.structureScope);
            mods.TryGetValue(key, out var a);
            a.flat += m.flat;
            a.percent += m.percent;
            mods[key] = a;
            // TODO(perf): if a dirty-flag value cache is added later, invalidate it here.
        }

        // The one stacking formula. Returns baseValue unchanged when nothing modifies this stat.
        //
        // Several buckets can contribute: the exact one, the source-agnostic one an author leaves by not
        // filling sourceScope in, the profession-agnostic one (anyProfession), the one that is both — and
        // each of those again for "any structure" on a structure-scoped stat. They are SUMMED and the
        // formula runs ONCE, so percents from all of them still stack additively — running the formula
        // per bucket would multiply them instead, and "+50% from trees" alongside "+50% from anything"
        // would come out as x2.25.
        public float Apply(float baseValue, StatId id, UnitType unit = default,
                           ResourceType res = default, ResourceSourceDef source = null,
                           StructureDef structure = null)
        {
            var key = MakeKey(id, unit, false, res, source, structure);

            // Each wildcard is read only where its dimension is live. key.src / key.st are NORMALISED, so
            // the agnostic bucket is skipped both when the stat lacks that dimension and when the caller
            // passed none; the unit check asks the mask for the same reason. In every skipped case MakeKey
            // has already collapsed the wildcard key into the exact one, and reading that same bucket a
            // second time would double the bonus.
            bool anySource = !ReferenceEquals(key.src, null);
            bool anyUnit = (StatMeta.ScopeOf(id) & StatScope.Unit) != 0;

            float flat = 0f, percent = 0f;
            CollectAround(key, key.st, anySource, anyUnit, ref flat, ref percent);
            if (!ReferenceEquals(key.st, null)) CollectAround(key, null, anySource, anyUnit, ref flat, ref percent);

            return (baseValue + flat) * (1f + percent);
        }

        // The unit × source buckets around one structure value (the queried one, or null = any).
        private void CollectAround(in Key key, StructureDef st, bool anySource, bool anyUnit,
                                   ref float flat, ref float percent)
        {
            Collect(new Key(key.id, key.unit, key.res, key.src, st), ref flat, ref percent);
            if (anySource) Collect(new Key(key.id, key.unit, key.res, null, st), ref flat, ref percent);
            if (anyUnit)
            {
                Collect(new Key(key.id, AnyUnit, key.res, key.src, st), ref flat, ref percent);
                if (anySource) Collect(new Key(key.id, AnyUnit, key.res, null, st), ref flat, ref percent);
            }
        }

        private void Collect(in Key key, ref float flat, ref float percent)
        {
            if (!mods.TryGetValue(key, out var a)) return;
            flat += a.flat;
            percent += a.percent;
        }

        // Convenience for pure-multiplier stats (e.g. ProductionGlobal): the factor with no base.
        public float Multiplier(StatId id, UnitType unit = default, ResourceType res = default,
                                ResourceSourceDef source = null, StructureDef structure = null)
            => Apply(1f, id, unit, res, source, structure);

        // Apply for a stat whose base is a COUNT (house slots, hits per visit): the formula's float,
        // rounded DOWN. Down, so a percent on a small base does nothing until it really reaches the
        // next whole — "+1" is authored as flat. The epsilon is for float noise only, and the noise is
        // real: percents are summed in single precision, and three -20% on a base of 10 come out as
        // 3.9999998, which is three, not the four the player was promised. (Positive sums happen to
        // round up and would not need it; a penalty is where it bites.) No clamp here — a house needs
        // at least one slot, a visit at least one hit, and that floor belongs to the caller that knows it.
        public int ApplyCount(int baseValue, StatId id, UnitType unit = default,
                              ResourceType res = default, ResourceSourceDef source = null,
                              StructureDef structure = null)
            => (int)System.Math.Floor(Apply(baseValue, id, unit, res, source, structure) + 1e-4f);
    }
}
