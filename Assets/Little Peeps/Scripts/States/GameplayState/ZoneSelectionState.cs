using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Inner gameplay state that owns the BUYING of an age and the choice of where the island grows:
    // spends + applies the age (TriggerAgeCmd), freezes the game, puts the zone offers on screen and,
    // once the player picks one, hands over to AgeTransitionState with the pick. There is no cancel —
    // the age is paid for on entry, so the only ways out are a pick or an age that grows nothing.
    //
    // A state and not a step of the transition, for the reason PerkSelectionState gives: a step that
    // waits on a human is a mode. It sits BEFORE the transition rather than inside it because the
    // player chooses while looking at the island as it is, with the candidates drawn onto it — the
    // fade that follows would hide exactly what the choice is about.
    //
    // Input block: timeScale 0 stops the sim and makes TapSystem ignore world clicks. Camera pan/zoom
    // run on unscaled time, so the player can look around the offers while they decide.
    //
    // Built per request by GameplayContainerState and discarded after, so holding the RunContext
    // directly is right here (the run cannot be replaced while the game is frozen and prestige is
    // unreachable) — the same argument AgeTransitionState makes.
    public class ZoneSelectionState : IState
    {
        private readonly StateMachine gameplayFsm;
        private readonly PlayingState playingState;
        private readonly PerkSelectionState perkSelectionState;
        private readonly AgeSequencer ageSequencer;
        private readonly ResourceSystem resourceSystem;
        private readonly IslandSystem islandSystem;
        private readonly ZoneSelectionUI ui;
        private readonly RunContext runContext;
        private readonly AgeDef ageDef;

        // The age never happened: the cost changed between the button press and here, or the state was
        // handed nothing to work with. Back to play, and no perk — nothing was bought.
        private bool aborted;

        // The age is bought and the zone question is settled — by a pick, by there being nothing to
        // offer (chosen stays null: the age goes on without growing) or by there being no screen to
        // pick on. Acted on in Tick, not where it is set: the state is already on the FSM's stack when
        // Enter runs, and changing state from inside Enter would run this object's own Exit first.
        private bool decided;
        private ZoneOffer chosen;

        public ZoneSelectionState(StateMachine gameplayFsm, PlayingState playingState,
                                  PerkSelectionState perkSelectionState, AgeSequencer ageSequencer,
                                  ResourceSystem resourceSystem, IslandSystem islandSystem, ZoneSelectionUI ui,
                                  RunContext runContext, AgeDef ageDef)
        {
            this.gameplayFsm = gameplayFsm;
            this.playingState = playingState;
            this.perkSelectionState = perkSelectionState;
            this.ageSequencer = ageSequencer;
            this.resourceSystem = resourceSystem;
            this.islandSystem = islandSystem;
            this.ui = ui;
            this.runContext = runContext;
            this.ageDef = ageDef;
        }

        public void Enter()
        {
            aborted = false;
            decided = false;
            chosen = null;

            if (runContext == null || ageDef == null || resourceSystem == null)
            {
                aborted = true;
                return;
            }

            var cmd = new TriggerAgeCmd(resourceSystem, runContext, ageDef);
            if (!cmd.CanExecute())
            {
                // Cost changed between the button press and here — bail straight back to playing.
                aborted = true;
                return;
            }

            cmd.Execute();                       // spend + currentAge++ + apply modifiers
            Time.timeScale = 0f;                 // freeze + block world input for the pick

            var offers = islandSystem != null ? islandSystem.ProposeZones() : new List<ZoneOffer>();

            // Nothing to offer is IslandSystem's to report (it does, loudly); here it only means the
            // age goes on without growing. The player paid, so the transition and the perk still owe.
            if (offers.Count == 0)
            {
                decided = true;
                return;
            }

            // A missing screen must not softlock the run, and must not stunt the island either: the
            // zone is what the age was bought for. Take the first offer and shout — this is a wiring
            // mistake, not a situation.
            if (ui == null)
            {
                Debug.LogError("ZoneSelectionState has no ZoneSelectionUI — taking the first zone offer " +
                               "unasked. Assign it on GameBootstrap.");
                chosen = offers[0];
                decided = true;
                return;
            }

            EventBus<ZoneSelectedEvent>.Subscribe(OnZoneSelected);
            ui.Show(offers);
        }

        public void Exit()
        {
            EventBus<ZoneSelectedEvent>.Unsubscribe(OnZoneSelected);
            if (ui != null) ui.Hide();

            // Unconditional, like the other frozen states: whichever way we leave, the game must not
            // stay frozen. AgeTransitionState freezes it again on the very next line when it follows.
            Time.timeScale = 1f;
        }

        public void Tick()
        {
            if (aborted)
            {
                gameplayFsm.ChangeState(playingState);
                return;
            }

            if (!decided) return;

            gameplayFsm.ChangeState(new AgeTransitionState(
                gameplayFsm, ageSequencer, perkSelectionState, runContext.currentAge, ageDef, chosen));
        }

        // The player picked a card. Only recorded here; the state change happens in the next Tick so
        // there is a single exit path whatever settled the question.
        private void OnZoneSelected(ZoneSelectedEvent e)
        {
            if (decided) return;
            chosen = e.Offer;
            decided = true;
        }
    }
}
