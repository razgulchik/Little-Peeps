using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The meta perks in effect in a run (RunContext.Grant): what puts one in, and that nothing puts it in twice — a
    // test preset gives some, the profile holds some, and they may be the same.
    public class RunUpgradesTests
    {
        private RunContext run;
        private readonly List<ScriptableObject> created = new();

        [SetUp]
        public void SetUp() => run = new RunContext();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void ARunStartsWithNone() => Assert.That(run.upgrades, Is.Empty);

        [Test]
        public void Granted_IsInEffect_AndOnRecord()
        {
            var upgrade = Carrying(3f);

            run.Grant(upgrade);

            Assert.That(run.upgrades, Is.EqualTo(new[] { upgrade }));
            Assert.That(run.stats.ApplyCount(0, StatId.CarryCapacity), Is.EqualTo(3));
        }

        [Test]
        public void GrantedTwice_CountsOnce()
        {
            var upgrade = Carrying(3f);

            run.Grant(upgrade);
            run.Grant(upgrade);

            Assert.That(run.upgrades.Count, Is.EqualTo(1));
            Assert.That(run.stats.ApplyCount(0, StatId.CarryCapacity), Is.EqualTo(3));
        }

        // An empty row of a start config.
        [Test]
        public void GrantingNothing_ChangesNothing()
        {
            run.Grant(null);

            Assert.That(run.upgrades, Is.Empty);
        }

        private StatUpgradeDef Carrying(float units)
        {
            var upgrade = ScriptableObject.CreateInstance<StatUpgradeDef>();
            upgrade.id = "test_carry";
            upgrade.modifiers = new List<StatModifier>
            {
                new StatModifier { id = StatId.CarryCapacity, kind = ModifierKind.Flat, value = units },
            };
            created.Add(upgrade);
            return upgrade;
        }
    }
}
