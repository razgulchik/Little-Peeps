using UnityEngine;

namespace LittlePeeps
{
    // Normal gameplay: units bounce, resources accumulate; player can open BuildMode or trigger prestige
    public class PlayingState : IState
    {
        private readonly StateMachine gameplayFsm;
        private readonly PrestigeSystem prestigeSystem;

        // RunManager, not RunContext: this state is built once and survives every prestige, so a captured
        // context would go stale the first time the player restarts. Read runManager.CurrentRun at the
        // point of use. See GameplayContainerState for the bug this rule came from.
        private readonly RunManager runManager;

        // The prestige chain: "End this run?" → the meta screen → back here. Built here rather than in
        // GameBootstrap because both steps return to THIS state, so neither can exist before it.
        private readonly PrestigeMenuState prestigeMenu;

        public PlayingState(StateMachine gameplayFsm, RunManager runManager, PrestigeSystem prestigeSystem,
                            ConfirmDialog confirmDialog, MetaUpgradesUI metaScreen)
        {
            this.gameplayFsm = gameplayFsm;
            this.runManager = runManager;
            this.prestigeSystem = prestigeSystem;

            var metaUpgrades = new MetaUpgradesState(gameplayFsm, prestigeSystem, runManager, metaScreen, this);
            prestigeMenu = new PrestigeMenuState(gameplayFsm, prestigeSystem, runManager, confirmDialog, this,
                                                 metaUpgrades);
        }

        // The prestige subscription lives HERE, and not on PrestigeSystem itself, because its lifetime IS
        // the rule "a run ends only from normal play". This state is current only then: opening the build
        // panel or an age transition replaces it in the inner FSM, and Exit() takes the subscription down
        // with it — held by construction rather than by a guard someone can forget to copy. (The panel
        // hides the prestige button as well; this is what holds even if a layout forgets to.)
        public void Enter()
        {
            // Normal play is the mode every other one falls back to, and the FSM re-Enters this state on
            // every way back — so announcing it here is the whole "put the HUD back" story.
            EventBus<UIModeChangedEvent>.Publish(new UIModeChangedEvent { Mode = UIMode.Playing });
            EventBus<PrestigeTriggeredEvent>.Subscribe(OnPrestigeTriggered);
        }

        public void Exit()
        {
            EventBus<PrestigeTriggeredEvent>.Unsubscribe(OnPrestigeTriggered);
        }

        public void Tick()
        {
            // Nothing per-frame yet. Age advancement is NOT here — GameplayContainerState owns it, since
            // it also owns the build-mode toggle that must not run at the same time.
        }

        // The player clicked the prestige button. The run is read HERE rather than held: this state
        // outlives every prestige, and the run being measured is the one that is about to be replaced.
        private void OnPrestigeTriggered(PrestigeTriggeredEvent _)
        {
            if (prestigeSystem == null || runManager == null) return;

            var run = runManager.CurrentRun;
            if (run == null) return;

            if (!prestigeSystem.CanPrestige(run))
            {
                // The button only shows once the run can prestige, so this is a publisher that skipped
                // that gate. Quiet: the run simply goes on.
                Debug.Log($"[Prestige] Opens at age {prestigeSystem.PierUnlockAge} — " +
                          $"this run is at {run.currentAge}.");
                return;
            }

            // Ask first. PrestigeMenuState shows what the run is worth and ends it only on Yes.
            gameplayFsm.ChangeState(prestigeMenu);
        }
    }
}
