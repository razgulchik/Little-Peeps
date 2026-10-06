using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // The growing build price (StructurePrice.At, behind StructureDef.CostAt). What is pinned is what a
    // designer balances by: the two curves, that flat stays linear in both, and the rounding the card
    // relies on — a price must never read "22" on the card while it really asks 22.5.
    //
    // Plain arithmetic, so these run offline as well as in the Editor.
    public class StructurePriceTests
    {
        private static float[] Curve(float baseAmount, float flat, float percent, bool exponential, int count = 5)
        {
            var prices = new float[count];
            for (int n = 0; n < count; n++) prices[n] = StructurePrice.At(baseAmount, n, flat, percent, exponential);
            return prices;
        }

        [Test]
        public void NoGrowth_EveryOneCostsTheBase()
        {
            Assert.That(Curve(10f, 0f, 0f, false), Is.EqualTo(new[] { 10f, 10f, 10f, 10f, 10f }));
            Assert.That(Curve(10f, 0f, 0f, true), Is.EqualTo(new[] { 10f, 10f, 10f, 10f, 10f }));
        }

        [Test]
        public void LinearPercent_AddsAShareOfTheBase_EachTime()
        {
            Assert.That(Curve(10f, 0f, 0.5f, false), Is.EqualTo(new[] { 10f, 15f, 20f, 25f, 30f }));
        }

        [Test]
        public void ExponentialPercent_AddsAShareOfThePreviousPrice_EachTime()
        {
            // x1.5 per building: 10, 15, 22.5, 33.75, 50.625 -> rounded half up.
            Assert.That(Curve(10f, 0f, 0.5f, true), Is.EqualTo(new[] { 10f, 15f, 23f, 34f, 51f }));
        }

        [Test]
        public void Flat_StaysLinear_InBothModes()
        {
            Assert.That(Curve(10f, 5f, 0f, false), Is.EqualTo(new[] { 10f, 15f, 20f, 25f, 30f }));
            Assert.That(Curve(10f, 5f, 0f, true), Is.EqualTo(new[] { 10f, 15f, 20f, 25f, 30f }));
        }

        [Test]
        public void FlatAndPercent_CombineLikeTheStatFormula()
        {
            // (base + n*flat) * (1 + n*percent): n=2 -> (10 + 10) * 2 = 40.
            Assert.That(StructurePrice.At(10f, 2, 5f, 0.5f, false), Is.EqualTo(40f));
            // (base + n*flat) * (1 + percent)^n: n=2 -> 20 * 2.25 = 45.
            Assert.That(StructurePrice.At(10f, 2, 5f, 0.5f, true), Is.EqualTo(45f));
        }

        [Test]
        public void APrice_IsAWholeNumber_RoundedHalfUp()
        {
            Assert.That(StructurePrice.At(10f, 1, 0f, 0.15f, false), Is.EqualTo(12f), "11.5 -> 12");
            Assert.That(StructurePrice.At(10f, 1, 0f, 0.12f, false), Is.EqualTo(11f), "11.2 -> 11");
            Assert.That(StructurePrice.At(10f, 1, 0f, 0.18f, false), Is.EqualTo(12f), "11.8 -> 12");
        }

        // Each of these is exactly x.5 on paper and comes out of single precision a hair below it
        // (25 * 1.06 = 26.499998); without the epsilon they would round DOWN.
        [TestCase(25f, 1, 0.06f, false, 27f)]
        [TestCase(25f, 1, 0.06f, true, 27f)]
        [TestCase(15f, 2, 0.55f, false, 32f)]
        [TestCase(15f, 3, 0.9f, false, 56f)]
        public void Rounding_IsNotFooledByFloatNoise(float baseAmount, int standing, float percent,
                                                     bool exponential, float expected)
        {
            Assert.That(StructurePrice.At(baseAmount, standing, 0f, percent, exponential), Is.EqualTo(expected));
        }

        [Test]
        public void APercentStep_OnABaseOfZero_StaysZero()
        {
            Assert.That(Curve(0f, 0f, 0.5f, false), Is.EqualTo(new[] { 0f, 0f, 0f, 0f, 0f }));
            Assert.That(Curve(0f, 0f, 0.5f, true), Is.EqualTo(new[] { 0f, 0f, 0f, 0f, 0f }));
            Assert.That(Curve(0f, 5f, 0f, false), Is.EqualTo(new[] { 0f, 5f, 10f, 15f, 20f }), "flat is the way");
        }

        [Test]
        public void ANegativeStep_IsADiscount_NeverAPayout()
        {
            Assert.That(StructurePrice.At(10f, 1, -5f, 0f, false), Is.EqualTo(5f));
            Assert.That(StructurePrice.At(10f, 3, -5f, 0f, false), Is.EqualTo(0f));
            Assert.That(StructurePrice.At(10f, 3, 0f, -0.5f, false), Is.EqualTo(0f));
            Assert.That(StructurePrice.At(10f, 2, 0f, -2f, true), Is.EqualTo(0f), "1 + percent is clamped at 0");
        }

        [Test]
        public void ANegativeCount_ReadsAsNoneStanding()
        {
            Assert.That(StructurePrice.At(10f, -1, 5f, 0.5f, true), Is.EqualTo(10f));
        }
    }
}
