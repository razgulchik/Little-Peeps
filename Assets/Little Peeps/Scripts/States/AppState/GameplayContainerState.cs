using UnityEngine;

namespace LittlePeeps
{
    // Outer app state owning the inner gameplay FSM: normal play (PlayingState — building included, the
    // build panel is always up there) and the screens opened over it, the age advance, the perk picks
    // owed at a run's start, the offering cycle and the pause.
    //
    // Entered from the main menu and left back to it (through the pause's Main Menu). The run itself
    // outlives both: leaving here only closes the screens, so Continue picks the same run up again.
    public class GameplayContainerState : IState
    {
        private readonly StateMachine innerFsm;
        private readonly PlayingState playingState;
        private readonly PerkSelectionState perkSelectionState;

        // Pushed over normal play by the back key, and only there: the other screens over the game are
        // decisions it waits on, and frozen already. Being on top of PlayingState is what stops the clocks
        // below — every Tick here asks for normal play.
        private readonly PauseState pauseState;

        // The offering cycle: its clock runs only while normal play is current — advanced from Tick below —
        // so every screen over the game stops it by construction, with no pause flag to keep in sync.
        private readonly OfferingSystem offerings;
        private readonly OfferingEndState offeringEndState;

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
                                      ZoneSelectionUI zoneSelectionUI, RunManager runManager,
                                      OfferingSystem offerings = null, OfferingEndState offeringEndState = null,
                                      PauseState pauseState = null)
        {
            this.innerFsm = innerFsm;
            this.playingState = playingState;
            this.perkSelectionState = perkSelectionState;
            this.pauseState = pauseState;
            this.offerings = offerings;
            this.offeringEndState = offeringEndState;
            this.ageSystem = ageSystem;
            this.ageSequencer = ageSequencer;
            this.resourceSystem = resourceSystem;
            this.islandSystem = islandSystem;
            this.zoneSelectionUI = zoneSelectionUI;
            this.runManager = runManager;
        }

        // Whether normal play is what is on screen — the only state the run can be snapshotted in as it stands
        // (every other one is part-way through something). Asked when the game is closed (GameBootstrap).
        public bool InNormalPlay => innerFsm.Current == playingState;

        public void Enter()
        {
            innerFsm.ChangeState(playingState);
            EventBus<AgeAdvanceRequestedEvent>.Subscribe(OnAgeAdvanceRequested);
            EventBus<BackRequestedEvent>.Subscribe(OnBackRequested);
        }

        // Back to the main menu. The inner states are left too — Clear runs the top one's Exit (the pause:
        // its screen goes away) — so nothing of the game's UI stays up under the menu, and the next Enter
        // starts from normal play on a machine that holds nothing stale.
        public void Exit()
        {
            EventBus<AgeAdvanceRequestedEvent>.Unsubscribe(OnAgeAdvanceRequested);
            EventBus<BackRequestedEvent>.Unsubscribe(OnBackRequested);
            innerFsm.Clear();
        }

        public void Tick()
        {
            TryOpenStartPerkPick();
            TickPlayTime();
            TickOffering();
            innerFsm.Tick();
        }

        // Count the run's play time, which prestige pays for (PrestigeFormula). Only in normal play, so every
        // screen over the game stops it by construction. Beside the offering clock and not read off it: the
        // time counts with the offering cycle switched off too.
        private void TickPlayTime()
        {
            if (innerFsm.Current != playingState) return;

            // Read the run HERE, not in the constructor — see the runManager field.
            var run = runManager != null ? runManager.CurrentRun : null;
            if (run != null) run.playSeconds += Time.deltaTime;
        }

        // Run the cycle's clock, and end the cycle when it runs out — only in normal play, after the owed
        // start picks (those leave normal play, and the check below then waits for the way back). The end of
        // a cycle stops the game and asks: offer, or accept the gods' wrath.
        private void TickOffering()
        {
            if (offerings == null || innerFsm.Current != playingState) return;

            offerings.Advance(Time.deltaTime);
            if (!offerings.IsOver) return;

            if (offeringEndState != null) innerFsm.ChangeState(offeringEndState);
            else offerings.NextCycle();   // nothing to ask with: the run goes on, never ended unasked
        }

        // Meta perks can owe the run perk picks at its start (StartPerkPickUpgradeDef).
        // Opened HERE, from normal play, rather than wherever a run is started: runs start in four places —
        // the first run in GameBootstrap, Play on the meta screen, New Run on the main menu, the debug Restart
        // Run — and this one check covers all of them, and any added later. Normal play only, by the rule the age advance follows
        // too: the pick freezes the game and must not cut into another screen. Leaving normal play drops
        // whatever build tool was in hand (PlayingState.Exit).
        private void TryOpenStartPerkPick()
        {
            if (innerFsm.Current != playingState) return;

            // Read the run HERE, not in the constructor — see the runManager field.
            var run = runManager != null ? runManager.CurrentRun : null;
            if (run == null || run.perkPicksOwed <= 0) return;

            // PerkSelectionState takes the pick this entry shows off the debt as it enters — after normal play
            // is left, so the snapshot taken then still owes it — shows the rest in the same sitting, and
            // clears them if it cannot: a pick that skips itself (nothing to offer, no screen) lands back in
            // playing with nothing left to reopen every frame.
            innerFsm.ChangeState(perkSelectionState.ForOwedPick());
        }

        // The back key: from normal play it opens the pause, and on the pause it is the pause's own step back
        // (a question, the settings, then the pause itself). Both here, in one handler, rather than the pause
        // listening for itself: two listeners of one key would each see the other's change within the same
        // publish, and which one ran first would decide whether Esc closes the pause or reopens it.
        private void OnBackRequested(BackRequestedEvent _)
        {
            if (pauseState == null) return;

            if (innerFsm.Current == playingState) innerFsm.Push(pauseState);
            else if (innerFsm.Current == pauseState) pauseState.Back();
        }

        // Start an age only from normal play (not mid-transition, not over another screen) and only when
        // the next age is actually free or affordable. ZoneSelectionState owns the spend and the zone pick, then
        // hands over to AgeTransitionState for the animation and the return.
        private void OnAgeAdvanceRequested(AgeAdvanceRequestedEvent _)
        {
            if (innerFsm.Current != playingState) return;
            if (ageSystem == null || !ageSystem.CanAdvance) return;

            // Read the run HERE, not in the constructor — see the runManager field.
            var run = runManager != null ? runManager.CurrentRun : null;
            if (run == null) return;

            innerFsm.ChangeState(new ZoneSelectionState(
                innerFsm, playingState, ageSequencer, resourceSystem, islandSystem,
                zoneSelectionUI, run, ageSystem.NextAge));
        }
    }
}
