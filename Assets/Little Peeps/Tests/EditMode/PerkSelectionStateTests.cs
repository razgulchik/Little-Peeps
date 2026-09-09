using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The perk pick freezes the game and waits for a click, so every path that reaches it without a way
    // to produce that click is a softlock: the player sits on a frozen island with no screen and no way
    // out. PerkSelectionState answers that by refusing to start — and THAT is what is pinned here.
    //
    // Only the refusal paths are covered offline, because they are the ones needing no Unity plumbing.
    // Everything past them (a real roll, applying the pick, the return to play) hangs off
    // RunManager.CurrentRun, which has a private setter and is only ever filled by StartNewRun — a call
    // that wants five live systems. Reaching it from here would mean either reflection into an auto-
    // property's backing field or a fake run manager, and both would pin the test to the plumbing rather
    // than to the behaviour. Those paths are verified by playing an age transition.
    public class PerkSelectionStateTests
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

            // PlayingState.Enter subscribes; a state left current at the end of a test would otherwise
            // leak its handler into the next one.
            EventBus<PrestigeTriggeredEvent>.Clear();
            EventBus<PerkSelectedEvent>.Clear();
        }

        // Both dependencies are passed as null on purpose rather than stubbed: the point is that the
        // state survives being handed nothing at all, which is what an unwired scene gives it.
        private static PerkSelectionState Build(StateMachine fsm, PlayingState playing) =>
            new PerkSelectionState(fsm, null, null, null, playing);

        [Test]
        public void WithNothingWired_ItHandsControlBackToPlaying()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null);
            var perkSelection = Build(fsm, playing);

            fsm.ChangeState(perkSelection);
            Assert.That(fsm.Current, Is.SameAs(perkSelection), "Enter must not change state from inside itself.");

            fsm.Tick();
            Assert.That(fsm.Current, Is.SameAs(playing));
        }

        // The whole reason the exit is deferred to Tick: the state is already on the stack when Enter
        // runs, so leaving from inside Enter would call this object's own Exit before Enter returned.
        [Test]
        public void ItNeverLeavesFromInsideEnter()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null);

            fsm.ChangeState(Build(fsm, playing));

            Assert.That(fsm.Current, Is.Not.SameAs(playing));
        }

        // A step that cannot run must not have frozen the game on its way out. This is the actual
        // softlock: timeScale 0 with no screen to dismiss it.
        [Test]
        public void ASkippedPickLeavesTheGameRunning()
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
            var perkSelection = Build(fsm, playing);

            fsm.ChangeState(perkSelection);
            Time.timeScale = 0f;            // as if the pick had started normally

            fsm.ChangeState(playing);

            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
