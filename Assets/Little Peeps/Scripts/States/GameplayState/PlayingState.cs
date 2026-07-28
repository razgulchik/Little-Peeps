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

        public PlayingState(StateMachine gameplayFsm, RunManager runManager, PrestigeSystem prestigeSystem)
        {
            this.gameplayFsm = gameplayFsm;
            this.runManager = runManager;
            this.prestigeSystem = prestigeSystem;
        }

        // The prestige subscription lives HERE, and not on PrestigeSystem itself, because its lifetime IS
        // the rule "a run never ends from build mode". This state is current only during normal play:
        // entering build mode or an age transition replaces it in the inner FSM, and Exit() takes the
        // subscription down with it. TapSystem's timeScale check happens to cover the same ground today,
        // but only because both of those freeze time — a coincidence of implementation, not a rule. That
        // rule is what makes RunManager.EndRun's sweep over run.structures complete, so it is worth
        // holding by construction rather than by a guard someone can forget to copy.
        public void Enter()
        {
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

        // The player clicked the pier. The run is read HERE rather than held: this state outlives every
        // prestige, and the run being measured is the one that is about to be replaced.
        private void OnPrestigeTriggered(PrestigeTriggeredEvent _)
        {
            if (prestigeSystem == null || runManager == null) return;

            var run = runManager.CurrentRun;
            if (run == null) return;

            if (!prestigeSystem.CanPrestige(run))
            {
                // Quiet by design: the pier is visible from the first age on purpose, as a goal to reach.
                // B2 turns this into the confirmation screen saying so; a log is enough until then.
                Debug.Log($"[Prestige] The pier opens at age {prestigeSystem.PierUnlockAge} — " +
                          $"this run is at {run.currentAge}.");
                return;
            }

            // B2 replaces this with gameplayFsm.ChangeState(new PrestigeMenuState(...)), which shows the
            // projected points and calls ExecutePrestige itself on Confirm.
            prestigeSystem.ExecutePrestige(run);
        }
    }
}
