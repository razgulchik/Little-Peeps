using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The settings: volume, fullscreen, and — from the main menu only — Reset Progress. One window for both
    // places it opens from (MainMenuState, PauseState); the caller brings what Back returns to and whether
    // a reset is on offer, the window brings the controls.
    //
    // Volume and fullscreen apply the moment they change (GameSettings) — there is no Apply button, so
    // Back only closes. Reset Progress is a HOLD, like Face the Wrath: it cannot be taken back, and the
    // hold is its confirmation.
    //
    // Its visibility is its own and NOT a row in UIVisibility, for ConfirmDialog's reason: it opens over
    // two modes (the main menu and the pause), so "is it on screen" means "is it open", which no mode can
    // say. The object must stay ACTIVE — the CanvasGroup is what hides it.
    [RequireComponent(typeof(CanvasGroup))]
    public class SettingsUI : MonoBehaviour
    {
        [Tooltip("Whole numbers from 0 to its Max Value: the volume in that many steps.")]
        [SerializeField] private Slider volumeSlider;

        [Tooltip("The volume as a percentage, beside the slider.")]
        [SerializeField] private TMP_Text volumeValue;

        [SerializeField] private Button fullscreenButton;
        [SerializeField] private TMP_Text fullscreenLabel;

        [Tooltip("Hidden when opened from the pause. A Button on the same object gives it the hover look; the " +
                 "answer is the hold.")]
        [SerializeField] private HoldButton resetButton;

        [Tooltip("Optional. Its text in the prefab is what the button says before a reset.")]
        [SerializeField] private TMP_Text resetLabel;

        [SerializeField] private Button backButton;

        private const string ResetDoneText = "Progress Reset";

        private CanvasGroup group;
        private Selectable resetLook;
        private Action onClose;
        private Action onReset;
        private bool fullscreen;

        // The reset label as authored, read before a reset overwrites it.
        private string resetText;

        public bool IsOpen { get; private set; }

        // Lazy, so Show works before Awake has run.
        private CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

        private void Awake()
        {
            if (resetLabel != null) resetText = resetLabel.text;
            if (resetButton != null) resetLook = resetButton.GetComponent<Selectable>();
            if (!IsOpen) UIVisibility.Set(Group, false);
        }

        private void OnEnable()
        {
            if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            if (fullscreenButton != null) fullscreenButton.onClick.AddListener(ToggleFullscreen);
            if (resetButton != null) resetButton.Confirmed += OnResetConfirmed;
            if (backButton != null) backButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            if (fullscreenButton != null) fullscreenButton.onClick.RemoveListener(ToggleFullscreen);
            if (resetButton != null) resetButton.Confirmed -= OnResetConfirmed;
            if (backButton != null) backButton.onClick.RemoveListener(Close);
        }

        // Wiring tripwire, in Start so every Awake has had its chance first. Without Back the window could
        // only be left with Esc.
        private void Start()
        {
            if (volumeSlider == null || fullscreenButton == null || resetButton == null || backButton == null)
                Debug.LogError($"[SettingsUI] '{name}' is missing a control — a setting cannot be changed, or " +
                               "the window can only be closed with Esc.", this);
        }

        // Open with the settings as they are. `onClose` runs once the window has closed (Back or Esc); a
        // null `onReset` leaves Reset Progress out.
        public void Show(Action onClose, Action onReset = null)
        {
            this.onClose = onClose;
            this.onReset = onReset;

            // Without notifying: opening the window must not write the volume back.
            if (volumeSlider != null)
                volumeSlider.SetValueWithoutNotify(Mathf.Round(GameSettings.Volume * volumeSlider.maxValue));
            RefreshVolume();

            fullscreen = GameSettings.Fullscreen;
            RefreshFullscreen();

            if (resetButton != null)
            {
                resetButton.gameObject.SetActive(onReset != null);
                SetResetAvailable(true);
            }

            IsOpen = true;
            UIVisibility.Set(Group, true);
        }

        // Back, and Esc through the caller: close, then hand the screen back.
        public void Close()
        {
            if (!IsOpen) return;

            var close = onClose;
            Hide();
            close?.Invoke();
        }

        // Close without handing anything back — the caller is leaving (its Exit). Drops both callbacks, so a
        // click that lands after this reaches nobody. Safe to call when already closed.
        public void Hide()
        {
            if (IsOpen) GameSettings.Save();

            onClose = null;
            onReset = null;
            IsOpen = false;
            UIVisibility.Set(Group, false);
        }

        private void OnVolumeChanged(float steps)
        {
            if (volumeSlider == null || volumeSlider.maxValue <= 0f) return;

            GameSettings.Volume = steps / volumeSlider.maxValue;
            RefreshVolume();
        }

        private void RefreshVolume()
        {
            if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
        }

        // Kept here rather than read back: the new mode applies at the end of the frame, and until then the
        // screen still reports the old one.
        private void ToggleFullscreen()
        {
            fullscreen = !fullscreen;
            GameSettings.SetFullscreen(fullscreen);
            RefreshFullscreen();
        }

        private void RefreshFullscreen()
        {
            if (fullscreenLabel != null) fullscreenLabel.text = fullscreen ? "Fullscreen: On" : "Fullscreen: Off";
        }

        // One reset per opening: the button locks and says it is done, so a second hold has nothing to do.
        private void OnResetConfirmed()
        {
            var reset = onReset;
            if (reset == null) return;

            SetResetAvailable(false);
            reset();
        }

        private void SetResetAvailable(bool available)
        {
            resetButton.Interactable = available;
            if (resetLook != null) resetLook.interactable = available;
            if (resetLabel != null) resetLabel.text = available ? resetText : ResetDoneText;
        }
    }
}
