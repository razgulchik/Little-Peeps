using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The meta perk only puts its structure on the run's list; where it stands on the island is
    // IslandContentTests' "start extras".
    public class StartStructureUpgradeDefTests
    {
        private StartStructureUpgradeDef upgrade;
        private StructureDef tent;
        private RunContext run;

        [SetUp]
        public void SetUp()
        {
            tent = ScriptableObject.CreateInstance<StructureDef>();
            tent.displayName = "Tent";
            upgrade = ScriptableObject.CreateInstance<StartStructureUpgradeDef>();
            upgrade.id = "test_start_structure";
            upgrade.structure = tent;
            run = new RunContext();
        }

        [TearDown]
        public void TearDown()
        {
            if (upgrade != null) Object.DestroyImmediate(upgrade);
            if (tent != null) Object.DestroyImmediate(tent);
        }

        [Test]
        public void ARunStartsWithNone() => Assert.That(run.startStructures, Is.Empty);

        [Test]
        public void Granted_ItsStructureStandsOnTheStartIsland()
        {
            run.Grant(upgrade);

            Assert.That(run.startStructures, Is.EqualTo(new[] { tent }));
        }

        [Test]
        public void WithNoStructure_ItAddsNothing()
        {
            upgrade.structure = null;

            run.Grant(upgrade);

            Assert.That(run.startStructures, Is.Empty);
        }

        [Test]
        public void ApplyingItToNoRun_DoesNothing() =>
            Assert.That(() => upgrade.ApplyToRun(null), Throws.Nothing);

        [Test]
        public void Description_SpeaksForItselfUnlessOverridden()
        {
            Assert.That(upgrade.ScreenText, Is.EqualTo("+1 TENT"));

            upgrade.description = "A roof";
            Assert.That(upgrade.ScreenText, Is.EqualTo("A roof"));
        }
    }
}
