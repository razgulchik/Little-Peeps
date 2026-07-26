using System.Collections.Generic;
using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // StateMachine is a stack of IStates. What matters is the ORDER of the lifecycle calls across
    // states — "Exit the old one before Entering the new one" cannot be seen from per-state flags, only
    // from a shared log, which is why every state here writes into one.
    public class StateMachineTests
    {
        // Writes "<name>.Enter" / ".Exit" / ".Tick" into a log shared by all states in a test.
        private class RecordingState : IState
        {
            private readonly List<string> log;
            private readonly string name;

            public RecordingState(List<string> log, string name)
            {
                this.log = log;
                this.name = name;
            }

            public void Enter() => log.Add(name + ".Enter");
            public void Exit() => log.Add(name + ".Exit");
            public void Tick() => log.Add(name + ".Tick");
        }

        private List<string> log;
        private StateMachine machine;
        private RecordingState a, b, c;

        [SetUp]
        public void SetUp()
        {
            log = new List<string>();
            machine = new StateMachine();
            a = new RecordingState(log, "A");
            b = new RecordingState(log, "B");
            c = new RecordingState(log, "C");
        }

        // --- ChangeState ---------------------------------------------------------------------------

        [Test]
        public void ChangeState_OnAnEmptyMachine_JustEntersTheNewState()
        {
            machine.ChangeState(a);

            Assert.AreEqual(new[] { "A.Enter" }, log);
            Assert.AreSame(a, machine.Current);
        }

        [Test]
        public void ChangeState_ExitsTheOldStateBeforeEnteringTheNewOne()
        {
            machine.ChangeState(a);
            log.Clear();

            machine.ChangeState(b);

            // Order is the whole point: an Enter that ran before the old state's Exit would let two
            // states own the same resources for an instant.
            Assert.AreEqual(new[] { "A.Exit", "B.Enter" }, log);
            Assert.AreSame(b, machine.Current);
        }

        [Test]
        public void ChangeState_ReplacesTheTopOfTheStack_WithoutBuryingIt()
        {
            machine.Push(a);
            machine.Push(b);
            machine.ChangeState(c);   // replaces B, does not stack on top of it
            log.Clear();

            machine.Pop();

            Assert.AreSame(a, machine.Current, "popping C must reveal A, not the replaced B");
            Assert.AreEqual(new[] { "C.Exit", "A.Enter" }, log);
        }

        // --- Push / Pop ----------------------------------------------------------------------------

        [Test]
        public void Push_PausesTheStateBelowAndEntersTheNewOne()
        {
            machine.Push(a);
            log.Clear();

            machine.Push(b);

            Assert.AreEqual(new[] { "A.Exit", "B.Enter" }, log);
            Assert.AreSame(b, machine.Current);
        }

        [Test]
        public void Pop_ExitsTheTopAndResumesTheStateBelowWithAFreshEnter()
        {
            machine.Push(a);
            machine.Push(b);
            log.Clear();

            machine.Pop();

            // The revealed state gets Enter a SECOND time — states must be written to tolerate that.
            Assert.AreEqual(new[] { "B.Exit", "A.Enter" }, log);
            Assert.AreSame(a, machine.Current);
        }

        [Test]
        public void Pop_OfTheLastState_LeavesTheMachineEmpty()
        {
            machine.Push(a);
            log.Clear();

            machine.Pop();

            Assert.AreEqual(new[] { "A.Exit" }, log, "nothing below, so nothing is resumed");
            Assert.IsNull(machine.Current);
        }

        [Test]
        public void Pop_OnAnEmptyStack_DoesNothing()
        {
            Assert.DoesNotThrow(() => machine.Pop());

            Assert.IsNull(machine.Current);
            Assert.IsEmpty(log);
        }

        [Test]
        public void PushAndPop_RestoreTheStackToItsPreviousState()
        {
            machine.Push(a);
            machine.Push(b);
            machine.Pop();
            log.Clear();

            machine.Pop();

            Assert.AreEqual(new[] { "A.Exit" }, log, "only A was left, so the stack is now empty");
            Assert.IsNull(machine.Current);
        }

        // --- Tick ----------------------------------------------------------------------------------

        [Test]
        public void Tick_ReachesTheTopStateOnly()
        {
            machine.Push(a);
            machine.Push(b);
            log.Clear();

            machine.Tick();

            Assert.AreEqual(new[] { "B.Tick" }, log, "a paused state must not keep ticking");
        }

        [Test]
        public void Tick_OnAnEmptyStack_DoesNothing()
        {
            Assert.DoesNotThrow(() => machine.Tick());
            Assert.IsEmpty(log);
        }

        [Test]
        public void Current_IsNullBeforeAnyStateIsSet()
        {
            Assert.IsNull(machine.Current);
        }
    }
}
