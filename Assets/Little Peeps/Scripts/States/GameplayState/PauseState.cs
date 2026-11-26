using System;
using UnityEngine;

namespace LittlePeeps
{
    // The pause: Resume, Settings, Main Menu, Quit, over the frozen game. Pushed over normal play by the back
    // key (GameplayContainerState) and popped back onto it by Resume or the key again; nowhere else — the
    // other screens over the game are decisions it is already waiting on.
    //
    // Being on top of PlayingState is the whole pause as far as the run is concerned: the offering clock,
    // the play time and the age advance all run only while normal play is current. timeScale 0 stops what
    // moves (units, animals, physics), as on every screen over the game.
    //
    // Main Menu keeps the run: it is written as the bookmark (GameBootstrap hands that in with toMainMenu) and
    // waits under the main menu for Continue. Quit writes it too, on the way out (GameBootstrap.OnApplicationQuit),
    // so neither asks.
    public class PauseState : IState
    {
        private readonly StateMachine gameplayFsm;
        private readonly PauseMenuUI ui;
        private readonly SettingsUI settings;
        private readonly ConfirmDialog confirm;

        // The app FSM's way to the main menu, from GameBootstrap: this state lives in the gameplay FSM and
        // the menu is a state of the app FSM around it, built after it.
        private readonly Action toMainMenu;

        // Set when the screen cannot be shown. Acted on in Tick, for PerkSelectionState's reason: leaving
        // from inside Enter would run this object's Exit before its Enter had returned.
        private bool leaveImmediately;

        public PauseState(StateMachine gameplayFsm, PauseMenuUI ui, SettingsUI settings, ConfirmDialog confirm,
                          Action toMainMenu)
        {
            this.gameplayFsm = gameplayFsm;
            this.ui = ui;
            this.settings = settings;
            this.confirm = confirm;
            this.toMainMenu = toMainMenu;
        }

        public void Enter()
        {
            leaveImmediately = false;

            // Without its screen the pause would freeze the game with nothing on it to say so. Loud, because
            // it is a wiring mistake, and the game simply plays on.
            if (ui == null)
            {
                Debug.LogError("PauseState has no PauseMenuUI — Esc does not pause. Assign Pause Menu on the " +
                               "Canvas's UIRoot.");
                leaveImmediately = true;
                return;
            }

            Time.timeScale = 0f;
            EventBus<UIModeChangedEvent>.Publish(new UIModeChangedEvent { Mode = UIMode.Pause });
            ui.Show(Resume, OpenSettings, ToMainMenu, Quit);
        }

        // Whichever way the pause is left — Resume, the key, or the main menu (GameplayContainerState.Exit
        // clears the gameplay FSM) — nothing of it stays up and no late click reaches it.
        public void Exit()
        {
            if (confirm != null) confirm.Hide();
            if (settings != null) settings.Hide();
            if (ui != null) ui.Hide();

            // Unconditional, like the other freezing states.
            Time.timeScale = 1f;
        }

        public void Tick()
        {
            if (leaveImmediately) Resume();
        }

        // The back key, one step at a time: the question, then the settings, then the pause itself.
        public void Back()
        {
            if (confirm != null && confirm.IsOpen) confirm.Cancel();
            else if (settings != null && settings.IsOpen) settings.Close();
            else Resume();
        }

        private void Resume()
        {
            leaveImmediately = false;
            gameplayFsm.Pop();
        }

        // The settings take the pause's place on screen and give it back when closed. No Reset Progress
        // here: the run being paused was built with the profile, so the reset lives on the main menu.
        private void OpenSettings()
        {
            if (settings == null) return;

            ui.SetPanelShown(false);
            settings.Show(() => ui.SetPanelShown(true));
        }

        private void ToMainMenu() => toMainMenu?.Invoke();

        private void Quit() => AppQuit.Quit();
    }
}
