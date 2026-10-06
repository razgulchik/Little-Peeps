using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The build card's hint text (StructureHint). What is pinned: the wording of the count and lock lines,
    // the full price of the NEXT one with every resource, and the footer joining the two.
    //
    // The line tests are pure and run anywhere; the Fill tests need a real StructureDef, so they run in
    // the Editor's Test Runner and are skipped by the offline reflection harness.
    public class StructureHintTests
    {
        private StructureDef def;
        private HintContent hint;

        [SetUp]
        public void SetUp()
        {
            hint = new HintContent();
        }

        [TearDown]
        public void TearDown()
        {
            if (def != null) Object.DestroyImmediate(def);
        }

        private StructureDef House()
        {
            def = ScriptableObject.CreateInstance<StructureDef>();
            def.name = "House";
            def.cost = new System.Collections.Generic.List<ResourceCost>
            {
                new ResourceCost { resourceType = ResourceType.Food, amount = 10 },
                new ResourceCost { resourceType = ResourceType.Wood, amount = 20 },
            };
            return def;
        }

        // --- the lines -------------------------------------------------------------------------------

        [Test]
        public void CountLine_WithLimit_ReadsStandingOfLimit()
        {
            Assert.That(StructureHint.CountLine(true, 1, 2), Is.EqualTo("Count: 1 of 2"));
        }

        [Test]
        public void CountLine_WithoutLimit_ReadsNoLimits()
        {
            Assert.That(StructureHint.CountLine(false, 7, 0), Is.EqualTo("No limits"));
        }

        [Test]
        public void LockLine_NamesWhatOpensTheCard()
        {
            Assert.That(StructureHint.LockLine(StructureLock.Age, 2), Is.EqualTo("Opens on the Age II"));
            Assert.That(StructureHint.LockLine(StructureLock.Perk, 0), Is.EqualTo("Opens with a perk"));
        }

        [Test]
        public void LockLine_OpenOrAtLimit_IsNull()
        {
            Assert.IsNull(StructureHint.LockLine(StructureLock.None, 3));
            Assert.IsNull(StructureHint.LockLine(StructureLock.Limit, 3));
        }

        // --- Fill ------------------------------------------------------------------------------------

        [Test]
        public void Fill_TitleFallsBackToAssetName_BodyIsDescription()
        {
            House().description = "Sleeps four.";

            StructureHint.Fill(hint, def, 0, 0, StructureLock.None, null);

            Assert.That(hint.title, Is.EqualTo("House"));
            Assert.That(hint.body, Is.EqualTo("Sleeps four."));
        }

        [Test]
        public void Fill_ListsEveryResource_AtTheNextOnesPrice()
        {
            House().costStepFlat = 5;

            StructureHint.Fill(hint, def, 2, 0, StructureLock.None, null);

            Assert.That(hint.costs.Count, Is.EqualTo(2));
            Assert.That(hint.costs[0].resourceType, Is.EqualTo(ResourceType.Food));
            Assert.That(hint.costs[0].amount, Is.EqualTo(20f));   // 10 + 2 × 5
            Assert.That(hint.costs[1].resourceType, Is.EqualTo(ResourceType.Wood));
            Assert.That(hint.costs[1].amount, Is.EqualTo(30f));   // 20 + 2 × 5
            Assert.IsTrue(hint.costs[0].affordable && hint.costs[1].affordable);   // no wallet = payable
        }

        [Test]
        public void Fill_SkipsAFreeResource()
        {
            House().cost[0].amount = 0;

            StructureHint.Fill(hint, def, 0, 0, StructureLock.None, null);

            Assert.That(hint.costs.Count, Is.EqualTo(1));
            Assert.That(hint.costs[0].resourceType, Is.EqualTo(ResourceType.Wood));
        }

        [Test]
        public void Fill_FooterIsTheCount_ThenWhatOpensAShutCard()
        {
            House().maxCount = 2;
            def.requiredAge = 3;

            StructureHint.Fill(hint, def, 1, 2, StructureLock.None, null);
            Assert.That(hint.footer, Is.EqualTo("Count: 1 of 2"));

            StructureHint.Fill(hint, def, 1, 2, StructureLock.Age, null);
            Assert.That(hint.footer, Is.EqualTo("Count: 1 of 2\nOpens on the Age III"));
        }

        [Test]
        public void Fill_Refill_DropsTheOldLines()
        {
            House();
            StructureHint.Fill(hint, def, 0, 0, StructureLock.None, null);
            def.cost.RemoveAt(1);

            StructureHint.Fill(hint, def, 0, 0, StructureLock.None, null);

            Assert.That(hint.costs.Count, Is.EqualTo(1));
        }
    }
}
