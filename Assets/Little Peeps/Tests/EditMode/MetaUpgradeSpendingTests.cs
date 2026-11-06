using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // The spending rules of the Ancestral Wisdom screen, on MetaContext: an upgrade is taken whole for its
    // price, only once, and is free to return. prestigePoints itself is never spent down — a taken upgrade
    // HOLDS its price — which is what makes re-spending free and the balance impossible to get out of step:
    // there is no second number to keep in sync, only "total minus what the taken upgrades hold".
    //
    // Plain ids and plain numbers, so these run in the offline harness as well as the Test Runner.
    public class MetaUpgradeSpendingTests
    {
        private const string Boots = "boots";
        private const string Houses = "houses";

        [Test]
        public void FreePoints_AreTheTotalMinusWhatTheTakenUpgradesHold()
        {
            var meta = new MetaContext { prestigePoints = 5 };

            meta.TryTake(Boots, 2);

            Assert.That(meta.PointsInvested, Is.EqualTo(2));
            Assert.That(meta.FreePoints, Is.EqualTo(3));
            Assert.That(meta.prestigePoints, Is.EqualTo(5), "the total is never spent down");
        }

        [Test]
        public void Take_NeedsTheWholePrice()
        {
            var meta = new MetaContext { prestigePoints = 2 };

            Assert.That(meta.TryTake(Boots, 3), Is.False);
            Assert.That(meta.IsTaken(Boots), Is.False);
            Assert.That(meta.FreePoints, Is.EqualTo(2), "a refused take must not hold a point");
        }

        [Test]
        public void Take_MaySpendTheLastPoint()
        {
            var meta = new MetaContext { prestigePoints = 3 };

            Assert.That(meta.TryTake(Boots, 3), Is.True);
            Assert.That(meta.IsTaken(Boots), Is.True);
            Assert.That(meta.FreePoints, Is.Zero);
        }

        // One icon, one upgrade: there is no second level to buy.
        [Test]
        public void Take_Twice_IsRefused()
        {
            var meta = new MetaContext { prestigePoints = 10 };

            meta.TryTake(Boots, 2);

            Assert.That(meta.TryTake(Boots, 2), Is.False);
            Assert.That(meta.FreePoints, Is.EqualTo(8), "a refused take must not hold a point");
        }

        [Test]
        public void Return_GivesTheWholePriceBack()
        {
            var meta = new MetaContext { prestigePoints = 3 };
            meta.TryTake(Boots, 3);

            Assert.That(meta.TryReturn(Boots), Is.True);

            Assert.That(meta.IsTaken(Boots), Is.False);
            Assert.That(meta.FreePoints, Is.EqualTo(3));
        }

        [Test]
        public void Return_WhenNotTaken_IsRefused()
        {
            var meta = new MetaContext { prestigePoints = 2 };

            Assert.That(meta.TryReturn(Boots), Is.False);
            Assert.That(meta.FreePoints, Is.EqualTo(2), "nothing to refund, and nothing may appear from nowhere");
        }

        // The point of free re-spending: everything put in can be moved somewhere else.
        [Test]
        public void PointsMoveFreelyBetweenUpgrades()
        {
            var meta = new MetaContext { prestigePoints = 2 };
            meta.TryTake(Boots, 2);
            Assert.That(meta.TryTake(Houses, 1), Is.False, "both points are held");

            meta.TryReturn(Boots);

            Assert.That(meta.TryTake(Houses, 1), Is.True);
            Assert.That(meta.IsTaken(Boots), Is.False);
            Assert.That(meta.IsTaken(Houses), Is.True);
            Assert.That(meta.FreePoints, Is.EqualTo(1));
        }

        // A cash-in after spending adds to what is free: the payout lands on the total, the taken upgrades
        // keep what they hold.
        [Test]
        public void ANewPayout_AddsToTheFreePoints()
        {
            var meta = new MetaContext { prestigePoints = 2 };
            meta.TryTake(Boots, 1);

            meta.BankPayout(payout: 3, agePointsEarned: 0, harvestPointsEarned: 0);

            Assert.That(meta.FreePoints, Is.EqualTo(4));
            Assert.That(meta.IsTaken(Boots), Is.True);
        }

        // An upgrade without an id could never be found again — a save keys what is taken by it.
        [Test]
        public void AnEmptyId_CannotBeTaken()
        {
            var meta = new MetaContext { prestigePoints = 3 };

            Assert.That(meta.TryTake("", 1), Is.False);
            Assert.That(meta.TryTake(null, 1), Is.False);
            Assert.That(meta.FreePoints, Is.EqualTo(3));
        }
    }
}
