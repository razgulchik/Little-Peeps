// Transition gameplay FSM to the prestige confirmation screen
public class TriggerPrestigeCmd : ICommand
{
    private readonly StateMachine gameplayFsm;
    private readonly PrestigeSystem prestigeSystem;
    private readonly RunContext runContext;

    public TriggerPrestigeCmd(StateMachine gameplayFsm, PrestigeSystem prestigeSystem, RunContext runContext)
    {
        this.gameplayFsm = gameplayFsm;
        this.prestigeSystem = prestigeSystem;
        this.runContext = runContext;
    }

    public bool CanExecute()
    {
        // TODO: check that the prestige condition is met (e.g., a flag set when a unit reaches Pier)
        return true;
    }

    public void Execute()
    {
        // TODO: gameplayFsm.ChangeState(new PrestigeMenuState(gameplayFsm, prestigeSystem, runContext))
    }
}
