using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The meta perk only says yes to the run; what the run's units do about it is TiredUnitTests.
    public class TiredGoHomeUpgradeDefTests
    {
        private TiredGoHomeUpgradeDef upgrade;
        private RunContext run;

        [SetUp]
        public void SetUp()
        {
            upgrade = ScriptableObject.CreateInstance<TiredGoHomeUpgradeDef>();
            upgrade.id = "test_tired_go_home";
            run = new RunContext();
        }

        [TearDown]
        public void TearDown()
        {
            if (upgrade != null) Object.DestroyImmediate(upgrade);
        }

        [Test]
        public void ARunStartsWithoutIt() => Assert.That(run.tiredGoHome, Is.False);

        [Test]
        public void Granted_TheRunsTiredGoHome()
        {
            run.Grant(upgrade);

            Assert.That(run.tiredGoHome, Is.True);
        }

        [Test]
        public void ApplyingItToNoRun_DoesNothing() =>
            Assert.That(() => upgrade.ApplyToRun(null), Throws.Nothing);

        [Test]
        public void Description_SpeaksForItselfUnlessOverridden()
        {
            Assert.That(upgrade.ScreenText, Is.EqualTo("TIRED PEEPS GO STRAIGHT HOME"));

            upgrade.description = "Homeward";
            Assert.That(upgrade.ScreenText, Is.EqualTo("Homeward"));
        }
    }
}
