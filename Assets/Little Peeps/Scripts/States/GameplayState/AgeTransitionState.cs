using UnityEngine;

namespace LittlePeeps
{
    // Inner gameplay state that plays one age transition: freezes the game, runs the AgeSequencer
    // (fade, grow the island by the chosen zone, banner, fade back), then hands over to the perk pick
    // when it completes. The age itself is already bought and applied by the time this state is
    // entered — ZoneSelectionState does that, and it is the only thing that builds one of these.
    //
    // Input block: timeScale 0 stops the sim AND makes TapSystem ignore world clicks (it early-returns at
    // timeScale 0), so pier/boost taps can't fire mid-transition — no UI-raycast juggling needed. The
    // sequencer runs on unscaled time, so the fade/banner still animate while frozen.
    public class AgeTransitionState : IState
    {
        private readonly StateMachine gameplayFsm;
        private readonly AgeSequencer ageSequencer;
        private readonly PerkSelectionState perkSelectionState;
        private readonly int newAge;
        private readonly AgeDef ageDef;

        // The zone the island grows by, or null when this age grows nothing (no valid zone to offer).
        private readonly ZoneOffer zone;

        private bool complete;

        public AgeTransitionState(StateMachine gameplayFsm, AgeSequencer ageSequencer,
                                  PerkSelectionState perkSelectionState, int newAge, AgeDef ageDef, ZoneOffer zone)
        {
            this.gameplayFsm = gameplayFsm;
            this.ageSequencer = ageSequencer;
            this.perkSelectionState = perkSelectionState;
            this.newAge = newAge;
            this.ageDef = ageDef;
            this.zone = zone;
        }

        public void Enter()
        {
            complete = false;
            Time.timeScale = 0f;                 // freeze + block world input for the transition

            // No sequencer means no transition to play, not a transition to wait on forever.
            if (ageSequencer == null)
            {
                complete = true;
                return;
            }

            // Announced only once there is a transition to actually play: a missing sequencer leaves on
            // the next Tick without ever drawing anything, and the mode must not blink to it.
            EventBus<UIModeChangedEvent>.Publish(new UIModeChangedEvent { Mode = UIMode.AgeTransition });

            ageSequencer.StartAgeTransition(newAge, ageDef, zone, () => complete = true);
        }

        public void Exit()
        {
            Time.timeScale = 1f;
        }

        public void Tick()
        {
            if (complete) gameplayFsm.ChangeState(perkSelectionState);
        }
    }
}
