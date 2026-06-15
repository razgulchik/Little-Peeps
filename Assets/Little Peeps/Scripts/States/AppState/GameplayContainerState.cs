// Outer app state that owns the inner Gameplay FSM (Playing, BuildMode, etc.)
public class GameplayContainerState : IState
{
    private readonly StateMachine innerFsm;
    private readonly PlayingState playingState;

    public GameplayContainerState(StateMachine innerFsm, PlayingState playingState)
    {
        this.innerFsm = innerFsm;
        this.playingState = playingState;
    }

    public void Enter()
    {
        innerFsm.ChangeState(playingState);
    }

    public void Exit()
    {
        // TODO: tear down / pause the inner gameplay FSM on prestige or return to menu
    }

    public void Tick()
    {
        innerFsm.Tick();
    }
}
