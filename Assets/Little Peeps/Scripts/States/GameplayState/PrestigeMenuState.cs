// Prestige confirmation screen: shows projected points; confirm executes prestige
public class PrestigeMenuState : IState
{
    private readonly StateMachine gameplayFsm;
    private readonly PrestigeSystem prestigeSystem;
    private readonly RunContext runContext;

    public PrestigeMenuState(StateMachine gameplayFsm, PrestigeSystem prestigeSystem, RunContext runContext)
    {
        this.gameplayFsm = gameplayFsm;
        this.prestigeSystem = prestigeSystem;
        this.runContext = runContext;
    }

    public void Enter()
    {
        // TODO: show prestige UI; display prestigeSystem.Calculate(runContext) as projected points
    }

    public void Exit()
    {
        // TODO: hide prestige UI
    }

    public void Tick()
    {
        // TODO: Confirm button → prestigeSystem.ExecutePrestige(runContext); Cancel → gameplayFsm.ChangeState(PlayingState)
    }
}
