// Build mode: physics paused, building tray visible, DragController active
public class BuildModeState : IState
{
    private readonly StateMachine gameplayFsm;
    private readonly DragController dragController;

    public BuildModeState(StateMachine gameplayFsm, DragController dragController)
    {
        this.gameplayFsm = gameplayFsm;
        this.dragController = dragController;
    }

    public void Enter()
    {
        // TODO: Physics2D.simulationMode = SimulationMode2D.Script (pause); show building tray; enable drag input
    }

    public void Exit()
    {
        // TODO: Physics2D.simulationMode = SimulationMode2D.FixedUpdate (resume); hide tray; disable drag input
    }

    public void Tick()
    {
        // TODO: read mouse position; on click pickup/drop forward to dragController; on Escape call dragController.OnCancel, ChangeState(PlayingState)
    }
}
