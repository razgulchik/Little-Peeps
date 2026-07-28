using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The weighted draw as pure arithmetic. PickIndex is static and takes the roll as an argument
    // exactly so its boundaries can be pinned against exact values, instead of hoping a random sequence
    // eventually wanders onto them — and so this half runs in the offline harness, unlike everything
    // below that needs real assets.
    public class PerkWeightedPickTests
    {
        [Test]
        public void PickIndex_ReturnsMinusOne_WhenThereIsNothingToPick()
        {
            Assert.That(PerkSystem.PickIndex(null, 0.5f), Is.EqualTo(-1));
            Assert.That(PerkSystem.PickIndex(new List<float>(), 0.5f), Is.EqualTo(-1));
        }

        [Test]
        public void PickIndex_ReturnsTheOnlyEntry_WhateverTheRoll()
        {
            var weights = new List<float> { 7f };

            for (int i = 0; i <= 10; i++)
                Assert.That(PerkSystem.PickIndex(weights, i / 10f), Is.EqualTo(0));
        }

        [TestCase(0f, 0)]
        [TestCase(0.49f, 0)]
        [TestCase(0.5f, 1)]
        [TestCase(1f, 1)]
        public void PickIndex_SplitsEqualWeightsDownTheMiddle(float roll, int expected)
        {
            Assert.That(PerkSystem.PickIndex(new List<float> { 1f, 1f }, roll), Is.EqualTo(expected));
        }

        [TestCase(0f, 0)]
        [TestCase(0.74f, 0)]
        [TestCase(0.75f, 1)]
        [TestCase(1f, 1)]
        public void PickIndex_GivesEachEntryASliceProportionalToItsWeight(float roll, int expected)
        {
            // 3 : 1, so the first entry owns the first three quarters of the range and no more.
            Assert.That(PerkSystem.PickIndex(new List<float> { 3f, 1f }, roll), Is.EqualTo(expected));
        }

        [Test]
        public void PickIndex_SkipsNonPositiveWeights_AtEveryRoll()
        {
            // weight 0 is the "listed but not rollable" switch. It has to be unreachable, not merely
            // unlikely — a roll landing on its slice must fall through to a real entry.
            var weights = new List<float> { 0f, 1f, -2f };

            for (int i = 0; i <= 10; i++)
                Assert.That(PerkSystem.PickIndex(weights, i / 10f), Is.EqualTo(1));
        }

        [Test]
        public void PickIndex_ReturnsMinusOne_WhenNothingHasAPositiveWeight()
        {
            Assert.That(PerkSystem.PickIndex(new List<float> { 0f, 0f }, 0.5f), Is.EqualTo(-1));
            Assert.That(PerkSystem.PickIndex(new List<float> { -1f, -3f }, 0.5f), Is.EqualTo(-1));
        }

        [Test]
        public void PickIndex_AtTheTopOfTheRange_LandsOnTheLastROLLABLEEntry()
        {
            // roll01 == 1 makes target == total, which no "target < running" comparison can catch, so
            // the loop falls out of the bottom. The answer is the last entry that could be rolled — index
            // 1 here, not the unrollable index 2.
            Assert.That(PerkSystem.PickIndex(new List<float> { 1f, 1f, 0f }, 1f), Is.EqualTo(1));
        }

        [Test]
        public void PickIndex_ClampsARollFromOutsideTheUnitRange()
        {
            var weights = new List<float> { 1f, 1f };

            Assert.That(PerkSystem.PickIndex(weights, -5f), Is.EqualTo(0));
            Assert.That(PerkSystem.PickIndex(weights, 5f), Is.EqualTo(1));
        }
    }

    // RollPerks and ApplyPerk against a real catalogue. These need ScriptableObject assets and a live
    // MonoBehaviour, so they run in the Editor's Test Runner and are skipped by the offline harness.
    //
    // What they guard is the eligibility filter, because every way it can be wrong is silent: a perk
    // that never appears looks exactly like a perk the player was unlucky with.
    public class PerkSystemRollTests
    {
        private GameObject go;
        private PerkSystem system;
        private PerkCatalogueDef catalogue;
        private RunContext run;
        private readonly List<PerkDef> created = new();

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("PerkSystem");
            system = go.AddComponent<PerkSystem>();
            catalogue = ScriptableObject.CreateInstance<PerkCatalogueDef>();
            run = new RunContext();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            for (int i = 0; i < created.Count; i++)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
            if (catalogue != null) Object.DestroyImmediate(catalogue);
        }

        // Sets the private [SerializeField]s the way the inspector does. SerializedObject rather than
        // reflection on the field: it goes through Unity's own serialisation, so a renamed field blows
        // up here instead of quietly leaving the system unconfigured and the tests passing on nothing.
        private void Configure(int choicesOffered = 3)
        {
            var so = new SerializedObject(system);
            so.FindProperty("catalogue").objectReferenceValue = catalogue;
            so.FindProperty("choicesOffered").intValue = choicesOffered;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private StatPerkDef Perk(string id, float weight = 1f, int minAge = 0)
        {
            var perk = ScriptableObject.CreateInstance<StatPerkDef>();
            perk.id = id;
            perk.weight = weight;
            perk.minAge = minAge;
            created.Add(perk);
            catalogue.perks.Add(perk);
            return perk;
        }

        [Test]
        public void RollPerks_OffersThreeDistinctPerks_WhenThereAreEnough()
        {
            for (int i = 0; i < 6; i++) Perk($"perk{i}");
            Configure();

            var offer = system.RollPerks(0, run);

            Assert.That(offer.Count, Is.EqualTo(3));
            Assert.That(offer, Is.Unique);
        }

        [Test]
        public void RollPerks_OffersWhatIsLeft_WhenFewerAreEligible()
        {
            Perk("only-one");
            Configure();

            // Design decision: show fewer cards rather than pad the offer with duplicates or filler.
            Assert.That(system.RollPerks(0, run).Count, Is.EqualTo(1));
        }

        [Test]
        public void RollPerks_ReturnsAnEmptyList_WhenNothingIsEligible()
        {
            Configure();

            // The caller skips the selection step entirely on this, rather than showing a blank screen.
            Assert.That(system.RollPerks(0, run), Is.Empty);
        }

        [Test]
        public void RollPerks_WithdrawsPerksAboveTheCurrentAge()
        {
            var early = Perk("early");
            Perk("late", minAge: 5);
            Configure();

            var offer = system.RollPerks(2, run);

            Assert.That(offer, Is.EquivalentTo(new[] { early }),
                        "minAge keeps the strong perks out of the first transitions");
        }

        [Test]
        public void RollPerks_OffersAPerkOnTheExactAgeItUnlocks()
        {
            var gated = Perk("gated", minAge: 3);
            Configure();

            Assert.That(system.RollPerks(3, run), Is.EquivalentTo(new[] { gated }),
                        "minAge is the first age it appears in, not the one before it");
        }

        [Test]
        public void RollPerks_WithdrawsPerksAlreadyChosenThisRun()
        {
            var taken = Perk("taken");
            var free = Perk("free");
            run.perksChosen.Add(taken);
            Configure();

            Assert.That(system.RollPerks(0, run), Is.EquivalentTo(new[] { free }));
        }

        [Test]
        public void RollPerks_WithdrawsPerksWhoseWeightIsZero()
        {
            Perk("disabled", weight: 0f);
            var live = Perk("live");
            Configure();

            Assert.That(system.RollPerks(0, run), Is.EquivalentTo(new[] { live }),
                        "weight 0 takes a perk out of the pool without removing it from the catalogue");
        }

        [Test]
        public void RollPerks_NeverRepeatsAPerkWithinOneOffer()
        {
            // Two heavy perks and one feather: a draw WITH replacement would hand back the same heavy
            // perk twice often enough that a hundred rolls catch it, while a single roll would not.
            Perk("heavy-a", weight: 100f);
            Perk("heavy-b", weight: 100f);
            Perk("feather", weight: 0.01f);
            Configure();

            for (int i = 0; i < 100; i++)
            {
                var offer = system.RollPerks(0, run);
                Assert.That(offer.Count, Is.EqualTo(3));
                Assert.That(offer, Is.Unique, "the same perk must not fill two cards of one offer");
            }
        }

        [Test]
        public void RollPerks_HonoursTheConfiguredNumberOfChoices()
        {
            for (int i = 0; i < 6; i++) Perk($"perk{i}");
            Configure(choicesOffered: 2);

            Assert.That(system.RollPerks(0, run).Count, Is.EqualTo(2));
        }

        [Test]
        public void ApplyPerk_AppliesTheEffectAndRecordsTheChoice()
        {
            var perk = Perk("speedy");
            perk.modifiers = new List<StatModifier>
            {
                new StatModifier { id = StatId.UnitSpeed, unitScope = UnitType.Miner, percent = 0.5f },
            };
            Configure();

            system.ApplyPerk(perk, run);

            Assert.That(run.stats.Apply(2f, StatId.UnitSpeed, UnitType.Miner),
                        Is.EqualTo(3f).Within(1e-4f), "a StatPerkDef is nothing but its modifiers");
            Assert.That(run.perksChosen, Does.Contain(perk));
        }

        [Test]
        public void ApplyPerk_MakesThePerkUnrollableForTheRestOfTheRun()
        {
            var taken = Perk("taken");
            var free = Perk("free");
            Configure();

            system.ApplyPerk(taken, run);

            // The ledger ApplyPerk writes is the same one the filter reads — otherwise a perk could be
            // offered again on the next transition and stack with itself.
            Assert.That(system.RollPerks(0, run), Is.EquivalentTo(new[] { free }));
        }

        [Test]
        public void ApplyPerk_IgnoresANullPerk()
        {
            Configure();

            Assert.DoesNotThrow(() => system.ApplyPerk(null, run));
            Assert.That(run.perksChosen, Is.Empty);
        }

        [Test]
        public void RollPerks_SurvivesANullRunContext()
        {
            var perk = Perk("any");
            Configure();

            // Nothing has been chosen when there is no run to have chosen it in, so the filter passes
            // everything rather than throwing in the middle of an age transition.
            Assert.That(system.RollPerks(0, null), Is.EquivalentTo(new[] { perk }));
        }
    }
}
