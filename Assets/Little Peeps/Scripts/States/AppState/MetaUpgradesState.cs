// Meta progression screen: spend prestige points on persistent global upgrades
public class MetaUpgradesState : IState
{
    private readonly StateMachine appFsm;
    private readonly MetaContext metaContext;
    private readonly SaveSystem saveSystem;

    public MetaUpgradesState(StateMachine appFsm, MetaContext metaContext, SaveSystem saveSystem)
    {
        this.appFsm = appFsm;
        this.metaContext = metaContext;
        this.saveSystem = saveSystem;
    }

    public void Enter()
    {
        // TODO: show meta upgrade UI; populate upgrade node graph from metaContext.globalUpgrades
    }

    public void Exit()
    {
        // TODO: hide meta upgrade UI; saveSystem.Save(metaContext)
    }

    public void Tick()
    {
        // TODO: idle — Back button calls appFsm.ChangeState(new MainMenuState(...))
    }
}
