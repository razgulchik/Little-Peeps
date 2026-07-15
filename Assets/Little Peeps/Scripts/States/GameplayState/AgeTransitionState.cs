using UnityEngine;

// Inner gameplay state that owns one age advance: spends + applies the age (TriggerAgeCmd), freezes
// the game, plays the AgeSequencer transition, then returns to PlayingState when it completes.
//
// Input block: timeScale 0 stops the sim AND makes TapSystem ignore world clicks (it early-returns at
// timeScale 0), so pier/boost taps can't fire mid-transition — no UI-raycast juggling needed. The
// sequencer runs on unscaled time, so the fade/banner still animate while frozen.
public class AgeTransitionState : IState
{
    private readonly StateMachine gameplayFsm;
    private readonly AgeSequencer ageSequencer;
    private readonly PlayingState playingState;
    private readonly ResourceSystem resourceSystem;
    private readonly RunContext runContext;
    private readonly AgeDef ageDef;

    private bool complete;

    public AgeTransitionState(StateMachine gameplayFsm, AgeSequencer ageSequencer, PlayingState playingState,
                              ResourceSystem resourceSystem, RunContext runContext, AgeDef ageDef)
    {
        this.gameplayFsm = gameplayFsm;
        this.ageSequencer = ageSequencer;
        this.playingState = playingState;
        this.resourceSystem = resourceSystem;
        this.runContext = runContext;
        this.ageDef = ageDef;
    }

    public void Enter()
    {
        complete = false;

        var cmd = new TriggerAgeCmd(resourceSystem, runContext, ageDef);
        if (!cmd.CanExecute())
        {
            // Cost changed between the button press and here — bail straight back to playing.
            complete = true;
            return;
        }

        cmd.Execute();                       // spend + currentAge++ + apply modifiers
        Time.timeScale = 0f;                 // freeze + block world input for the transition
        ageSequencer.StartAgeTransition(runContext.currentAge, ageDef, runContext, () => complete = true);
    }

    public void Exit()
    {
        Time.timeScale = 1f;
    }

    public void Tick()
    {
        if (complete) gameplayFsm.ChangeState(playingState);
    }
}
