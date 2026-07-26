using System.Collections.Generic;

namespace LittlePeeps
{
    public interface IState
    {
        void Enter();
        void Exit();
        void Tick();
    }

    public class StateMachine
    {
        private readonly Stack<IState> stack = new();

        public IState Current => stack.Count > 0 ? stack.Peek() : null;

        // Replace top state: Exit old, Enter new
        public void ChangeState(IState newState)
        {
            if (stack.Count > 0)
            {
                stack.Pop().Exit();
            }
            stack.Push(newState);
            newState.Enter();
        }

        // Push state on top. The state below stays on the stack but is paused with Exit; Pop later
        // resumes it with a fresh Enter, so a state must tolerate its Enter/Exit pair running more
        // than once over its lifetime.
        public void Push(IState state)
        {
            Current?.Exit();
            stack.Push(state);
            state.Enter();
        }

        // Exit the top state and resume the one below it with a fresh Enter. No-op on an empty stack.
        public void Pop()
        {
            if (stack.Count == 0) return;
            stack.Pop().Exit();
            Current?.Enter();
        }

        // Forward update to top state
        public void Tick()
        {
            Current?.Tick();
        }
    }
}
