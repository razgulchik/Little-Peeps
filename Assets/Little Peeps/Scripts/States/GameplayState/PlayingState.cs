namespace LittlePeeps
{
    // Normal gameplay: units bounce, resources accumulate; player can open BuildMode or trigger prestige
    public class PlayingState : IState
    {
        private readonly StateMachine gameplayFsm;

        // RunManager, not RunContext: this state is built once and survives every prestige, so a captured
        // context would go stale the first time the player restarts. Read runManager.CurrentRun at the
        // point of use. See GameplayContainerState for the bug this rule came from.
        private readonly RunManager runManager;

        public PlayingState(StateMachine gameplayFsm, RunManager runManager)
        {
            this.gameplayFsm = gameplayFsm;
            this.runManager = runManager;
        }

        public void Enter()
        {
            // TODO: show HUD; subscribe to PrestigeTriggeredEvent (build-mode toggle is owned by GameplayContainerState)
        }

        public void Exit()
        {
            // TODO: unsubscribe from the prestige event
        }

        public void Tick()
        {
            // TODO: check age advancement condition; if met, new TriggerAgeCmd(...).Execute()
        }
    }
}
