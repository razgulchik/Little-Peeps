using NUnit.Framework;
using UnityEngine;

namespace LittlePeeps.Tests
{
    // "End this run?" freezes the game and waits for an answer, so — like the perk and zone picks — any
    // path that reaches it without a way to answer is a softlock. And any path that ends the run without
    // an answer is worse: the one thing the player cannot take back, done for them. The state meets both
    // by refusing to start, and THAT is what is pinned here.
    //
    // Only the refusal paths are covered, for PerkSelectionStateTests' reason: everything past them (the
    // payout in the question, Yes ending the run) hangs off RunManager.CurrentRun, which only StartNewRun
    // fills. Those paths are verified by prestiging in play.
    public class PrestigeMenuStateTests
    {
        private float originalTimeScale;
        private GameObject dialogObject;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            if (dialogObject != null) Object.DestroyImmediate(dialogObject);

            // PlayingState.Enter subscribes; a state left current at the end of a test would otherwise
            // leak its handler into the next one.
            EventBus<PrestigeTriggeredEvent>.Clear();
        }

        private ConfirmDialog NewDialog()
        {
            dialogObject = new GameObject("ConfirmDialog", typeof(RectTransform), typeof(CanvasGroup));
            return dialogObject.AddComponent<ConfirmDialog>();
        }

        // No prestige system and no run manager on purpose rather than stubs: the state must survive being
        // handed nothing at all, which is what an unwired scene gives it.
        private static PrestigeMenuState Build(StateMachine fsm, PlayingState playing, ConfirmDialog dialog = null) =>
            new PrestigeMenuState(fsm, null, null, dialog, playing, null);

        [Test]
        public void WithNothingWired_ItHandsControlBackToPlaying()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null, null, null);
            var menu = Build(fsm, playing);

            fsm.ChangeState(menu);
            Assert.That(fsm.Current, Is.SameAs(menu), "Enter must not change state from inside itself.");

            fsm.Tick();
            Assert.That(fsm.Current, Is.SameAs(playing));
        }

        [Test]
        public void ItNeverLeavesFromInsideEnter()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null, null, null);

            fsm.ChangeState(Build(fsm, playing));

            Assert.That(fsm.Current, Is.Not.SameAs(playing));
        }

        // The softlock itself: timeScale 0 with no question on screen to dismiss it.
        [Test]
        public void AQuestionItCannotAsk_LeavesTheGameRunning()
        {
            Time.timeScale = 1f;

            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null, null, null);

            fsm.ChangeState(Build(fsm, playing, NewDialog()));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Enter froze the game on a path it cannot finish.");

            fsm.Tick();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        // No run that can prestige, no question — the dialog is never put up for nothing.
        [Test]
        public void WithNoRun_NothingIsAsked()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null, null, null);
            var dialog = NewDialog();

            fsm.ChangeState(Build(fsm, playing, dialog));
            fsm.Tick();

            Assert.That(dialog.IsOpen, Is.False);
        }

        [Test]
        public void LeavingAlwaysUnfreezesTheGame()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null, null, null);
            var menu = Build(fsm, playing);

            fsm.ChangeState(menu);
            Time.timeScale = 0f;            // as if the question had gone up normally

            fsm.ChangeState(playing);

            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        // Displaced without an answer, the state takes its question with it: a late click must not call
        // into a state that is no longer current — for Yes, that would end a run from outside the FSM.
        [Test]
        public void LeavingTakesTheQuestionWithIt()
        {
            var fsm = new StateMachine();
            var playing = new PlayingState(fsm, null, null, null, null);
            var dialog = NewDialog();
            var menu = Build(fsm, playing, dialog);
            bool answered = false;

            fsm.ChangeState(menu);
            dialog.Show("End this run?", () => answered = true, null);   // as if Enter had asked

            fsm.ChangeState(playing);
            dialog.Confirm();

            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(answered, Is.False);
        }
    }

    // The words of the question. A class of its own because these are pure strings: without the timeScale
    // SetUp above they also run in the offline harness, where touching Time throws.
    public class PrestigeQuestionTests
    {
        [Test]
        public void Question_NamesThePayout()
        {
            Assert.That(PrestigeMenuState.Question(3), Does.Contain("+3"));
        }

        [Test]
        public void Question_NeverOffersPlusZeroAsAReward()
        {
            Assert.That(PrestigeMenuState.Question(0), Does.Not.Contain("+0"));
        }
    }
}
