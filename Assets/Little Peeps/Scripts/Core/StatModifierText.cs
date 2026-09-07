using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LittlePeeps
{
    // Turns an authored StatModifier into the line the player reads on a card. GENERATED rather than
    // typed, so a balance pass can never leave a stale label behind: AgeDef.bonusOverride exists for
    // wording one card by hand, and an empty override keeps that card following the data forever.
    //
    // The grammar is deliberately mechanical — "+5% FOOD (FARMER)", not prose. That is the point: it
    // is a always-correct starting line on an 82px card, and the override is where it becomes English.
    //
    // Durations are named as DURATIONS ("REGROW TIME", "SPAWN DELAY"), never as speeds. That is what
    // keeps the sign honest with no inversion anywhere: a modifier scales SECONDS (see the duration
    // note in StatId), so SourceRespawn +20% really is a penalty and "+20% REGROW TIME" says so.
    // Naming one of them "REGROW SPEED" would silently invert the meaning of every sign under it.
    public static class StatModifierText
    {
        // Empty for a modifier that changes nothing (both buckets zero) — a card shows nothing rather
        // than "+0%", and the AgeDef inspector flags it as the authoring mistake it almost always is.
        public static string Describe(StatModifier m)
        {
            string magnitude = Magnitude(m);
            if (magnitude == null) return string.Empty;

            (string text, StatScope spoken) subject = Subject(m);
            string scope = Scope(m, subject.spoken);

            return scope.Length > 0
                ? $"{magnitude} {subject.text} ({scope})"
                : $"{magnitude} {subject.text}";
        }

        // flat and percent are independent buckets on the one formula, so an author may set both and
        // both are then shown — dropping one would understate the bonus.
        private static string Magnitude(StatModifier m)
        {
            bool hasFlat = !Mathf.Approximately(m.flat, 0f);
            bool hasPercent = !Mathf.Approximately(m.percent, 0f);

            if (!hasFlat && !hasPercent) return null;
            if (hasFlat && hasPercent) return $"{Signed(m.flat)}, {Signed(m.percent * 100f)}%";
            return hasFlat ? Signed(m.flat) : $"{Signed(m.percent * 100f)}%";
        }

        // The noun the magnitude applies to, plus the scope dimension that noun already spoke — kept
        // together so the parenthetical below can never repeat it, and so a stat whose subject IS one
        // of its scopes says so in exactly one place.
        private static (string text, StatScope spoken) Subject(StatModifier m) => m.id switch
        {
            StatId.ProductionGlobal => ("ALL PRODUCTION", StatScope.None),

            // The resource is the subject here ("+5% FOOD"), so it is not listed again as a scope.
            StatId.ResourceYield    => (Upper(m.resourceScope.ToString()), StatScope.Resource),

            StatId.UnitSpeed        => ("SPEED", StatScope.None),
            StatId.SpawnerRecharge  => ("SPAWN DELAY", StatScope.None),
            StatId.UnitFatigueDelay => ("WORK TIME", StatScope.None),
            StatId.SourceRespawn    => ("REGROW TIME", StatScope.None),

            // A stat added to the enum but not named here falls back to its own name. Visibly ugly on
            // the card, which is the right failure: it asks for a name instead of hiding the bonus.
            _                       => (Upper(m.id.ToString()), StatScope.None),
        };

        // Only the dimensions StatMeta declares live for this stat, minus whatever the subject said.
        // Asking the same ScopeOf the runtime asks is what stops the text from advertising a scope
        // RunStats.MakeKey has already zeroed away.
        private static string Scope(StatModifier m, StatScope spoken)
        {
            StatScope mask = StatMeta.ScopeOf(m.id) & ~spoken;
            var parts = new List<string>(3);

            if ((mask & StatScope.Resource) != 0) parts.Add(Upper(m.resourceScope.ToString()));
            if ((mask & StatScope.Unit) != 0) parts.Add(Upper(m.unitScope.ToString()));

            // An unset source means EVERY source (see StatModifier.sourceScope), which is the default
            // and needs no words — naming it would turn the widest bonus into the narrowest-looking one.
            if ((mask & StatScope.Source) != 0 && m.sourceScope != null) parts.Add(Upper(SourceName(m.sourceScope)));

            return string.Join(", ", parts);
        }

        // ResourceSourceDef.id is the authored identifier, but it is blank on every source shipped so
        // far — the asset name ("Tree", "Wheat") is what actually reads, so it is the fallback.
        private static string SourceName(ResourceSourceDef def) =>
            !string.IsNullOrEmpty(def.id) ? def.id : def.name;

        private static string Signed(float value) =>
            (value > 0f ? "+" : "") + value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Upper(string value) => value.ToUpperInvariant();
    }
}
