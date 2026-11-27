using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // The quarry as pure numbers: a hit pays by its speed over the def's own speed, with no floor and no cap
    // (ImpactYield.For), and the look steps one stage per paying hit, the last one holding (QuarryView.StageIndex).
    public class QuarryTests
    {
        private const float DefSpeed = 2f;

        [TestCase(2f, 1f, TestName = "Plain walk pays the authored yield")]
        [TestCase(1.2f, 0.6f, TestName = "Drunk at 0.6 pays 0.6")]
        [TestCase(8f, 4f, TestName = "Fresh out of a house at x4 pays 4")]
        [TestCase(10f, 5f, TestName = "Fresh tap at x5 pays 5")]
        [TestCase(50f, 25f, TestName = "Speed perks are not capped")]
        public void Factor_IsImpactSpeedOverDefSpeed(float impactSpeed, float expected)
        {
            Assert.That(ImpactYield.For(impactSpeed, DefSpeed), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void Factor_WithNoDefSpeed_PaysThePlainAmount()
        {
            Assert.That(ImpactYield.For(10f, 0f), Is.EqualTo(1f));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(5, 1)]
        [TestCase(-1, 0)]
        public void Stage_StepsPerHit_AndTheLastOneHolds(int hitsTaken, int expected)
        {
            Assert.That(QuarryView.StageIndex(hitsTaken, 2), Is.EqualTo(expected));
        }
    }
}
