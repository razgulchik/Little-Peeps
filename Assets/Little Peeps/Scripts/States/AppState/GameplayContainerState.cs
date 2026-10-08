using UnityEngine;

namespace LittlePeeps
{
    // Outer app state owning the inner gameplay FSM (Playing, BuildMode). Bridges the UI toggle
    // button (BuildModeToggleRequestedEvent) to the inner FSM, enforces a re-entry cooldown after
    // leaving build mode, and pushes BuildModeButtonStateEvent so the button reflects mode + cooldown.
    public class GameplayContainerState : IState
    {
        private readonly StateMachine innerFsm;
        private readonly PlayingState playingState;
        private readonly BuildModeState buildModeState;
        private readonly PerkSelectionState perkSelectionState;
        private readonly float buildModeCooldown;

        // Deps for building a ZoneSelectionState on demand (it builds the AgeTransitionState after it).
        private readonly AgeSystem ageSystem;
        private readonly AgeSequencer ageSequencer;
        private readonly ResourceSystem resourceSystem;
        private readonly IslandSystem islandSystem;
        private readonly ZoneSelectionUI zoneSelectionUI;

        // The RUN MANAGER, deliberately, not a RunContext. This state outlives the run: it is built once
        // in GameBootstrap.Awake and survives every prestige, so a captured context would be the one that
        // has already ended. That is not hypothetical — it shipped: TriggerAgeCmd incremented the dead
        // run while AgeAdvancePanel displayed the live one, so the age label froze at 0, the island kept re-applying
        // the first age's expansion and the cost never rose. Ask for CurrentRun at the moment it is used.
        private readonly RunManager runManager;

        private bool inBuildMode;
        private float cooldownRemaining;

        public GameplayContainerState(StateMachine innerFsm, PlayingState playingState,
                                      BuildModeState buildModeState, PerkSelectionState perkSelectionState,
                                      float buildModeCooldown,
                                      AgeSystem ageSystem, AgeSequencer ageSequencer,
                                      ResourceSystem resourceSystem, IslandSystem islandSystem,
                                      ZoneSelectionUI zoneSelectionUI, RunManager runManager)
        {
            this.innerFsm = innerFsm;
            this.playingState = playingState;
            this.buildModeState = buildModeState;
            this.perkSelectionState = perkSelectionState;
            this.buildModeCooldown = buildModeCooldown;
            this.ageSystem = ageSystem;
            this.ageSequencer = ageSequencer;
            this.resourceSystem = resourceSystem;
            this.islandSystem = islandSystem;
            this.zoneSelectionUI = zoneSelectionUI;
            this.runManager = runManager;
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

            TryOpenStartPerkPick();
            innerFsm.Tick();
        }

        // Meta perks can owe the run perk picks at its start (StartPerkPickUpgradeDef, StartAgeUpgradeDef).
        // Opened HERE, from normal play, rather than wherever a run is started: runs start in three places —
        // the first run in GameBootstrap, Play on the meta screen, the debug Restart Run — and this one check
        // covers all of them, and any added later. Normal play only, by the rule build mode and age advance
        // already follow: the pick freezes the game and must not cut into another mode.
        private void TryOpenStartPerkPick()
        {
            if (inBuildMode || innerFsm.Current != playingState) return;

            // Read the run HERE, not in the constructor — see the runManager field.
            var run = runManager != null ? runManager.CurrentRun : null;
            if (run == null || run.perkPicksOwed <= 0) return;

            // One pick taken off BEFORE entering, as the one this entry shows; PerkSelectionState shows the
            // rest in the same sitting, and clears them if it cannot — so a pick that skips itself (nothing
            // to offer, no screen) lands back in playing with nothing left to reopen every frame.
            run.perkPicksOwed--;
            innerFsm.ChangeState(perkSelectionState);
        }

        private void OnToggleRequested(BuildModeToggleRequestedEvent _)
        {
            if (inBuildMode) ExitBuildMode();
            else EnterBuildMode();
        }

        // Start an age only from normal play (not build mode / not mid-transition) and only when the
        // next age is actually affordable. ZoneSelectionState owns the spend and the zone pick, then hands
        // over to AgeTransitionState for the animation and the return.
        private void OnAgeAdvanceRequested(AgeAdvanceRequestedEvent _)
        {
            if (inBuildMode || innerFsm.Current != playingState) return;
            if (ageSystem == null || !ageSystem.CanAdvance) return;

            // Read the run HERE, not in the constructor — see the runManager field.
            var run = runManager != null ? runManager.CurrentRun : null;
            if (run == null) return;

            innerFsm.ChangeState(new ZoneSelectionState(
                innerFsm, playingState, perkSelectionState, ageSequencer, resourceSystem, islandSystem,
                zoneSelectionUI, run, ageSystem.NextAge));
        }

        private void EnterBuildMode()
        {
            if (cooldownRemaining > 0f) return;   // re-entry blocked during cooldown

            // Build mode is reachable only from normal play, the same rule OnAgeAdvanceRequested already
            // enforces. It needs saying twice because the toggle has a SECOND publisher: GameHotkeys sends
            // the same event from a key press, and a key press runs in Update — past every UI raycast and
            // unaffected by timeScale. Without this, pressing the build key during an age transition or a
            // perk pick dropped that state mid-flight: its Exit restored timeScale, the units were
            // despawned behind the fade, and the sequencer coroutine finished onto a state no longer on
            // the stack, so the run never returned to playing on its own.
            if (innerFsm.Current != playingState) return;

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
            EventBus<BuildModeButtonStateEvent>.Publish(new BuildModeButtonStateEvent
            {
                InBuildMode = inBuildMode,
                Interactable = inBuildMode || cooldownRemaining <= 0f
            });
        }
    }
}
