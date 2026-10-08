using UnityEngine;

namespace LittlePeeps
{
    // "End this run?" — the step between the prestige button and the end of a run. It shows what the run
    // is worth right now and ends it only on Yes. Ending a run is the one thing the player cannot take
    // back, so nothing ends it unasked: no dialog, no prestige.
    //
    // The game is frozen while the question is up, like the perk pick: the payout in the question must
    // still be the payout when Yes lands, and nothing may change the run underneath it.
    //
    // Entered from PlayingState (on PrestigeTriggeredEvent). No goes back there; Yes goes on to the meta
    // screen (MetaUpgradesState), which starts the next run on Play.
    public class PrestigeMenuState : IState
    {
        private readonly StateMachine gameplayFsm;
        private readonly PrestigeSystem prestigeSystem;
        private readonly ConfirmDialog dialog;
        private readonly PlayingState playingState;
        private readonly MetaUpgradesState metaUpgrades;

        // RunManager, not RunContext — and here it matters most: this state is the one that ENDS the run,
        // so a captured context would be exactly the object about to be replaced. Read CurrentRun at use.
        private readonly RunManager runManager;

        // Set when the question cannot be asked: no run, a run that cannot prestige, or no dialog to ask
        // it with. Acted on in Tick rather than at once, for PerkSelectionState's reason: the state is
        // already on the FSM's stack when Enter runs, so leaving from inside Enter would run this object's
        // Exit before its Enter had returned.
        private bool leaveImmediately;

        public PrestigeMenuState(StateMachine gameplayFsm, PrestigeSystem prestigeSystem, RunManager runManager,
                                 ConfirmDialog dialog, PlayingState playingState, MetaUpgradesState metaUpgrades)
        {
            this.gameplayFsm = gameplayFsm;
            this.prestigeSystem = prestigeSystem;
            this.runManager = runManager;
            this.dialog = dialog;
            this.playingState = playingState;
            this.metaUpgrades = metaUpgrades;
        }

        public void Enter()
        {
            leaveImmediately = false;

            var run = runManager != null ? runManager.CurrentRun : null;
            if (run == null || prestigeSystem == null || !prestigeSystem.CanPrestige(run))
            {
                leaveImmediately = true;
                return;
            }

            // Loud, because it is a wiring mistake, not a situation. The run goes on rather than ending
            // unasked: a missing dialog costs the player the prestige button, not their island.
            if (dialog == null)
            {
                Debug.LogError("PrestigeMenuState has no ConfirmDialog — the run is not ended unasked. " +
                               "Assign Confirm Dialog on the Canvas's UIRoot.");
                leaveImmediately = true;
                return;
            }

            // Asked BEFORE freezing: a dialog that cannot show its question must not leave the game frozen
            // with nothing on screen to answer.
            if (!dialog.Show(Question(prestigeSystem.Calculate(run)), OnYes, OnNo))
            {
                leaveImmediately = true;
                return;
            }

            // UI clicks are unaffected by the freeze — the EventSystem runs in Update, which timeScale does
            // not gate.
            Time.timeScale = 0f;
            EventBus<UIModeChangedEvent>.Publish(new UIModeChangedEvent { Mode = UIMode.PrestigeConfirm });
        }

        public void Exit()
        {
            // Whichever way the state is left, the question goes with it, so a late click cannot call into
            // a state that is no longer current. Harmless after an answer: the dialog is already closed.
            if (dialog != null) dialog.Hide();

            // Unconditional, like the other freezing states: the game must not stay frozen whichever way we
            // leave. Harmless when we never froze — the value is already 1.
            Time.timeScale = 1f;
        }

        public void Tick()
        {
            if (leaveImmediately) gameplayFsm.ChangeState(playingState);
        }

        // The question, with the payout in it. A run that beats no record is worth nothing, and the
        // question says so plainly instead of offering "+0" as if it were a reward.
        public static string Question(int payout) =>
            payout > 0
                ? $"End this run?\nYou'll get +{payout} prestige."
                : "End this run?\nNo prestige: it hasn't beaten your best run yet.";

        // Yes: cash the run in, then on to the meta screen. The run is NOT replaced here — it stays frozen
        // under the screen, and the screen's Play replaces it, so that the next run is built with what the
        // player spends there. Cashing in first is what puts this run's points on that screen.
        //
        // A refused cash-in cannot happen (the gate was checked in Enter, and the game has been frozen
        // since), but if it ever did, the run simply goes on rather than reaching a screen with nothing
        // new to spend.
        private void OnYes()
        {
            var run = runManager != null ? runManager.CurrentRun : null;
            bool cashedIn = run != null && prestigeSystem != null && prestigeSystem.CashIn(run);

            gameplayFsm.ChangeState(cashedIn && metaUpgrades != null ? metaUpgrades : playingState);
        }

        private void OnNo() => gameplayFsm.ChangeState(playingState);
    }
}
