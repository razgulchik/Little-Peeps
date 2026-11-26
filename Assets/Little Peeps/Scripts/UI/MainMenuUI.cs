using System;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The title screen's buttons. Clicks go back to MainMenuState through the callbacks it hands to Show —
    // this panel decides nothing, it only shows the buttons that apply:
    //
    //   - no run in progress: Play / Settings / Quit;
    //   - a run in progress:  Continue / New Run / Settings / Quit.
    //
    // The buttons sit in a Vertical Layout Group, so the ones put away leave no gap.
    //
    // Visibility is NOT this panel's job — UIVisibility shows it on UIMode.MainMenu — but the object must
    // stay ACTIVE, for PerkSelectionUI's reason: a panel authored inactive would run its Awake inside the
    // very reveal that shows it.
    [RequireComponent(typeof(CanvasGroup))]
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("The title and the buttons — put away while the settings are up, which take their place.")]
        [SerializeField] private GameObject panel;

        [Tooltip("Shown while there is no run to continue.")]
        [SerializeField] private Button playButton;

        [Tooltip("Shown, with New Run, while a run waits under the menu.")]
        [SerializeField] private Button continueButton;

        [SerializeField] private Button newRunButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        private Action onPlay;
        private Action onContinue;
        private Action onNewRun;
        private Action onSettings;
        private Action onQuit;

        private void OnEnable()
        {
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
            if (newRunButton != null) newRunButton.onClick.AddListener(NewRun);
            if (settingsButton != null) settingsButton.onClick.AddListener(Settings);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private void OnDisable()
        {
            if (playButton != null) playButton.onClick.RemoveListener(Play);
            if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
            if (newRunButton != null) newRunButton.onClick.RemoveListener(NewRun);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(Settings);
            if (quitButton != null) quitButton.onClick.RemoveListener(Quit);
        }

        // Wiring tripwire, in Start so every Awake has had its chance first. Loud: the menu is the only way
        // into the game, and a missing Play is a game nobody can start.
        private void Start()
        {
            if (panel == null || playButton == null || continueButton == null || newRunButton == null ||
                settingsButton == null || quitButton == null)
                Debug.LogError($"[MainMenuUI] '{name}' is missing its panel or one of its buttons — the main menu " +
                               "may offer no way into the game.", this);
        }

        // Called by MainMenuState after it has announced the mode, so the screen is already up: this owns
        // which buttons it shows and where their clicks go, not whether it is on screen.
        public void Show(bool runInProgress, Action onPlay, Action onContinue, Action onNewRun, Action onSettings,
                         Action onQuit)
        {
            this.onPlay = onPlay;
            this.onContinue = onContinue;
            this.onNewRun = onNewRun;
            this.onSettings = onSettings;
            this.onQuit = onQuit;

            SetRunInProgress(runInProgress);
            SetPanelShown(true);
        }

        // Drops the callbacks, so a click that lands after this reaches nobody.
        public void Hide()
        {
            onPlay = null;
            onContinue = null;
            onNewRun = null;
            onSettings = null;
            onQuit = null;
        }

        public void SetRunInProgress(bool inProgress)
        {
            if (playButton != null) playButton.gameObject.SetActive(!inProgress);
            if (continueButton != null) continueButton.gameObject.SetActive(inProgress);
            if (newRunButton != null) newRunButton.gameObject.SetActive(inProgress);
        }

        public void SetPanelShown(bool shown)
        {
            if (panel != null) panel.SetActive(shown);
        }

        private void Play() => onPlay?.Invoke();
        private void Continue() => onContinue?.Invoke();
        private void NewRun() => onNewRun?.Invoke();
        private void Settings() => onSettings?.Invoke();
        private void Quit() => onQuit?.Invoke();
    }
}
