using System.Collections.Generic;

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

    // Push state on top; old state Exit is called (paused, not resumed on Pop)
    public void Push(IState state)
    {
        Current?.Exit();
        stack.Push(state);
        state.Enter();
    }

    // Return to previous state
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
