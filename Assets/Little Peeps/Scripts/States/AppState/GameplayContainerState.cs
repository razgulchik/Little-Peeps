using UnityEngine;

namespace LittlePeeps
{
    // Outer app state owning the inner gameplay FSM: normal play (PlayingState — building included, the
    // build panel is always up there) and the screens opened over it, the age advance, the perk picks
    // owed at a run's start and the offering cycle.
    public class GameplayContainerState : IState
    {
        private readonly StateMachine innerFsm;
        private readonly PlayingState playingState;
        private readonly PerkSelectionState perkSelectionState;

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
                                      OfferingSystem offerings = null, OfferingEndState offeringEndState = null)
        {
            this.innerFsm = innerFsm;
            this.playingState = playingState;
            this.perkSelectionState = perkSelectionState;
            this.offerings = offerings;
            this.offeringEndState = offeringEndState;
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
            EventBus<OfferRequestedEvent>.Subscribe(OnOfferRequested);
        }

        public void Exit()
        {
            EventBus<AgeAdvanceRequestedEvent>.Unsubscribe(OnAgeAdvanceRequested);
            EventBus<OfferRequestedEvent>.Unsubscribe(OnOfferRequested);
        }

        public void Tick()
        {
            TryOpenStartPerkPick();
            TickOffering();
            innerFsm.Tick();
        }

        // Run the cycle's clock, and end the cycle when it runs out — only in normal play, after the owed
        // start picks (those leave normal play, and the check below then waits for the way back). A cycle
        // paid early simply gives way to the next one; an unpaid one stops the game and asks.
        private void TickOffering()
        {
            if (offerings == null || innerFsm.Current != playingState) return;

            offerings.Advance(Time.deltaTime);
            if (!offerings.IsOver) return;

            if (offerings.Offered) offerings.NextCycle();
            else if (offeringEndState != null) innerFsm.ChangeState(offeringEndState);
            else offerings.NextCycle();   // nothing to ask with: the run goes on, never ended unasked
        }

        // The offering button: pay now, before the time is up, and take the perk at once. Normal play only,
        // the rule the age advance follows. The clock keeps its rhythm — the next cycle still starts when
        // this one's time is up (TickOffering).
        private void OnOfferRequested(OfferRequestedEvent _)
        {
            if (innerFsm.Current != playingState) return;
            if (offerings == null || !offerings.TryOffer()) return;

            innerFsm.ChangeState(perkSelectionState.For(offerings.RewardGrade));
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
                innerFsm, playingState, ageSequencer, resourceSystem, islandSystem,
                zoneSelectionUI, run, ageSystem.NextAge));
        }
    }
}
