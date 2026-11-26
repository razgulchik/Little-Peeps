using System;
using System.Collections.Generic;
using System.Linq;
using LittlePeeps.EditorTools;
using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // The ids saves store defs by (DefIdAudit): which defs it finds, the rules on plain names and ids, then
    // the project's own assets. The live check is DefIdWatch, on every save; this one catches what got past
    // it — a warning that scrolled away, a fault older than the watch.
    public class DefIdTests
    {
        private static DefIdAudit.Kind KindOf(Type type) => DefIdAudit.Kinds.Single(k => k.type == type);

        private static List<(int index, string message)> Problems(params (string name, string id)[] defs) =>
            DefIdAudit.Problems("PerkDef", defs);

        // A floor, not the list: a def added later is found too and must not break this test.
        [Test]
        public void Discovery_FindsEveryDefWithAnId()
        {
            Assert.That(DefIdAudit.Kinds.Select(k => k.type), Is.SupersetOf(new[]
            {
                typeof(GlobalUpgradeDef), typeof(PerkDef), typeof(StructureDef),
                typeof(ResourceSourceDef), typeof(UnitDef), typeof(BiomeDef),
            }));
        }

        [Test]
        public void Discovery_FollowsAnIdIntoAnInlineBlock()
        {
            Assert.That(KindOf(typeof(BiomeDef)).IdPath, Is.EqualTo("profile.id"));
            Assert.That(KindOf(typeof(PerkDef)).IdPath, Is.EqualTo("id"));
        }

        // A subclass inherits its base's id, so it is in the base's kind rather than a kind of its own:
        // ids must be unique across every perk, not within each perk class.
        [Test]
        public void Discovery_PutsSubclassesInTheirBasesKind()
        {
            var types = DefIdAudit.Kinds.Select(k => k.type).ToList();

            Assert.That(types, Has.None.EqualTo(typeof(StatPerkDef)));
            Assert.That(types, Has.None.EqualTo(typeof(UnlockStructurePerkDef)));
            Assert.That(types, Has.None.EqualTo(typeof(StatUpgradeDef)));
        }

        [Test]
        public void WellFormedIdsOfTheirOwn_PassClean()
        {
            Assert.That(Problems(("010 Good rest", "good_rest"), ("170 Geese x3", "geese_x3")), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        public void EmptyId_IsReported(string id)
        {
            var problems = Problems(("010 Good rest", "good_rest"), ("New Perk", id));

            Assert.That(problems.Select(p => p.index), Is.EqualTo(new[] { 1 }));
            StringAssert.Contains("has no id", problems[0].message);
        }

        [TestCase("Good_rest")]
        [TestCase("good-rest")]
        [TestCase("good rest")]
        [TestCase("good_rest ")]
        [TestCase("хороший_отдых")]
        public void IdOutsideTheFormat_IsReported(string id)
        {
            var problems = Problems(("010 Good rest", id));

            Assert.That(problems.Select(p => p.index), Is.EqualTo(new[] { 0 }));
            StringAssert.Contains("breaks the format", problems[0].message);
        }

        // Both holders, each naming the other: the watch reports only the def that was just saved, and
        // that may be the original as well as the copy.
        [Test]
        public void RepeatedId_IsReportedOnEveryDefThatHoldsIt()
        {
            var problems = Problems(("010 Good rest", "good_rest"), ("170 Geese x3", "geese_x3"),
                                    ("010 Good rest 1", "good_rest"));

            Assert.That(problems.Select(p => p.index), Is.EquivalentTo(new[] { 0, 2 }));
            StringAssert.Contains("'010 Good rest 1'", problems.Single(p => p.index == 0).message);
            StringAssert.Contains("'010 Good rest'", problems.Single(p => p.index == 2).message);
        }

        // The search goes by the base class; this is the proof it brings the subclasses along — without
        // them every perk would go unchecked and the project test below would pass on nothing.
        [Test]
        public void FindAll_ThroughTheBaseClass_FindsItsSubclasses()
        {
            var found = DefIdAudit.FindAll(KindOf(typeof(PerkDef))).Select(d => d.GetType()).Distinct();

            Assert.That(found, Is.SupersetOf(new[] { typeof(StatPerkDef), typeof(UnlockStructurePerkDef) }));
        }

        [Test]
        public void EveryDefInTheProject_HasAWellFormedIdOfItsOwn()
        {
            var problems = new List<string>();
            foreach (var kind in DefIdAudit.Kinds)
                problems.AddRange(DefIdAudit.Check(kind, DefIdAudit.FindAll(kind)).Select(p => p.message));

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
    }
}
