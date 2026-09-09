using UnityEngine;

namespace LittlePeeps
{
    // The perk pick, as its own gameplay mode. It runs AFTER the age transition has fully finished, so
    // the player chooses while looking at the island they just grew rather than at a black screen.
    //
    // Why a state and not a step inside AgeSequencer's coroutine, since the sequencer has a hook shaped
    // exactly for it: the sequencer is choreography — an ordered list of things that each take a known
    // amount of time — while this step waits on a human, which is a different kind of wait. It is also
    // the only interactive moment in an otherwise non-interactive transition, and a perk that changes
    // the world (growing the island further is already planned) needs the world to exist and be visible
    // at the moment it applies. Both of those are properties of a MODE, which is what the FSM is for.
    //
    // The state is entered by AgeTransitionState on success — and deliberately not on its abort path,
    // where no age happened and so no perk is owed.
    public class PerkSelectionState : IState
    {
        private readonly StateMachine gameplayFsm;
        private readonly PerkSystem perkSystem;
        private readonly PerkSelectionUI ui;
        private readonly PlayingState playingState;

        // RunManager, not RunContext: this state is built once in GameBootstrap and survives every
        // prestige, so a captured context would be the run that already ended. Read CurrentRun at the
        // point of use — see GameplayContainerState for the bug this rule came from.
        private readonly RunManager runManager;

        // Set when the step cannot run to completion: nothing eligible to offer, no screen to offer it
        // on, or no run. Acted on in Tick rather than acted on immediately, because the state is already
        // on the FSM's stack by the time Enter is called — changing state from inside Enter would run
        // this object's own Exit before its Enter had returned.
        private bool leaveImmediately;

        public PerkSelectionState(StateMachine gameplayFsm, PerkSystem perkSystem, PerkSelectionUI ui,
                                  RunManager runManager, PlayingState playingState)
        {
            this.gameplayFsm = gameplayFsm;
            this.perkSystem = perkSystem;
            this.ui = ui;
            this.runManager = runManager;
            this.playingState = playingState;
        }

        public void Enter()
        {
            leaveImmediately = false;

            var run = runManager != null ? runManager.CurrentRun : null;
            if (run == null || perkSystem == null)
            {
                leaveImmediately = true;
                return;
            }

            var offer = perkSystem.RollPerks(run.currentAge, run);

            // An empty roll means nothing is eligible — every perk taken, or none unlocked at this age.
            // Skip the step rather than show a screen with no cards on it.
            if (offer == null || offer.Count == 0)
            {
                leaveImmediately = true;
                return;
            }

            // A missing screen must not softlock the run: without it there is no way to pick, and the
            // player would sit on a frozen island forever. Loud, because it is a wiring mistake, not a
            // situation — the same reason PerkSystem shouts about a missing catalogue.
            if (ui == null)
            {
                Debug.LogError("PerkSelectionState has no PerkSelectionUI — skipping the perk pick. " +
                               "Assign it on GameBootstrap.");
                leaveImmediately = true;
                return;
            }

            // Freeze while the player decides: units must not keep bouncing and resources must not keep
            // accruing during a choice that lasts as long as they want it to. UI clicks are unaffected —
            // the EventSystem runs in Update, which timeScale does not gate.
            Time.timeScale = 0f;

            EventBus<PerkSelectedEvent>.Subscribe(OnPerkSelected);
            ui.Show(offer);
        }

        public void Exit()
        {
            EventBus<PerkSelectedEvent>.Unsubscribe(OnPerkSelected);
            if (ui != null) ui.Hide();

            // Unconditional, like BuildModeState and AgeTransitionState: whichever way we leave, the game
            // must not stay frozen. Harmless when we never froze — the value is already 1.
            Time.timeScale = 1f;
        }

        public void Tick()
        {
            if (leaveImmediately) gameplayFsm.ChangeState(playingState);
        }

        // The player confirmed a card. Changing state from inside an event handler is the pattern
        // GameplayContainerState already uses for the build-mode toggle; it is safe here because the
        // Exit it triggers unsubscribes this very handler mid-dispatch, which EventBus pins as legal —
        // it iterates a snapshot, so the change only takes effect on the next Publish.
        private void OnPerkSelected(PerkSelectedEvent e)
        {
            var run = runManager != null ? runManager.CurrentRun : null;
            if (run == null || e.Perk == null)
            {
                leaveImmediately = true;
                return;
            }

            perkSystem.ApplyPerk(e.Perk, run);
            gameplayFsm.ChangeState(playingState);
        }
    }
}
