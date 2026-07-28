namespace LittlePeeps
{
    // Perk selection overlay: player chooses 1 of 3; blocks all other gameplay
    public class PerkSelectionState : IState
    {
        private readonly StateMachine gameplayFsm;
        private readonly PerkSystem perkSystem;

        // RunManager, not RunContext: this state is built once and survives every prestige. Read
        // runManager.CurrentRun at the point of use — see GameplayContainerState for why.
        private readonly RunManager runManager;

        public PerkSelectionState(StateMachine gameplayFsm, PerkSystem perkSystem, RunManager runManager)
        {
            this.gameplayFsm = gameplayFsm;
            this.perkSystem = perkSystem;
            this.runManager = runManager;
        }

        public void Enter()
        {
            // TODO: show PerkSelectionUI with perkSystem.RollPerks(run.currentAge, run) and listen for
            //       the player's choice (mechanism not decided yet — see PerkSelectionUI). An EMPTY
            //       roll means nothing is eligible: skip the step rather than show a blank screen.
        }

        public void Exit()
        {
            // TODO: hide PerkSelectionUI; drop the choice listener set up in Enter
        }

        public void Tick()
        {
            // TODO: idle — PerkSelected handler calls perkSystem.ApplyPerk then gameplayFsm.ChangeState(PlayingState)
        }
    }
}
