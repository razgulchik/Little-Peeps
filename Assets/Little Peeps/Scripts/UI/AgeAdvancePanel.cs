using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The age label and the Next Age button — the one panel that BUYS an age. Its two neighbours under
    // the same root only report: AgeCostPanel draws the price, AgeTimelinePanel draws the ladder.
    //
    // The button just publishes an intent (AgeAdvanceRequestedEvent) — GameplayContainerState decides
    // whether the transition may start. The button's interactable state tracks affordability
    // (recomputed on every resource change / age start).
    public class AgeAdvancePanel : MonoBehaviour, IUIElement
    {
        [SerializeField] private TMP_Text ageLabel;
        [SerializeField] private Button nextAgeButton;

        private AgeSystem ageSystem;
        private RunContext runContext;

        // From UIRoot once the run exists; the run is the one standing now, and RunStartedEvent replaces it.
        public void Initialize(UIServices ui)
        {
            ageSystem = ui.AgeSystem;
            runContext = ui.RunManager != null ? ui.RunManager.CurrentRun : null;
        }

        private void Start() => Refresh();

        private void OnEnable()
        {
            if (nextAgeButton != null) nextAgeButton.onClick.AddListener(OnNextAgeClicked);
            EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted);
            EventBus<ResourceChangedEvent>.Subscribe(OnResourceChanged);
            EventBus<RunStartedEvent>.Subscribe(OnRunStarted);
        }

        private void OnDisable()
        {
            if (nextAgeButton != null) nextAgeButton.onClick.RemoveListener(OnNextAgeClicked);
            EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted);
            EventBus<ResourceChangedEvent>.Unsubscribe(OnResourceChanged);
            EventBus<RunStartedEvent>.Unsubscribe(OnRunStarted);
        }

        private void OnNextAgeClicked()
        {
            EventBus<AgeAdvanceRequestedEvent>.Publish(new AgeAdvanceRequestedEvent());
        }

        private void OnAgeStarted(AgeStartedEvent e) => Refresh();
        private void OnResourceChanged(ResourceChangedEvent e) => Refresh();

        // A prestige replaces the RunContext: re-bind before refreshing, or the label would keep
        // reporting the finished run's age.
        private void OnRunStarted(RunStartedEvent e)
        {
            runContext = e.Run;
            Refresh();
        }

        private void Refresh()
        {
            if (ageLabel != null && runContext != null) ageLabel.text = $"Age {runContext.currentAge}";
            if (nextAgeButton != null) nextAgeButton.interactable = ageSystem != null && ageSystem.CanAdvance;
        }
    }
}
