// Perk selection overlay: player chooses 1 of 3; blocks all other gameplay
public class PerkSelectionState : IState
{
    private readonly StateMachine gameplayFsm;
    private readonly PerkSystem perkSystem;
    private readonly RunContext runContext;

    public PerkSelectionState(StateMachine gameplayFsm, PerkSystem perkSystem, RunContext runContext)
    {
        this.gameplayFsm = gameplayFsm;
        this.perkSystem = perkSystem;
        this.runContext = runContext;
    }

    public void Enter()
    {
        // TODO: subscribe to PerkSelectedEvent; show PerkSelectionUI with perkSystem.Roll3Perks(runContext.currentAge, runContext)
    }

    public void Exit()
    {
        // TODO: hide PerkSelectionUI; unsubscribe from PerkSelectedEvent
    }

    public void Tick()
    {
        // TODO: idle — PerkSelected handler calls perkSystem.ApplyPerk then gameplayFsm.ChangeState(PlayingState)
    }
}
