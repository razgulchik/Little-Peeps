using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // SoundSystem.CrowdFactor is the whole "a big village is no louder than a small one" rule: below the
    // calm rate nothing changes, past it each crowd sound gets quieter. The property that matters is the
    // one at strength 0.5 — total power (plays per second × volume²) stays where the calm rate left it.
    public class SoundCrowdTests
    {
        private const float Calm = 4f;

        [TestCase(0f)]
        [TestCase(2f)]
        [TestCase(4f)]
        public void AtOrBelowCalmRate_FullVolume(float rate)
        {
            Assert.AreEqual(1f, SoundSystem.CrowdFactor(rate, Calm, 0.5f));
        }

        [Test]
        public void StrengthZero_IsOff()
        {
            Assert.AreEqual(1f, SoundSystem.CrowdFactor(40f, Calm, 0f));
        }

        [TestCase(8f)]
        [TestCase(16f)]
        [TestCase(40f)]
        public void HalfStrength_KeepsTotalPowerAtTheCalmLevel(float rate)
        {
            float factor = SoundSystem.CrowdFactor(rate, Calm, 0.5f);
            Assert.AreEqual(Calm, rate * factor * factor, 1e-4f);
        }

        [Test]
        public void BusierIsQuieterPerPlay()
        {
            Assert.Less(SoundSystem.CrowdFactor(20f, Calm, 0.5f), SoundSystem.CrowdFactor(10f, Calm, 0.5f));
        }
    }
}
