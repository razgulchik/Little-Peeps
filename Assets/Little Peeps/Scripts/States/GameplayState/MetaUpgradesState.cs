using UnityEngine;

namespace LittlePeeps
{
    // The meta screen: after a run is cashed in, the player spends prestige points on meta perks and presses
    // Play. Play is the only way out, and it is what starts the next run — AFTER the spending, so that run is
    // built with what was just bought (RunManager.StartNewRun → PrestigeSystem.ApplyUpgrades).
    //
    // A gameplay state, like the perk pick: entered from PrestigeMenuState on Yes, left to PlayingState on
    // Play, with the cashed-in run frozen underneath until then. It started life as an empty app-FSM stub in
    // the first skeleton, meant for a "Meta" button on a main menu; that menu does not exist, and a prestige
    // is the only way here.
    public class MetaUpgradesState : IState
    {
        private readonly StateMachine gameplayFsm;
        private readonly PrestigeSystem prestigeSystem;
        private readonly MetaUpgradesUI ui;
        private readonly PlayingState playingState;

        // RunManager, not RunContext: what this state does on the way out is replace the run.
        private readonly RunManager runManager;

        // Set when the screen cannot be shown. Acted on in Tick, for PerkSelectionState's reason: leaving
        // from inside Enter would run this object's Exit before its Enter had returned.
        private bool leaveImmediately;

        public MetaUpgradesState(StateMachine gameplayFsm, PrestigeSystem prestigeSystem, RunManager runManager,
                                 MetaUpgradesUI ui, PlayingState playingState)
        {
            this.gameplayFsm = gameplayFsm;
            this.prestigeSystem = prestigeSystem;
            this.runManager = runManager;
            this.ui = ui;
            this.playingState = playingState;
        }

        public void Enter()
        {
            leaveImmediately = false;

            // No screen, no spending — but the run HAS been cashed in, so the next run must still start.
            // Going back to the old one would hand the player a run that is already paid for. Loud, because
            // it is a wiring mistake, not a situation.
            if (ui == null)
            {
                Debug.LogError("MetaUpgradesState has no MetaUpgradesUI — the next run starts without the meta " +
                               "screen. Assign Meta Screen on the Canvas's UIRoot.");
                leaveImmediately = true;
                return;
            }

            // Frozen like the question before it: the old run waits underneath, and nothing may move in it.
            Time.timeScale = 0f;
            EventBus<UIModeChangedEvent>.Publish(new UIModeChangedEvent { Mode = UIMode.MetaUpgrades });
            ui.Show(prestigeSystem, StartNextRun);
        }

        public void Exit()
        {
            if (ui != null) ui.Hide();

            // Unconditional, like the other freezing states.
            Time.timeScale = 1f;
        }

        public void Tick()
        {
            if (leaveImmediately) StartNextRun();
        }

        // Play. Back to normal play FIRST, then replace the run from there — the order a prestige has always
        // replaced the run in, since the days of the pier click: unfrozen, in normal play, never from build
        // mode. The profile is saved before the run is built, since the run reads what was spent.
        private void StartNextRun()
        {
            leaveImmediately = false;
            gameplayFsm.ChangeState(playingState);

            if (prestigeSystem != null) prestigeSystem.SaveProfile();
            if (runManager != null) runManager.StartNewRun();
        }
    }
}
