// Title screen: allows starting a new run or opening Meta Upgrades
public class MainMenuState : IState
{
    private readonly StateMachine appFsm;
    private readonly RunManager runManager;

    public MainMenuState(StateMachine appFsm, RunManager runManager)
    {
        this.appFsm = appFsm;
        this.runManager = runManager;
    }

    public void Enter()
    {
        // TODO: show main menu UI; subscribe to Play button → ChangeState(GameplayContainerState) and Meta button → ChangeState(MetaUpgradesState)
    }

    public void Exit()
    {
        // TODO: hide main menu UI; unsubscribe from button callbacks
    }

    public void Tick()
    {
        // TODO: idle — all transitions are event-driven
    }
}
