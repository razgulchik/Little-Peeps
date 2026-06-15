// First state on startup: loads save data, then transitions to MainMenu
public class BootState : IState
{
    private readonly StateMachine appFsm;
    private readonly SaveSystem saveSystem;
    private readonly MetaContext meta;

    public BootState(StateMachine appFsm, SaveSystem saveSystem, MetaContext meta)
    {
        this.appFsm = appFsm;
        this.saveSystem = saveSystem;
        this.meta = meta;
    }

    public void Enter()
    {
        // TODO: show loading screen; load MetaContext via saveSystem; transition to MainMenuState when done
    }

    public void Exit()
    {
        // TODO: hide loading screen
    }

    public void Tick()
    {
        // TODO: check async load completion; call appFsm.ChangeState(new MainMenuState(...)) when ready
    }
}
