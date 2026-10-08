using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // The meta screen freezes the game and waits for Play, so a path that reaches it without a screen to
    // press Play on would be a softlock — and this state comes AFTER a cash-in, so its way out must still
    // start the next run rather than return to the one already paid for. Only the refusal paths are pinned,
    // for PerkSelectionStateTests' reason: the rest wants a live RunManager. Verified by prestiging in play.
    public class MetaUpgradesStateTests
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
        }

        private static MetaUpgradesState Build(StateMachine fsm, PlayingState playing) =>
            new MetaUpgradesState(fsm, null, null, null, playing);

        [Test]
        public void WithNoScreen_ItHandsControlBackToPlaying()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null, null, null);
            var meta = Build(fsm, playing);

            fsm.ChangeState(meta);
            Assert.That(fsm.Current, Is.SameAs(meta), "Enter must not change state from inside itself.");

            fsm.Tick();
            Assert.That(fsm.Current, Is.SameAs(playing));
        }

        [Test]
        public void WithNoScreen_TheGameIsNeverFrozen()
        {
            Time.timeScale = 1f;

            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null, null, null);

            fsm.ChangeState(Build(fsm, playing));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Enter froze the game with no screen to press Play on.");

            fsm.Tick();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void LeavingAlwaysUnfreezesTheGame()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null, null, null);

            fsm.ChangeState(Build(fsm, playing));
            Time.timeScale = 0f;            // as if the screen had gone up normally

            fsm.ChangeState(playing);

            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
