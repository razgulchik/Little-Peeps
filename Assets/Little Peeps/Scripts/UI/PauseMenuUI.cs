using System;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The pause's buttons: Resume / Settings / Main Menu / Quit. Clicks go back to PauseState through the
    // callbacks it hands to Show; this panel decides nothing.
    //
    // Visibility is NOT this panel's job — UIVisibility shows it on UIMode.Pause — but the object must stay
    // ACTIVE, for PerkSelectionUI's reason: a panel authored inactive would run its Awake inside the very
    // reveal that shows it.
    [RequireComponent(typeof(CanvasGroup))]
    public class PauseMenuUI : MonoBehaviour
    {
        [Tooltip("The title and the buttons — put away while the settings are up, which take their place.")]
        [SerializeField] private GameObject panel;

        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        private Action onResume;
        private Action onSettings;
        private Action onMainMenu;
        private Action onQuit;

        private void OnEnable()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (settingsButton != null) settingsButton.onClick.AddListener(Settings);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(MainMenu);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        private void OnDisable()
        {
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(Settings);
            if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(MainMenu);
            if (quitButton != null) quitButton.onClick.RemoveListener(Quit);
        }

        // Wiring tripwire, in Start so every Awake has had its chance first. Esc still resumes without the
        // buttons, so a warning, not an error.
        private void Start()
        {
            if (panel == null || resumeButton == null || settingsButton == null || mainMenuButton == null ||
                quitButton == null)
                Debug.LogWarning($"[PauseMenuUI] '{name}' is missing its panel or one of its buttons — the pause " +
                                 "offers less than it should. Esc still resumes.", this);
        }

        // Called by PauseState after it has announced the mode, so the screen is already up.
        public void Show(Action onResume, Action onSettings, Action onMainMenu, Action onQuit)
        {
            this.onResume = onResume;
            this.onSettings = onSettings;
            this.onMainMenu = onMainMenu;
            this.onQuit = onQuit;

            SetPanelShown(true);
        }

        // Drops the callbacks, so a click that lands after this reaches nobody.
        public void Hide()
        {
            onResume = null;
            onSettings = null;
            onMainMenu = null;
            onQuit = null;
        }

        public void SetPanelShown(bool shown)
        {
            if (panel != null) panel.SetActive(shown);
        }

        private void Resume() => onResume?.Invoke();
        private void Settings() => onSettings?.Invoke();
        private void MainMenu() => onMainMenu?.Invoke();
        private void Quit() => onQuit?.Invoke();
    }
}
