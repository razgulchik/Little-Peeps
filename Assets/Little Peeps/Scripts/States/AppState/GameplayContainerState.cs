using UnityEngine;

// Outer app state owning the inner gameplay FSM (Playing, BuildMode). Bridges the UI toggle
// button (BuildModeToggleRequestedEvent) to the inner FSM, enforces a re-entry cooldown after
// leaving build mode, and pushes BuildModeUIStateEvent so the button reflects mode + cooldown.
public class GameplayContainerState : IState
{
    private readonly StateMachine innerFsm;
    private readonly PlayingState playingState;
    private readonly BuildModeState buildModeState;
    private readonly float buildModeCooldown;

    // Deps for building an AgeTransitionState on demand.
    private readonly AgeSystem ageSystem;
    private readonly AgeSequencer ageSequencer;
    private readonly ResourceSystem resourceSystem;
    private readonly RunContext runContext;

    private bool inBuildMode;
    private float cooldownRemaining;

    public GameplayContainerState(StateMachine innerFsm, PlayingState playingState,
                                  BuildModeState buildModeState, float buildModeCooldown,
                                  AgeSystem ageSystem, AgeSequencer ageSequencer,
                                  ResourceSystem resourceSystem, RunContext runContext)
    {
        this.innerFsm = innerFsm;
        this.playingState = playingState;
        this.buildModeState = buildModeState;
        this.buildModeCooldown = buildModeCooldown;
        this.ageSystem = ageSystem;
        this.ageSequencer = ageSequencer;
        this.resourceSystem = resourceSystem;
        this.runContext = runContext;
    }

    public void Enter()
    {
        inBuildMode = false;
        cooldownRemaining = 0f;
        innerFsm.ChangeState(playingState);
        EventBus<BuildModeToggleRequestedEvent>.Subscribe(OnToggleRequested);
        EventBus<AgeAdvanceRequestedEvent>.Subscribe(OnAgeAdvanceRequested);
    }

    public void Exit()
    {
        EventBus<BuildModeToggleRequestedEvent>.Unsubscribe(OnToggleRequested);
        EventBus<AgeAdvanceRequestedEvent>.Unsubscribe(OnAgeAdvanceRequested);
        // Safety: never leave the game frozen if we tear down mid-build-mode.
        if (inBuildMode) Time.timeScale = 1f;
    }

    public void Tick()
    {
        // Cooldown runs on unscaled time (gameplay is at timeScale 1 here, but stay robust).
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining -= Time.unscaledDeltaTime;
            if (cooldownRemaining <= 0f)
            {
                cooldownRemaining = 0f;
                PublishUIState();   // cooldown ended → re-enable the button
            }
        }

        innerFsm.Tick();
    }

    private void OnToggleRequested(BuildModeToggleRequestedEvent _)
    {
        if (inBuildMode) ExitBuildMode();
        else EnterBuildMode();
    }

    // Start an age transition only from normal play (not build mode / not mid-transition) and only
    // when the next age is actually affordable. AgeTransitionState owns spend + animation + return.
    private void OnAgeAdvanceRequested(AgeAdvanceRequestedEvent _)
    {
        if (inBuildMode || innerFsm.Current != playingState) return;
        if (ageSystem == null || !ageSystem.CanAdvance) return;

        innerFsm.ChangeState(new AgeTransitionState(
            innerFsm, ageSequencer, playingState, resourceSystem, runContext, ageSystem.NextAge));
    }

    private void EnterBuildMode()
    {
        if (cooldownRemaining > 0f) return;   // re-entry blocked during cooldown
        inBuildMode = true;
        innerFsm.ChangeState(buildModeState);
        PublishUIState();
    }

    private void ExitBuildMode()
    {
        inBuildMode = false;
        innerFsm.ChangeState(playingState);
        cooldownRemaining = buildModeCooldown;   // block re-entry for the cooldown window
        PublishUIState();
    }

    private void PublishUIState()
    {
        EventBus<BuildModeUIStateEvent>.Publish(new BuildModeUIStateEvent
        {
            InBuildMode = inBuildMode,
            Interactable = inBuildMode || cooldownRemaining <= 0f
        });
    }
}
