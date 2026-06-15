// Normal gameplay: units bounce, resources accumulate; player can open BuildMode or trigger prestige
public class PlayingState : IState
{
    private readonly StateMachine gameplayFsm;
    private readonly RunContext runContext;

    public PlayingState(StateMachine gameplayFsm, RunContext runContext)
    {
        this.gameplayFsm = gameplayFsm;
        this.runContext = runContext;
    }

    public void Enter()
    {
        // TODO: resume physics simulation; show HUD; subscribe to build button and PrestigeTriggeredEvent
    }

    public void Exit()
    {
        // TODO: unsubscribe from button and prestige event
    }

    public void Tick()
    {
        // TODO: check age advancement condition; if met, new TriggerAgeCmd(...).Execute()
    }
}
