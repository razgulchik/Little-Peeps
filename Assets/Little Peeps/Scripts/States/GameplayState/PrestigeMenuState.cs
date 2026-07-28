namespace LittlePeeps
{
    // Prestige confirmation screen: shows projected points; confirm executes prestige
    public class PrestigeMenuState : IState
    {
        private readonly StateMachine gameplayFsm;
        private readonly PrestigeSystem prestigeSystem;

        // RunManager, not RunContext — and here it matters most: this state is the one that ENDS the run,
        // so a captured context would be exactly the object about to be replaced. Read CurrentRun at use.
        private readonly RunManager runManager;

        public PrestigeMenuState(StateMachine gameplayFsm, PrestigeSystem prestigeSystem, RunManager runManager)
        {
            this.gameplayFsm = gameplayFsm;
            this.prestigeSystem = prestigeSystem;
            this.runManager = runManager;
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
}
