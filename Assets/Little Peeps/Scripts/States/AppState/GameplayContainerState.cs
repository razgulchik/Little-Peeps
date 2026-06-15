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
        // TODO: innerFsm.ChangeState(playingState) — begin gameplay
    }

    public void Exit()
    {
        // TODO: clean up inner FSM; pause or destroy all gameplay objects
    }

    public void Tick()
    {
        // TODO: innerFsm.Tick()
    }
}
