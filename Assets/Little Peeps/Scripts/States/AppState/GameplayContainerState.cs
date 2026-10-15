namespace LittlePeeps
{
    // Outer app state owning the inner gameplay FSM: normal play (PlayingState — building included, the
    // build panel is always up there) and the screens opened over it, the age advance and the perk picks
    // owed at a run's start.
    public class GameplayContainerState : IState
    {
        private readonly StateMachine innerFsm;
        private readonly PlayingState playingState;
        private readonly PerkSelectionState perkSelectionState;

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

        public GameplayContainerState(StateMachine innerFsm, PlayingState playingState,
                                      PerkSelectionState perkSelectionState,
                                      AgeSystem ageSystem, AgeSequencer ageSequencer,
                                      ResourceSystem resourceSystem, IslandSystem islandSystem,
                                      ZoneSelectionUI zoneSelectionUI, RunManager runManager)
        {
            this.innerFsm = innerFsm;
            this.playingState = playingState;
            this.perkSelectionState = perkSelectionState;
            this.ageSystem = ageSystem;
            this.ageSequencer = ageSequencer;
            this.resourceSystem = resourceSystem;
            this.islandSystem = islandSystem;
            this.zoneSelectionUI = zoneSelectionUI;
            this.runManager = runManager;
        }

        public void Enter()
        {
            innerFsm.ChangeState(playingState);
            EventBus<AgeAdvanceRequestedEvent>.Subscribe(OnAgeAdvanceRequested);
        }

        public void Exit()
        {
            EventBus<AgeAdvanceRequestedEvent>.Unsubscribe(OnAgeAdvanceRequested);
        }

        public void Tick()
        {
            TryOpenStartPerkPick();
            innerFsm.Tick();
        }

        // Meta perks can owe the run perk picks at its start (StartPerkPickUpgradeDef, StartAgeUpgradeDef).
        // Opened HERE, from normal play, rather than wherever a run is started: runs start in three places —
        // the first run in GameBootstrap, Play on the meta screen, the debug Restart Run — and this one check
        // covers all of them, and any added later. Normal play only, by the rule the age advance follows
        // too: the pick freezes the game and must not cut into another screen. Leaving normal play drops
        // whatever build tool was in hand (PlayingState.Exit).
        private void TryOpenStartPerkPick()
        {
            if (innerFsm.Current != playingState) return;

            // Read the run HERE, not in the constructor — see the runManager field.
            var run = runManager != null ? runManager.CurrentRun : null;
            if (run == null || run.perkPicksOwed <= 0) return;

            // One pick taken off BEFORE entering, as the one this entry shows; PerkSelectionState shows the
            // rest in the same sitting, and clears them if it cannot — so a pick that skips itself (nothing
            // to offer, no screen) lands back in playing with nothing left to reopen every frame.
            run.perkPicksOwed--;
            innerFsm.ChangeState(perkSelectionState);
        }

        // Start an age only from normal play (not mid-transition, not over another screen) and only when
        // the next age is actually affordable. ZoneSelectionState owns the spend and the zone pick, then
        // hands over to AgeTransitionState for the animation and the return.
        private void OnAgeAdvanceRequested(AgeAdvanceRequestedEvent _)
        {
            if (innerFsm.Current != playingState) return;
            if (ageSystem == null || !ageSystem.CanAdvance) return;

            // Read the run HERE, not in the constructor — see the runManager field.
            var run = runManager != null ? runManager.CurrentRun : null;
            if (run == null) return;

            innerFsm.ChangeState(new ZoneSelectionState(
                innerFsm, playingState, perkSelectionState, ageSequencer, resourceSystem, islandSystem,
                zoneSelectionUI, run, ageSystem.NextAge));
        }
    }
}
