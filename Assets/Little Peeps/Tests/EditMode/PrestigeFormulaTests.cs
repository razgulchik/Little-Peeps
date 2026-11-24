using System.Collections.Generic;
using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // PrestigeFormula converts a finished run into meta currency. Two record terms —
    //
    //     age term     = sum of agePoints over the transitions bought (currentAge - RunContext.FirstAge)
    //     harvest term = floor(coefficient * pow(weighted harvest, exponent))
    //
    // — of which the run is paid only the part that beats the profile's record for that term, and
    //
    //     time term    = pointsPerInterval * whole intervals played
    //
    // — paid in full by every run.
    //
    // Three properties of the record terms are load-bearing design decisions, and all three fail silently
    // when broken.
    //
    // The RECORDS are what make those terms worth "how far did you get" instead of "how long did you play".
    // Lose either subtraction and the same run can be cashed in forever: repeat the age you already
    // reached, or re-harvest the pile you already sold, and be paid again each time. The game still
    // runs — it just pays best for the least interesting way to play.
    //
    // The two records are subtracted and CLAMPED SEPARATELY. Beating your harvest record has to pay even
    // on a run that stopped short of your best age, and vice versa; clamping after summing would let a
    // shortfall in one term silently eat the other term's earnings.
    //
    // The WEIGHT FALLBACK must be defaultWeight, never zero: authoring a weight for a single type would
    // otherwise stop every other type from counting at all, and the only symptom is a payout that looks
    // a bit low.
    //
    // Pure arithmetic, no GameObjects — these run in the offline harness as well as the Test Runner.
    public class PrestigeFormulaTests
    {
        // Non-zero resource types on purpose: Food is 0, so the per-type tests below would pass even if
        // the weight lookup did nothing at all. Same reasoning as the scope tests in RunStatsTests.
        private const ResourceType Listed   = ResourceType.Stone;
        private const ResourceType Unlisted = ResourceType.Metal;

        private static ResourceWeight Weight(ResourceType type, float weight)
            => new ResourceWeight { resourceType = type, weight = weight };

        // Linear and unscaled by default, so a test that isn't about the curve reads as plain arithmetic.
        private static PrestigeFormula Formula(float coefficient = 1f, float exponent = 1f,
                                               float defaultWeight = 1f, int[] agePoints = null,
                                               params ResourceWeight[] weights)
            => new PrestigeFormula
            {
                agePoints     = new List<int>(agePoints ?? new[] { 1 }),
                weights       = new List<ResourceWeight>(weights),
                defaultWeight = defaultWeight,
                coefficient   = coefficient,
                exponent      = exponent,
            };

        // Counted in transitions bought, the unit the age term pays in: currentAge is the player's age
        // number, so `transitions: 3` is a run standing in the Age IV.
        private static RunContext Run(int transitions, ResourceType type = Listed, float harvested = 0f)
        {
            var run = new RunContext { currentAge = RunContext.FirstAge + transitions };
            if (harvested != 0f) run.harvested[type] = harvested;
            return run;
        }

        // Nothing paid out yet — what every test assumes unless it is about the records themselves.
        private static MetaContext FreshProfile() => new MetaContext();

        private static MetaContext Profile(int agePoints = 0, int harvestPoints = 0)
            => new MetaContext { agePointsAwarded = agePoints, harvestPointsAwarded = harvestPoints };

        // --- the age term ------------------------------------------------------------------------

        [Test]
        public void Points_PayOnePerAge_WhenTheRunHarvestedNothing()
        {
            // A run that bought three ages and produced nothing is still worth its three transitions.
            Assert.That(Formula().Points(Run(transitions: 3), FreshProfile()), Is.EqualTo(3));
        }

        [Test]
        public void Points_AreZero_ForARunStillInTheFirstAge()
        {
            // The age the run starts in is not bought, so it pays nothing: the term counts transitions,
            // not the age number the player sees.
            Assert.That(Formula().Points(new RunContext(), FreshProfile()), Is.EqualTo(0));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 4)]
        [TestCase(4, 8)]
        public void Points_SumTheAuthoredPointsOfEveryAgeEntered(int transitions, int expected)
        {
            // The first element is the FIRST BOUGHT age (the Age II), not the one the run starts in.
            var formula = Formula(agePoints: new[] { 1, 1, 2, 4 });

            Assert.That(formula.Points(Run(transitions), FreshProfile()), Is.EqualTo(expected));
        }

        [Test]
        public void Points_RepeatTheLastAge_PastTheEndOfTheList()
        {
            var formula = Formula(agePoints: new[] { 1, 3 });

            // 1 + 3 + 3 + 3.
            Assert.That(formula.Points(Run(transitions: 4), FreshProfile()), Is.EqualTo(10));
        }

        [Test]
        public void Points_PayNothingForAges_WhenTheListIsEmpty()
        {
            Assert.That(Formula(agePoints: new int[0]).Points(Run(transitions: 3), FreshProfile()),
                        Is.EqualTo(0));
        }

        [Test]
        public void Points_IgnoreANegativeAge_InsteadOfTakingItOffTheOthers()
        {
            var formula = Formula(agePoints: new[] { 2, -5, 3 });

            Assert.That(formula.Points(Run(transitions: 3), FreshProfile()), Is.EqualTo(5));
        }

        [Test]
        public void Points_PayTheDifferenceToTheAgeRecord_OnAnUnevenLadder()
        {
            var formula = Formula(agePoints: new[] { 1, 1, 2, 4 });

            // Record: the Age III, 2 points. This run reached the Age V, worth 8 → 6.
            Assert.That(formula.Points(Run(transitions: 4), Profile(agePoints: 2)), Is.EqualTo(6));
        }

        [Test]
        public void Points_AreZero_ForANullRun()
        {
            // CashIn can be reached with no run in progress (a teardown mid-frame); worth 0,
            // not a NullReferenceException.
            Assert.That(Formula().Points(null, FreshProfile()), Is.EqualTo(0));
        }

        [Test]
        public void Points_TreatANullProfileAsAFreshOne()
        {
            // A save that failed to load pays the player in full rather than throwing mid-prestige.
            Assert.That(Formula().Points(Run(transitions: 3), null), Is.EqualTo(3));
        }

        // --- each term is paid only for beating its record ---------------------------------------

        [Test]
        public void Points_PayOnlyForTheAgesTheRunAddedToTheRecord()
        {
            // Bought 5 ages, already paid 3 age points → only the 4th and 5th transitions are new.
            Assert.That(Formula().Points(Run(transitions: 5), Profile(agePoints: 3)), Is.EqualTo(2));
        }

        [Test]
        public void Points_PayOnlyForTheHarvestTheRunAddedToTheRecord()
        {
            var formula = Formula(exponent: 1f);

            // Harvested 100 (worth 100 gross), already paid 40 → 60.
            Assert.That(formula.Points(Run(transitions: 0, Listed, 100f), Profile(harvestPoints: 40)),
                        Is.EqualTo(60));
        }

        [Test]
        public void Points_AreZero_ForARunThatBeatsNeitherRecord()
        {
            var formula = Formula(exponent: 1f);
            var run = Run(transitions: 5, Listed, 100f);

            // Replaying your own best run is worth nothing at all — the whole point of the records.
            Assert.That(formula.Points(run, Profile(agePoints: 5, harvestPoints: 100)), Is.EqualTo(0));
        }

        [Test]
        public void Points_AreZero_ForARunThatFallsShortOfBothRecords()
        {
            var formula = Formula(exponent: 1f);
            var run = Run(transitions: 2, Listed, 10f);

            // A run cut short earns nothing — and must not owe anything either.
            Assert.That(formula.Points(run, Profile(agePoints: 5, harvestPoints: 100)), Is.EqualTo(0));
        }

        [Test]
        public void Points_PayTheHarvestRecord_EvenWhenTheRunStoppedShortOfTheBestAge()
        {
            var formula = Formula(exponent: 1f);
            var run = Run(transitions: 2, Listed, 150f);

            // Age falls 3 short, harvest beats the record by 50. Clamping the terms separately pays the
            // 50; clamping after summing would pay 47 — the age shortfall eating into earned harvest.
            Assert.That(formula.Points(run, Profile(agePoints: 5, harvestPoints: 100)), Is.EqualTo(50));
        }

        [Test]
        public void Points_PayTheAgeRecord_EvenWhenTheRunHarvestedLessThanTheBest()
        {
            var formula = Formula(exponent: 1f);
            var run = Run(transitions: 7, Listed, 10f);

            // The mirror case: two new ages are paid although the harvest fell 90 short.
            Assert.That(formula.Points(run, Profile(agePoints: 5, harvestPoints: 100)), Is.EqualTo(2));
        }

        [Test]
        public void Points_IgnoreNegativeRecords()
        {
            // Garbage from a hand-edited save must not pay a bonus on top of the real payout.
            Assert.That(Formula().Points(Run(transitions: 3), Profile(agePoints: -5, harvestPoints: -5)),
                        Is.EqualTo(3));
        }

        // --- the time term -----------------------------------------------------------------------

        private static PrestigeFormula TimeFormula(float minutes = 5f, int points = 1)
        {
            var formula = Formula();
            formula.minutesPerInterval = minutes;
            formula.pointsPerInterval = points;
            return formula;
        }

        private static RunContext Played(double seconds, int transitions = 0)
        {
            var run = Run(transitions);
            run.playSeconds = seconds;
            return run;
        }

        [TestCase(0.0, 0)]
        [TestCase(299.9, 0)]
        [TestCase(300.0, 1)]
        [TestCase(540.0, 1)]
        [TestCase(600.0, 2)]
        public void TimePoints_PayWholeIntervalsOnly(double seconds, int expected)
        {
            Assert.That(TimeFormula().TimePoints(Played(seconds)), Is.EqualTo(expected));
        }

        [Test]
        public void TimePoints_FollowTheAuthoredIntervalAndPoints()
        {
            // 5 minutes at 2 minutes an interval: two whole ones, 3 points each.
            Assert.That(TimeFormula(minutes: 2f, points: 3).TimePoints(Played(300.0)), Is.EqualTo(6));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void TimePoints_AreOff_WithNoInterval(float minutes)
        {
            Assert.That(TimeFormula(minutes: minutes).TimePoints(Played(6000.0)), Is.EqualTo(0));
        }

        [Test]
        public void TimePoints_AreNeverNegative()
        {
            Assert.That(TimeFormula(points: -1).TimePoints(Played(600.0)), Is.EqualTo(0));
        }

        [Test]
        public void Points_PayForTime_EvenWhenTheRunBeatsNoRecord()
        {
            // The whole point of the term: a run short of every record is still worth its time.
            Assert.That(TimeFormula().Points(Played(600.0, transitions: 2), Profile(agePoints: 8, harvestPoints: 500)),
                        Is.EqualTo(2));
        }

        [Test]
        public void Points_AddTimeToTheRecordTerms()
        {
            Assert.That(TimeFormula().Points(Played(600.0, transitions: 3), FreshProfile()), Is.EqualTo(5));
        }

        // --- the gross terms, which CashIn uses to raise the records ------------------------------

        [Test]
        public void GrossTerms_AreTheRunsOwnValue_IgnoringWhatWasAlreadyPaid()
        {
            var formula = Formula(exponent: 1f);
            var run = Run(transitions: 3, Listed, 100f);

            // CashIn raises each record to these, so they must NOT be net of anything.
            Assert.That(formula.AgePoints(run), Is.EqualTo(3));
            Assert.That(formula.HarvestPoints(run), Is.EqualTo(100));
        }

        [Test]
        public void GrossTerms_AreZero_ForANullRun()
        {
            Assert.That(Formula().AgePoints(null), Is.EqualTo(0));
            Assert.That(Formula().HarvestPoints(null), Is.EqualTo(0));
            Assert.That(Formula().TimePoints(null), Is.EqualTo(0));
        }

        // --- weights -----------------------------------------------------------------------------

        [Test]
        public void Points_ScaleEachResourceByItsAuthoredWeight()
        {
            var formula = Formula(weights: Weight(Listed, 2f));

            // 8 Stone * weight 2 = 16, linear, no age.
            Assert.That(formula.Points(Run(transitions: 0, Listed, 8f), FreshProfile()), Is.EqualTo(16));
        }

        [Test]
        public void Points_UseDefaultWeight_ForATypeMissingFromTheList()
        {
            var formula = Formula(defaultWeight: 3f, weights: Weight(Listed, 2f));

            // Metal isn't listed → 5 * defaultWeight 3 = 15. If the fallback were 0 this would pay
            // nothing, and authoring one weight would have silently disabled every other resource.
            Assert.That(formula.Points(Run(transitions: 0, Unlisted, 5f), FreshProfile()), Is.EqualTo(15));
        }

        [Test]
        public void Points_SumAcrossResourceTypes()
        {
            var formula = Formula(defaultWeight: 1f, weights: Weight(Listed, 2f));
            var run = Run(transitions: 0, Listed, 8f);
            run.harvested[Unlisted] = 5f;

            // 8 * 2 + 5 * 1 = 21.
            Assert.That(formula.Points(run, FreshProfile()), Is.EqualTo(21));
        }

        // --- the curve ---------------------------------------------------------------------------

        [Test]
        public void Points_AtExponentOneHalf_PayTwiceAsMuchForFourTimesTheHarvest()
        {
            var formula = Formula(exponent: 0.5f);

            // How fast the harvest record gets more expensive to beat: quadrupling the harvest only
            // doubles what it is worth.
            Assert.That(formula.Points(Run(transitions: 0, Listed, 100f), FreshProfile()), Is.EqualTo(10));
            Assert.That(formula.Points(Run(transitions: 0, Listed, 400f), FreshProfile()), Is.EqualTo(20));
        }

        [Test]
        public void Points_AtExponentOne_AreLinear()
        {
            // The knob the designer turns to change that, without a code change.
            var formula = Formula(exponent: 1f);

            Assert.That(formula.Points(Run(transitions: 0, Listed, 100f), FreshProfile()), Is.EqualTo(100));
            Assert.That(formula.Points(Run(transitions: 0, Listed, 400f), FreshProfile()), Is.EqualTo(400));
        }

        [Test]
        public void Points_ScaleTheHarvestTermByTheCoefficient()
        {
            var formula = Formula(coefficient: 0.5f, exponent: 1f);

            Assert.That(formula.Points(Run(transitions: 0, Listed, 100f), FreshProfile()), Is.EqualTo(50));
        }

        [Test]
        public void Points_FloorTheHarvestTerm_AndAddItToTheWholeAgeTerm()
        {
            var formula = Formula(exponent: 0.5f);

            // sqrt(2) = 1.41... → 1, plus 3 ages. The fractional part is dropped, never rounded up:
            // prestige points are whole or the meta screen has to explain halves.
            Assert.That(formula.Points(Run(transitions: 3, Listed, 2f), FreshProfile()), Is.EqualTo(4));
        }

        [Test]
        public void Points_AreNeverNegative()
        {
            // A coefficient typed with a stray minus in the inspector must cost the player nothing.
            var formula = Formula(coefficient: -5f, exponent: 1f);

            Assert.That(formula.Points(Run(transitions: 0, Listed, 100f), FreshProfile()), Is.EqualTo(0));
        }

        // --- the ledger --------------------------------------------------------------------------

        [Test]
        public void WeightedHarvest_IsZero_ForAFreshRun()
        {
            // RunContext starts with an empty ledger, not a null one — the projection can be asked for
            // at any moment in the run, including its first frame.
            Assert.That(Formula().WeightedHarvest(new RunContext()), Is.EqualTo(0f));
        }
    }
}
