using UnityEngine;

namespace LittlePeeps
{
    // The title screen, over the island standing still behind it. The game boots into it, and the pause's
    // Main Menu comes back to it with the run kept.
    //
    //   - No run to continue:                       Play / Settings / Quit.
    //   - A run left in the middle, or saved last time: Continue / New Run / Settings / Quit.
    //
    // The run under the menu is a real one: GameBootstrap builds it in Awake — the saved one, or a fresh
    // island — so Play and Continue just go into it. A run is replaced here only when it has to be — New Run,
    // or Play after a progress reset, since that run was built with the perks the reset took away.
    //
    // A saved run is a bookmark (RunFile): Continue takes it out (the file is deleted, the run goes on in
    // memory), and leaving the game writes it again. So Quit loses nothing and does not ask; New Run, which
    // throws the run away, still does. The meta screen is not reached from here: it is a gameplay state after
    // a prestige (MetaUpgradesState).
    public class MainMenuState : IState
    {
        private readonly StateMachine appFsm;
        private readonly IState gameplay;
        private readonly RunManager runManager;
        private readonly PrestigeSystem prestigeSystem;
        private readonly SaveSystem saveSystem;
        private readonly MainMenuUI ui;
        private readonly SettingsUI settings;
        private readonly ConfirmDialog confirm;

        // Whether the player has been into the run under the menu, i.e. there is one to continue.
        private bool runEntered;

        // Whether that run was built with the profile as it stands. A progress reset makes it stale: it holds
        // the meta perks the reset just took away, so the next Play builds a fresh one.
        private bool runFresh = true;

        // Set when the menu cannot be shown. Acted on in Tick, for PerkSelectionState's reason: leaving
        // from inside Enter would run this object's Exit before its Enter had returned.
        private bool leaveImmediately;

        // `runRestored`: the run under the menu is the one saved last time, so it is offered to continue.
        public MainMenuState(StateMachine appFsm, IState gameplay, RunManager runManager,
                             PrestigeSystem prestigeSystem, SaveSystem saveSystem, MainMenuUI ui, SettingsUI settings,
                             ConfirmDialog confirm, bool runRestored = false)
        {
            this.appFsm = appFsm;
            this.gameplay = gameplay;
            this.runManager = runManager;
            this.prestigeSystem = prestigeSystem;
            this.saveSystem = saveSystem;
            this.ui = ui;
            this.settings = settings;
            this.confirm = confirm;
            runEntered = runRestored;
        }

        public void Enter()
        {
            leaveImmediately = false;

            // No menu to click means no way into the game at all, so go straight in rather than strand the
            // player on a frozen island. Loud, because it is a wiring mistake.
            if (ui == null)
            {
                Debug.LogError("MainMenuState has no MainMenuUI — the game starts without its main menu. " +
                               "Assign Main Menu on the Canvas's UIRoot.");
                leaveImmediately = true;
                return;
            }

            // Frozen: the island is the menu's backdrop, and nothing in it may move — or earn — meanwhile.
            Time.timeScale = 0f;
            EventBus<UIModeChangedEvent>.Publish(new UIModeChangedEvent { Mode = UIMode.MainMenu });
            EventBus<BackRequestedEvent>.Subscribe(OnBackRequested);
            ui.Show(runEntered, Play, Continue, NewRun, OpenSettings, Quit);
        }

        public void Exit()
        {
            EventBus<BackRequestedEvent>.Unsubscribe(OnBackRequested);

            if (confirm != null) confirm.Hide();
            if (settings != null) settings.Hide();
            if (ui != null) ui.Hide();

            // Unconditional, like the freezing gameplay states.
            Time.timeScale = 1f;
        }

        public void Tick()
        {
            if (leaveImmediately) EnterGame(rebuild: !runFresh);
        }

        // The back key: the question, then the settings. The menu itself has nothing to go back to.
        private void OnBackRequested(BackRequestedEvent _)
        {
            if (confirm != null && confirm.IsOpen) confirm.Cancel();
            else if (settings != null && settings.IsOpen) settings.Close();
        }

        private void Play() => EnterGame(rebuild: !runFresh);

        // The bookmark comes out: from here on nothing on disk can take the game back to this moment.
        private void Continue()
        {
            if (saveSystem != null) saveSystem.ConsumeRun();
            EnterGame(rebuild: false);
        }

        private void NewRun()
        {
            if (confirm == null || !confirm.Show("Start a new run? This one won't be cashed in.",
                                                 () => EnterGame(rebuild: true), null, "New Run", "Cancel"))
                EnterGame(rebuild: true);
        }

        // Into the game FIRST, then replace the run from there — the order the meta screen's Play has always
        // replaced it in (MetaUpgradesState.StartNextRun): unfrozen and in normal play.
        private void EnterGame(bool rebuild)
        {
            leaveImmediately = false;
            runEntered = true;
            runFresh = true;
            appFsm.ChangeState(gameplay);

            if (rebuild && runManager != null) runManager.StartNewRun();
        }

        // The settings take the menu's place on screen and give it back when closed. Reset Progress is
        // offered here only: the run under the menu is not being played, so replacing it costs nothing.
        private void OpenSettings()
        {
            if (settings == null) return;

            ui.SetPanelShown(false);
            settings.Show(() => ui.SetPanelShown(true), ResetProgress);
        }

        // The profile back to a fresh one (the hold on the button was the confirmation). The run under the
        // menu goes with it: it was built with the perks just taken away, and played with the points.
        private void ResetProgress()
        {
            if (prestigeSystem != null) prestigeSystem.ResetProfile();

            runEntered = false;
            runFresh = false;
            ui.SetRunInProgress(false);
        }

        // Nothing to ask: a run left in the middle was written on the way here, and one saved last time is
        // still on disk.
        private void Quit() => AppQuit.Quit();
    }
}
