using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // MetaContext.BankPayout is the other half of the prestige records: PrestigeFormula decides what a
    // run is worth, this decides what the profile remembers afterwards.
    //
    // The whole mechanism rests on one operator. Each record is raised with MAX, never assigned: a run
    // that falls short of a record must leave it standing. Assign instead and a deliberately bad run
    // lowers the bar, handing back the difference as payable — so the player earns the same stretch of
    // progress again, and again, by replaying what they were already paid for. Nothing breaks, no error
    // is logged; prestige simply becomes farmable, which is exactly what the records exist to prevent.
    //
    // Plain arithmetic on a plain class — runs in the offline harness as well as the Test Runner.
    public class PrestigePayoutTests
    {
        [Test]
        public void BankPayout_CreditsThePoints()
        {
            var meta = new MetaContext { prestigePoints = 7 };

            meta.BankPayout(payout: 5, agePointsEarned: 0, harvestPointsEarned: 0);

            Assert.That(meta.prestigePoints, Is.EqualTo(12));
        }

        [Test]
        public void BankPayout_RaisesBothRecordsToWhatTheRunEarned()
        {
            var meta = new MetaContext();

            meta.BankPayout(payout: 140, agePointsEarned: 4, harvestPointsEarned: 136);

            Assert.That(meta.agePointsAwarded, Is.EqualTo(4));
            Assert.That(meta.harvestPointsAwarded, Is.EqualTo(136));
        }

        [Test]
        public void BankPayout_NeverLowersARecord()
        {
            var meta = new MetaContext { agePointsAwarded = 8, harvestPointsAwarded = 500 };

            // The farming attempt: a deliberately short run, banked to knock the bar back down.
            meta.BankPayout(payout: 0, agePointsEarned: 1, harvestPointsEarned: 3);

            Assert.That(meta.agePointsAwarded, Is.EqualTo(8),
                        "a weak run must not reset the age record and make it earnable twice");
            Assert.That(meta.harvestPointsAwarded, Is.EqualTo(500),
                        "same for the harvest record — this is the whole anti-farm mechanism");
        }

        [Test]
        public void BankPayout_RaisesOneRecord_WithoutDisturbingTheOther()
        {
            var meta = new MetaContext { agePointsAwarded = 8, harvestPointsAwarded = 500 };

            // Beat the harvest record on a run that stopped well short of the best age.
            meta.BankPayout(payout: 100, agePointsEarned: 2, harvestPointsEarned: 600);

            Assert.That(meta.agePointsAwarded, Is.EqualTo(8));
            Assert.That(meta.harvestPointsAwarded, Is.EqualTo(600));
        }

        [Test]
        public void BankPayout_IgnoresANegativePayout()
        {
            var meta = new MetaContext { prestigePoints = 7 };

            // Prestige is spent elsewhere; banking a run must never be the thing that takes it away.
            meta.BankPayout(payout: -5, agePointsEarned: 0, harvestPointsEarned: 0);

            Assert.That(meta.prestigePoints, Is.EqualTo(7));
        }

        [Test]
        public void BankPayout_AccumulatesAcrossSeveralRuns()
        {
            var meta = new MetaContext();

            meta.BankPayout(payout: 10, agePointsEarned: 3, harvestPointsEarned: 7);
            meta.BankPayout(payout: 6,  agePointsEarned: 5, harvestPointsEarned: 11);

            Assert.That(meta.prestigePoints, Is.EqualTo(16));
            Assert.That(meta.agePointsAwarded, Is.EqualTo(5));
            Assert.That(meta.harvestPointsAwarded, Is.EqualTo(11));
        }
    }
}
