using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // The spending rules of the meta screen, on MetaContext: one point per level, never past the maximum,
    // never below zero, and free to move back. prestigePoints itself is never spent down — a level HOLDS a
    // point — which is what makes re-spending free and the balance impossible to get out of step: there is
    // no second number to keep in sync, only "total minus what the levels hold".
    //
    // Plain ids and plain numbers, so these run in the offline harness as well as the Test Runner.
    public class MetaUpgradeSpendingTests
    {
        private const string Boots = "boots";
        private const string Houses = "houses";

        [Test]
        public void FreePoints_AreTheTotalMinusWhatTheLevelsHold()
        {
            var meta = new MetaContext { prestigePoints = 5 };

            meta.TryRaise(Boots, 3);
            meta.TryRaise(Boots, 3);

            Assert.That(meta.PointsInvested, Is.EqualTo(2));
            Assert.That(meta.FreePoints, Is.EqualTo(3));
            Assert.That(meta.prestigePoints, Is.EqualTo(5), "the total is never spent down");
        }

        [Test]
        public void Raise_NeedsAFreePoint()
        {
            var meta = new MetaContext { prestigePoints = 1 };

            Assert.That(meta.TryRaise(Boots, 3), Is.True);
            Assert.That(meta.TryRaise(Houses, 3), Is.False);
            Assert.That(meta.GetUpgradeLevel(Houses), Is.EqualTo(0));
        }

        [Test]
        public void Raise_StopsAtTheMaximum()
        {
            var meta = new MetaContext { prestigePoints = 10 };

            meta.TryRaise(Boots, 2);
            meta.TryRaise(Boots, 2);

            Assert.That(meta.TryRaise(Boots, 2), Is.False);
            Assert.That(meta.GetUpgradeLevel(Boots), Is.EqualTo(2));
            Assert.That(meta.FreePoints, Is.EqualTo(8), "a refused raise must not take a point");
        }

        [Test]
        public void Lower_GivesThePointBack()
        {
            var meta = new MetaContext { prestigePoints = 2 };
            meta.TryRaise(Boots, 3);
            meta.TryRaise(Boots, 3);

            Assert.That(meta.TryLower(Boots), Is.True);

            Assert.That(meta.GetUpgradeLevel(Boots), Is.EqualTo(1));
            Assert.That(meta.FreePoints, Is.EqualTo(1));
        }

        [Test]
        public void Lower_AtZero_IsRefused()
        {
            var meta = new MetaContext { prestigePoints = 2 };

            Assert.That(meta.TryLower(Boots), Is.False);
            Assert.That(meta.FreePoints, Is.EqualTo(2), "nothing to refund, and nothing may appear from nowhere");
        }

        // The point of free re-spending: everything put in can be moved somewhere else.
        [Test]
        public void PointsMoveFreelyBetweenUpgrades()
        {
            var meta = new MetaContext { prestigePoints = 2 };
            meta.TryRaise(Boots, 3);
            meta.TryRaise(Boots, 3);
            Assert.That(meta.TryRaise(Houses, 3), Is.False, "both points are held");

            meta.TryLower(Boots);

            Assert.That(meta.TryRaise(Houses, 3), Is.True);
            Assert.That(meta.GetUpgradeLevel(Boots), Is.EqualTo(1));
            Assert.That(meta.GetUpgradeLevel(Houses), Is.EqualTo(1));
            Assert.That(meta.FreePoints, Is.EqualTo(0));
        }

        // A cash-in after spending adds to what is free: the payout lands on the total, the levels keep
        // what they hold.
        [Test]
        public void ANewPayout_AddsToTheFreePoints()
        {
            var meta = new MetaContext { prestigePoints = 2 };
            meta.TryRaise(Boots, 3);

            meta.BankPayout(payout: 3, agePointsEarned: 0, harvestPointsEarned: 0);

            Assert.That(meta.FreePoints, Is.EqualTo(4));
            Assert.That(meta.GetUpgradeLevel(Boots), Is.EqualTo(1));
        }

        // An upgrade without an id could never be found again — a save keys levels by it.
        [Test]
        public void AnEmptyId_CannotBeBought()
        {
            var meta = new MetaContext { prestigePoints = 3 };

            Assert.That(meta.TryRaise("", 3), Is.False);
            Assert.That(meta.TryRaise(null, 3), Is.False);
            Assert.That(meta.FreePoints, Is.EqualTo(3));
        }

        // Lowering to zero leaves no empty row behind: 0 is "never bought", one meaning, one shape.
        [Test]
        public void LoweringToZero_LeavesNoRow()
        {
            var meta = new MetaContext { prestigePoints = 1 };
            meta.TryRaise(Boots, 3);

            meta.TryLower(Boots);

            Assert.That(meta.globalUpgrades.ContainsKey(Boots), Is.False);
        }
    }
}
