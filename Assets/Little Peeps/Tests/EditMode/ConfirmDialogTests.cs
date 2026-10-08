using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LittlePeeps.Tests
{
    // The dialog's whole contract is about WHEN the caller hears back: exactly once, and only after the
    // dialog has closed. Both halves are one-line mistakes that look harmless in a diff — call before
    // closing and a callback that asks the next question gets it closed under it; forget to drop the
    // callbacks and a double click ends a run twice, or a late click calls into a state already left.
    //
    // The buttons are not involved: they only forward to Confirm/Cancel, and Edit Mode never runs the
    // OnEnable that wires them. Native Unity objects throughout, so the offline harness skips these;
    // they run in the Test Runner.
    public class ConfirmDialogTests
    {
        private GameObject go;
        private ConfirmDialog dialog;
        private CanvasGroup group;
        private int yes;
        private int no;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("ConfirmDialog", typeof(RectTransform), typeof(CanvasGroup));
            group = go.GetComponent<CanvasGroup>();
            dialog = go.AddComponent<ConfirmDialog>();
            yes = 0;
            no = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        private bool Ask() => dialog.Show("Sure?", () => yes++, () => no++);

        [Test]
        public void Show_PutsTheQuestionOnScreen()
        {
            Assert.That(Ask(), Is.True);

            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.interactable, Is.True);
            Assert.That(group.blocksRaycasts, Is.True, "the backdrop must swallow clicks while asking");
        }

        [Test]
        public void Confirm_CallsYesOnce_AndClosesTheDialog()
        {
            Ask();

            dialog.Confirm();

            Assert.That(yes, Is.EqualTo(1));
            Assert.That(no, Is.EqualTo(0));
            Assert.That(dialog.IsOpen, Is.False);
            Assert.That(group.blocksRaycasts, Is.False, "a closed dialog must not leave a dead rectangle");
        }

        [Test]
        public void Cancel_CallsNoOnce()
        {
            Ask();

            dialog.Cancel();

            Assert.That(no, Is.EqualTo(1));
            Assert.That(yes, Is.EqualTo(0));
        }

        // A double click, or both buttons in the same frame: the first answer is the answer.
        [Test]
        public void OnlyTheFirstAnswerCounts()
        {
            Ask();

            dialog.Confirm();
            dialog.Confirm();
            dialog.Cancel();

            Assert.That(yes, Is.EqualTo(1));
            Assert.That(no, Is.EqualTo(0));
        }

        // What a state's Exit relies on: once it has hidden the dialog, nothing can call back into it.
        [Test]
        public void AnAnswerAfterHide_ReachesNobody()
        {
            Ask();

            dialog.Hide();
            dialog.Confirm();
            dialog.Cancel();

            Assert.That(yes, Is.EqualTo(0));
            Assert.That(no, Is.EqualTo(0));
        }

        [Test]
        public void TheDialogIsClosedBeforeTheAnswerRuns()
        {
            bool openDuringCallback = true;
            dialog.Show("Sure?", () => openDuringCallback = dialog.IsOpen, null);

            dialog.Confirm();

            Assert.That(openDuringCallback, Is.False);
        }

        // The reason for closing first: an answer may ask the next question, and it must stay up.
        [Test]
        public void AQuestionAskedFromAnAnswer_StaysOpen()
        {
            dialog.Show("First?", () => dialog.Show("Second?", () => yes++, null), null);

            dialog.Confirm();
            Assert.That(dialog.IsOpen, Is.True);

            dialog.Confirm();
            Assert.That(yes, Is.EqualTo(1));
        }

        [Test]
        public void AMissingCallback_IsJustNoAnswer()
        {
            dialog.Show("Sure?", null, null);

            Assert.DoesNotThrow(() => dialog.Cancel());
            Assert.That(dialog.IsOpen, Is.False);
        }

        [Test]
        public void AskingAgainWhileOpen_ReplacesTheQuestion()
        {
            Ask();
            int second = 0;

            LogAssert.Expect(LogType.Warning, new Regex("still open"));
            dialog.Show("Really?", () => second++, null);
            dialog.Confirm();

            Assert.That(second, Is.EqualTo(1));
            Assert.That(yes, Is.EqualTo(0), "the replaced question must not be answered too");
        }

        // An inactive object cannot show anything, so the caller is told — and must not freeze the game
        // around a question nobody can see.
        [Test]
        public void AnInactiveDialog_RefusesTheQuestion()
        {
            go.SetActive(false);

            LogAssert.Expect(LogType.Error, new Regex("inactive"));
            Assert.That(Ask(), Is.False);
            Assert.That(dialog.IsOpen, Is.False);
        }
    }
}
