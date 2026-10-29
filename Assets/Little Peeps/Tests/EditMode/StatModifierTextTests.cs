using System.Collections.Generic;
using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // StatModifierText is what a card shows when nobody worded it by hand — AgeDef.BonusText and
    // PerkDef.DescriptionAt both fall back to it. The list form is what a perk's description is
    // generated from, and the rule pinned here is the one an author relies on without reading code:
    // every modifier gets its line, a modifier that changes nothing gets none, and an empty result
    // means the whole perk changes nothing (the same signal the single form gives).
    //
    // Pure string work, so these run offline as well as in the Editor.
    public class StatModifierTextTests
    {
        private static StatModifier Mod(StatId id, float flat = 0f, float percent = 0f,
                                        UnitType unit = default, bool anyProfession = false)
            => new StatModifier
            {
                id = id, unitScope = unit, anyProfession = anyProfession, kind = percent != 0f ? ModifierKind.Percent : ModifierKind.Flat, value = percent != 0f ? percent : flat,
            };

        // Any profession is the wide default on the unit axis, so it is silent like an empty source —
        // naming the leftover profession under it would advertise the narrowest reading of the widest bonus.
        [Test]
        public void AnyProfession_IsNotNamed()
        {
            Assert.That(StatModifierText.Describe(Mod(StatId.UnitSpeed, percent: 0.5f,
                                                      unit: UnitType.Lumberjack, anyProfession: true)),
                        Is.EqualTo("+50% SPEED"));

            var yield = Mod(StatId.ResourceYield, percent: 1f, anyProfession: true);
            yield.resourceScope = ResourceType.Wood;
            Assert.That(StatModifierText.Describe(yield), Is.EqualTo("+100% WOOD"));
        }

        // An empty building is every limited one — the wide default, so the text names no building. The
        // named form needs a real StructureDef and lives in StructureLimitTests.
        [Test]
        public void StructureLimit_WithNoBuilding_ReadsAsBuildLimit()
        {
            Assert.That(StatModifierText.Describe(Mod(StatId.StructureLimit, flat: 1f)),
                        Is.EqualTo("+1 BUILD LIMIT"));
        }

        [Test]
        public void DenCapacity_ReadsAsAnimalsPerDen()
        {
            Assert.That(StatModifierText.Describe(Mod(StatId.DenCapacity, flat: 1f)),
                        Is.EqualTo("+1 ANIMALS PER DEN"));
        }

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
                Mod(StatId.UnitSpeed),                       // zero: nothing to say
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

        // --- a perk level ------------------------------------------------------------------------------

        // Any profession, so a unit-scoped stat (SPEED) reads without one; the stats with no unit ignore it.
        private static LevelledModifier Levels(StatId id, ModifierKind kind, params float[] steps) =>
            new LevelledModifier { id = id, anyProfession = true, kind = kind, steps = new List<float>(steps) };

        [Test]
        public void Level1_IsTheStepAlone()
        {
            var m = Levels(StatId.TapRadius, ModifierKind.Percent, 0.1f, 0.1f, 0.2f);

            Assert.That(StatModifierText.Describe(m, 1), Is.EqualTo("+10% TAP RADIUS"));
        }

        // The card shows what this level adds AND what the bonus comes to — in square brackets, after the
        // scope's parentheses.
        [Test]
        public void ALaterLevel_ShowsTheStepThenTheTotal()
        {
            var pressure = Levels(StatId.TapRadius, ModifierKind.Percent, 0.1f, 0.1f, 0.2f);
            Assert.That(StatModifierText.Describe(pressure, 3), Is.EqualTo("+20% TAP RADIUS [TOTAL +40%]"));

            var boots = Levels(StatId.UnitSpeed, ModifierKind.Flat, 0.5f, 0.5f, 0.5f);
            Assert.That(StatModifierText.Describe(boots, 3), Is.EqualTo("+0.5 SPEED [TOTAL +1.5]"));

            var food = Levels(StatId.ResourceYield, ModifierKind.Percent, 0.05f, 0.05f);
            food.anyProfession = false;
            food.unitScope = UnitType.Farmer;
            Assert.That(StatModifierText.Describe(food, 2), Is.EqualTo("+5% FOOD (FARMER) [TOTAL +10%]"));
        }

        // The card says what changes: a bonus that does not grow on this level has no line.
        [Test]
        public void ALevelThatAddsNothing_GivesNoLine()
        {
            var m = Levels(StatId.TapRadius, ModifierKind.Flat, 1f, 0f);

            Assert.That(StatModifierText.Describe(m, 2), Is.Empty);
        }

        // A bonus that joins on a later level holds only that step — the total would repeat it.
        [Test]
        public void ABonusJoiningLate_ShowsNoTotal()
        {
            var m = Levels(StatId.TapRadius, ModifierKind.Flat, 0f, 0f, 1f);

            Assert.That(StatModifierText.Describe(m, 3), Is.EqualTo("+1 TAP RADIUS"));
        }

        [Test]
        public void PerkList_GivesOneLinePerBonusThatGrows()
        {
            var modifiers = new List<LevelledModifier>
            {
                Levels(StatId.UnitSpeed, ModifierKind.Flat, 0.5f, 0.5f),
                Levels(StatId.UnitStamina, ModifierKind.Flat, 1f, 0f),
            };

            Assert.That(StatModifierText.Describe(modifiers, 1), Is.EqualTo("+0.5 SPEED\n+1 STAMINA"));
            Assert.That(StatModifierText.Describe(modifiers, 2), Is.EqualTo("+0.5 SPEED [TOTAL +1]"));
            Assert.That(StatModifierText.Describe((IReadOnlyList<LevelledModifier>)null, 1), Is.Empty);
        }

        // The meta screen at the top level: no next step, just what every level gives together.
        [Test]
        public void Total_IsOnePlainLinePerBonus()
        {
            var modifiers = new List<LevelledModifier>
            {
                Levels(StatId.ProductionGlobal, ModifierKind.Percent, 0.25f, 0.5f),
                Levels(StatId.UnitStamina, ModifierKind.Flat, 1f, 0f),
            };

            Assert.That(StatModifierText.DescribeTotal(modifiers, 2), Is.EqualTo("+75% ALL PRODUCTION\n+1 STAMINA"));
            Assert.That(StatModifierText.DescribeTotal((IReadOnlyList<LevelledModifier>)null, 2), Is.Empty);
        }
    }

    // A levelled bonus's numbers per level (perk or meta upgrade): what one level adds, and what the run
    // holds once it is taken.
    public class LevelledModifierTests
    {
        private static LevelledModifier Steps(params float[] steps) =>
            new LevelledModifier { id = StatId.TapRadius, steps = new List<float>(steps) };

        [Test]
        public void StepAt_IsThatLevelsOwnStep()
        {
            var m = Steps(0.1f, 0.1f, 0.2f);

            Assert.That(m.StepAt(1), Is.EqualTo(0.1f));
            Assert.That(m.StepAt(3), Is.EqualTo(0.2f));
        }

        // A level outside the list adds nothing rather than throwing — a step list StatPerkDef has not
        // sized yet, or a level 0 asked by mistake.
        [Test]
        public void StepAt_OutsideTheList_IsZero()
        {
            var m = Steps(0.1f);

            Assert.That(m.StepAt(0), Is.Zero);
            Assert.That(m.StepAt(2), Is.Zero);
            Assert.That(new LevelledModifier().StepAt(1), Is.Zero);
        }

        [Test]
        public void TotalAt_SumsEveryStepUpToTheLevel()
        {
            var m = Steps(0.1f, 0.1f, 0.2f);

            Assert.That(m.TotalAt(1), Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(m.TotalAt(3), Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void With_KeepsTheStatAndScopes()
        {
            var m = Steps(1f);
            m.unitScope = UnitType.Miner;
            m.resourceScope = ResourceType.Metal;
            m.kind = ModifierKind.Percent;

            StatModifier one = m.With(0.3f);

            Assert.That(one.id, Is.EqualTo(StatId.TapRadius));
            Assert.That(one.unitScope, Is.EqualTo(UnitType.Miner));
            Assert.That(one.resourceScope, Is.EqualTo(ResourceType.Metal));
            Assert.That(one.kind, Is.EqualTo(ModifierKind.Percent));
            Assert.That(one.value, Is.EqualTo(0.3f));
        }

        // More levels repeat the last step ("the same again"); fewer drop the extra ones; none yet starts at 0.
        [Test]
        public void FitSteps_FollowsTheLevelCount()
        {
            var modifiers = new List<LevelledModifier> { Steps(0.1f, 0.2f), Steps(1f, 2f, 3f), new LevelledModifier() };

            LevelledModifier.FitSteps(modifiers, 3);
            Assert.That(modifiers[0].steps, Is.EqualTo(new[] { 0.1f, 0.2f, 0.2f }));
            Assert.That(modifiers[1].steps, Is.EqualTo(new[] { 1f, 2f, 3f }));
            Assert.That(modifiers[2].steps, Is.EqualTo(new[] { 0f, 0f, 0f }));

            LevelledModifier.FitSteps(modifiers, 1);
            Assert.That(modifiers[1].steps, Is.EqualTo(new[] { 1f }));

            Assert.DoesNotThrow(() => LevelledModifier.FitSteps(null, 2));
        }
    }
}
