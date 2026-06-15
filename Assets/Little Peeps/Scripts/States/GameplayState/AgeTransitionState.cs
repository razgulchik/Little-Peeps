// Blocks input while AgeSequencer coroutines run; transitions back to Playing when done
public class AgeTransitionState : IState
{
    private readonly StateMachine gameplayFsm;
    private readonly AgeSequencer ageSequencer;
    private readonly int newAge;
    private readonly RunContext runContext;
    private bool transitionComplete;

    public AgeTransitionState(StateMachine gameplayFsm, AgeSequencer ageSequencer, int newAge, RunContext runContext)
    {
        this.gameplayFsm = gameplayFsm;
        this.ageSequencer = ageSequencer;
        this.newAge = newAge;
        this.runContext = runContext;
    }

    public void Enter()
    {
        // TODO: block player input; transitionComplete = false; ageSequencer.StartAgeTransition(newAge, runContext)
        // TODO: subscribe to AgeSequencer completion callback (or use a flag polled in Tick)
    }

    public void Exit()
    {
        // TODO: re-enable player input
    }

    public void Tick()
    {
        // TODO: if transitionComplete, gameplayFsm.ChangeState(new PlayingState(...))
    }
}
