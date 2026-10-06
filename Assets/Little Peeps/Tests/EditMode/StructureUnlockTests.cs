using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The build palette's lock rule (StructureDef.LockState) and the perk that opens a perk-locked
    // building (UnlockStructurePerkDef). The rule has two gates — the age and the perk — and the part
    // worth pinning is how they combine: a perk-locked building must stay shut when its age comes, and
    // an opened one must still wait for its age.
    //
    // Needs real StructureDef instances, so these run in the Editor's Test Runner and are skipped by the
    // offline reflection harness.
    public class StructureUnlockTests
    {
        private StructureDef paddy;
        private UnlockStructurePerkDef perk;

        [SetUp]
        public void SetUp()
        {
            paddy = ScriptableObject.CreateInstance<StructureDef>();
            paddy.name = "RicePaddy";
            perk = ScriptableObject.CreateInstance<UnlockStructurePerkDef>();
            perk.structure = paddy;
        }

        [TearDown]
        public void TearDown()
        {
            if (paddy != null) Object.DestroyImmediate(paddy);
            if (perk != null) Object.DestroyImmediate(perk);
        }

        // --- the lock rule ---------------------------------------------------------------------------

        [Test]
        public void APlainStructure_IsOpen()
        {
            Assert.That(paddy.LockState(0, new RunContext()), Is.EqualTo(StructureLock.None));
            Assert.That(paddy.LockState(0, null), Is.EqualTo(StructureLock.None));
        }

        [Test]
        public void TheAgeGate_HoldsUntilItsAge()
        {
            paddy.requiredAge = 2;

            // currentAge counts transitions: 0 is the Age I, 1 the Age II — the number the lock text prints.
            Assert.That(paddy.LockState(0, new RunContext()), Is.EqualTo(StructureLock.Age));
            Assert.That(paddy.LockState(1, new RunContext()), Is.EqualTo(StructureLock.None));
        }

        [Test]
        public void TheAgeGate_OfZeroOrOne_IsOpenFromTheStart()
        {
            paddy.requiredAge = 1;
            Assert.That(paddy.LockState(0, new RunContext()), Is.EqualTo(StructureLock.None));

            paddy.requiredAge = 0;
            Assert.That(paddy.LockState(0, new RunContext()), Is.EqualTo(StructureLock.None));
        }

        [Test]
        public void APerkLockedStructure_StaysShut_WhateverTheAge()
        {
            paddy.lockedUntilPerk = true;

            Assert.That(paddy.LockState(9, new RunContext()), Is.EqualTo(StructureLock.Perk));
            Assert.That(paddy.LockState(9, null), Is.EqualTo(StructureLock.Perk), "no run has opened nothing");
        }

        [Test]
        public void APerkLockedStructure_OpensOnceTheRunHasUnlockedIt()
        {
            paddy.lockedUntilPerk = true;
            var run = new RunContext();
            run.unlockedStructures.Add(paddy);

            Assert.That(paddy.LockState(0, run), Is.EqualTo(StructureLock.None));
        }

        [Test]
        public void ThePerkLock_IsTheOneReported_WhenBothGatesHold()
        {
            paddy.lockedUntilPerk = true;
            paddy.requiredAge = 3;

            // "Open on the Age III" would be a promise the game does not keep: the age alone won't open it.
            Assert.That(paddy.LockState(0, new RunContext()), Is.EqualTo(StructureLock.Perk));
        }

        [Test]
        public void AnUnlockedStructure_StillWaitsForItsAge()
        {
            paddy.lockedUntilPerk = true;
            paddy.requiredAge = 3;
            var run = new RunContext();
            run.unlockedStructures.Add(paddy);

            Assert.That(paddy.LockState(1, run), Is.EqualTo(StructureLock.Age));
            Assert.That(paddy.LockState(2, run), Is.EqualTo(StructureLock.None));
        }

        // --- the perk --------------------------------------------------------------------------------

        [Test]
        public void ThePerk_OpensItsStructure_ForThatRunOnly()
        {
            paddy.lockedUntilPerk = true;
            var run = new RunContext();

            perk.ApplyPerk(run);

            Assert.That(paddy.LockState(0, run), Is.EqualTo(StructureLock.None));
            Assert.That(paddy.LockState(0, new RunContext()), Is.EqualTo(StructureLock.Perk),
                        "a fresh run (prestige) starts shut again");
        }

        [Test]
        public void APerkWithNoStructure_IsInert()
        {
            perk.structure = null;
            var run = new RunContext();

            Assert.DoesNotThrow(() => perk.ApplyPerk(run));
            Assert.DoesNotThrow(() => perk.ApplyPerk(null));
            Assert.That(run.unlockedStructures, Is.Empty);
            Assert.That(perk.GeneratedDescription, Is.Empty);
        }

        [Test]
        public void ThePerk_NamesItsStructure_ByDisplayName_ElseByAsset()
        {
            Assert.That(perk.GeneratedDescription, Is.EqualTo("UNLOCKS RICEPADDY"));

            paddy.displayName = "Rice Paddy";
            Assert.That(perk.GeneratedDescription, Is.EqualTo("UNLOCKS RICE PADDY"));
            Assert.That(perk.DescriptionText, Is.EqualTo("UNLOCKS RICE PADDY"), "an empty override follows it");
        }
    }
}
