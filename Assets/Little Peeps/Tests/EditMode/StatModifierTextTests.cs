using System.Collections.Generic;
using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // StatModifierText is what a card shows when nobody worded it by hand — AgeDef.BonusText and
    // PerkDef.DescriptionText both fall back to it. The list form is what a perk's description is
    // generated from, and the rule pinned here is the one an author relies on without reading code:
    // every modifier gets its line, a modifier that changes nothing gets none, and an empty result
    // means the whole perk changes nothing (the same signal the single form gives).
    //
    // Pure string work, so these run offline as well as in the Editor.
    public class StatModifierTextTests
    {
        private static StatModifier Mod(StatId id, float flat = 0f, float percent = 0f,
                                        UnitType unit = default)
            => new StatModifier { id = id, unitScope = unit, flat = flat, percent = percent };

        [Test]
        public void List_GivesOneLinePerModifier()
        {
            // The second modifier carries a unit scope its stat does not use (house slots are not per
            // profession): the line must not advertise it, exactly as the runtime will not honour it.
            string text = StatModifierText.Describe(new List<StatModifier>
            {
                Mod(StatId.UnitSpeed, percent: 0.5f, unit: UnitType.Lumberjack),
                Mod(StatId.HouseCapacity, flat: 1f, unit: UnitType.Lumberjack),
            });

            Assert.That(text, Is.EqualTo("+50% SPEED (LUMBERJACK)\n+1 HOUSE SLOTS"));
        }

        [Test]
        public void List_SkipsAModifierThatChangesNothing_WithoutABlankLine()
        {
            string text = StatModifierText.Describe(new List<StatModifier>
            {
                Mod(StatId.MarketVisitHits, flat: 1f),
                Mod(StatId.UnitSpeed),                       // both zero: nothing to say
                Mod(StatId.ProductionGlobal, percent: 0.05f),
            });

            Assert.That(text, Is.EqualTo("+1 HITS PER MARKET VISIT\n+5% ALL PRODUCTION"));
        }

        [Test]
        public void List_IsEmpty_WhenNothingChangesAnything()
        {
            Assert.That(StatModifierText.Describe(new List<StatModifier> { Mod(StatId.UnitSpeed) }), Is.Empty);
            Assert.That(StatModifierText.Describe(new List<StatModifier>()), Is.Empty);
            Assert.That(StatModifierText.Describe((IReadOnlyList<StatModifier>)null), Is.Empty);
        }

        [Test]
        public void List_OfOne_ReadsExactlyLikeTheSingleForm()
        {
            var m = Mod(StatId.ForgeHotYield, percent: 0.5f);

            Assert.That(StatModifierText.Describe(new List<StatModifier> { m }),
                        Is.EqualTo(StatModifierText.Describe(m)));
        }
    }
}
