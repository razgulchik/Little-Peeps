using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The zone pick freezes the game and waits for a click, so every path that reaches it without a
    // way to produce that click is a softlock. Like PerkSelectionStateTests, only the refusal paths are
    // covered offline: everything past them (the spend, the offers, the pick) wants a live
    // ResourceSystem and IslandSystem, which are MonoBehaviours. Those paths are verified by buying an
    // age in play; the offers themselves are pinned in ZoneOffersTests.
    public class ZoneSelectionStateTests
    {
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            EventBus<PrestigeTriggeredEvent>.Clear();
            EventBus<ZoneSelectedEvent>.Clear();
        }

        // Every dependency is null on purpose: the state must survive being handed nothing at all,
        // which is what an unwired scene gives it — and an age with no run or no def to buy is no age.
        private static ZoneSelectionState Build(StateMachine fsm, PlayingState playing) =>
            new ZoneSelectionState(fsm, playing, null, null, null, null, null, null, null);

        [Test]
        public void WithNothingWired_ItHandsControlBackToPlaying()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null);
            var zoneSelection = Build(fsm, playing);

            fsm.ChangeState(zoneSelection);
            Assert.That(fsm.Current, Is.SameAs(zoneSelection), "Enter must not change state from inside itself.");

            fsm.Tick();
            Assert.That(fsm.Current, Is.SameAs(playing));
        }

        // The state is already on the stack when Enter runs, so leaving from inside Enter would call
        // this object's own Exit before Enter returned.
        [Test]
        public void ItNeverLeavesFromInsideEnter()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null);

            fsm.ChangeState(Build(fsm, playing));

            Assert.That(fsm.Current, Is.Not.SameAs(playing));
        }

        // An age that never happened must not have frozen the game on its way out: timeScale 0 with no
        // screen to dismiss it is the softlock.
        [Test]
        public void AnAbortedAgeLeavesTheGameRunning()
        {
            Time.timeScale = 1f;

            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null);

            fsm.ChangeState(Build(fsm, playing));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Enter froze the game on a path it cannot finish.");

            fsm.Tick();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        // Exit restores timeScale unconditionally, so the game cannot stay frozen no matter which way
        // the state is left — including being displaced by something else.
        [Test]
        public void LeavingAlwaysUnfreezesTheGame()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null);
            var zoneSelection = Build(fsm, playing);

            fsm.ChangeState(zoneSelection);
            Time.timeScale = 0f;            // as if the pick had started normally

            fsm.ChangeState(playing);

            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }

    // The transition after the pick: it no longer buys anything, so its one job offline is to move on
    // to the perk pick when there is nothing to play, rather than wait forever on a sequencer it does
    // not have.
    public class AgeTransitionStateTests
    {
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            EventBus<PrestigeTriggeredEvent>.Clear();
            EventBus<PerkSelectedEvent>.Clear();
        }

        [Test]
        public void WithoutASequencer_ItMovesOnToThePerkPick()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null);
            var perkSelection = new PerkSelectionState(fsm, null, null, null, playing);
            var transition = new AgeTransitionState(fsm, null, perkSelection, 1, null, null);

            fsm.ChangeState(transition);
            Assert.That(fsm.Current, Is.SameAs(transition), "Enter must not change state from inside itself.");

            fsm.Tick();
            Assert.That(fsm.Current, Is.SameAs(perkSelection));
        }

        [Test]
        public void LeavingAlwaysUnfreezesTheGame()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null);
            var transition = new AgeTransitionState(fsm, null, null, 1, null, null);

            fsm.ChangeState(transition);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "the transition plays frozen");

            fsm.ChangeState(playing);

            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
