using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // The sluice as pure numbers: sand settles a stage every ten seconds up to a full tray (Sluice.Settle), and
    // washing out pays the stage's entry of the table — 1, 3, 6 (Sluice.Payout). The design's own sums close it:
    // a Miner passing every ten seconds takes 3 in half a minute, one well-spaced visit takes 6.
    public class SluiceTests
    {
        private const float SecondsPerStage = 10f;
        private static readonly int[] Table = { 1, 3, 6 };

        private static (int stage, float progress) After(float seconds, int stage = 0, float progress = 0f)
        {
            Sluice.Settle(ref stage, ref progress, seconds, SecondsPerStage, Table.Length);
            return (stage, progress);
        }

        [TestCase(9.9f, 0)]
        [TestCase(10f, 1)]
        [TestCase(19.9f, 1)]
        [TestCase(20f, 2)]
        [TestCase(30f, 3)]
        [TestCase(500f, 3)]
        public void Settles_AStageEveryTenSeconds_UpToFull(float seconds, int expected)
        {
            Assert.That(After(seconds).stage, Is.EqualTo(expected));
        }

        [Test]
        public void A_FullTray_BanksNothingForLater()
        {
            var full = After(35f);
            Assert.That(full.progress, Is.Zero, "the part-settled stage is dropped at full");
            Assert.That(After(5f, full.stage, full.progress), Is.EqualTo(full), "a full tray's clock stands");
        }

        [TestCase(0, 0f)]
        [TestCase(1, 1f)]
        [TestCase(2, 3f)]
        [TestCase(3, 6f)]
        [TestCase(4, 0f)]
        public void Payout_IsTheTablesEntry(int stage, float expected)
        {
            Assert.That(Sluice.Payout(Table, stage), Is.EqualTo(expected));
        }

        // Washing out every `interval` seconds for half a minute; each visit takes the stage reached and
        // settling starts over (OnPaid).
        [TestCase(10f, 3f, TestName = "A Miner every ten seconds takes 3 in half a minute")]
        [TestCase(15f, 2f, TestName = "Every fifteen seconds takes 2 — the half-settled stage washes out too")]
        [TestCase(30f, 6f, TestName = "One well-spaced visit takes 6")]
        public void HalfAMinute_OfVisits(float interval, float expected)
        {
            int stage = 0;
            float progress = 0f, taken = 0f;
            for (float t = interval; t <= 30f + 1e-3f; t += interval)
            {
                Sluice.Settle(ref stage, ref progress, interval, SecondsPerStage, Table.Length);
                taken += Sluice.Payout(Table, stage);
                stage = 0;
                progress = 0f;
            }
            Assert.That(taken, Is.EqualTo(expected));
        }
    }
}
