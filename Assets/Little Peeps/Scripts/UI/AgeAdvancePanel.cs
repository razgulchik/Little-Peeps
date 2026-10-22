using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

namespace LittlePeeps
{
    // The age panel's own part: the age we are IN with the bonus it grants, and the Expand Island
    // button — the one control that BUYS an age. The price under it is AgeCostPanel's, which only reports.
    //
    // The button just publishes an intent (AgeAdvanceRequestedEvent) — GameplayContainerState decides
    // whether the transition may start. The button's interactable state tracks affordability
    // (recomputed on every resource change / age start).
    public class AgeAdvancePanel : MonoBehaviour, IUIElement
    {
        [Header("The age we are in")]
        [Tooltip("\"AGE IV\". Left-aligned, Baseline: the script centres it on whole pixels.")]
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("The bonus up to its first space — \"+10%\". Left-aligned, Baseline, centred by the script.")]
        [SerializeField] private TMP_Text bonusValueLabel;

        [Tooltip("The rest of the bonus — \"ALL PRODUCTION\". Left-aligned, Baseline, centred by the script.")]
        [SerializeField] private TMP_Text bonusTextLabel;

        [Space]
        [SerializeField] private Button nextAgeButton;

        private AgeSystem ageSystem;

        // The age we are standing IN. Taken from event payloads rather than read back off AgeSystem, so
        // the panel never depends on which of the two handled RunStartedEvent first. Same reasoning as
        // AgeCostPanel.
        private int currentAge = RunContext.FirstAge;

        // From UIRoot once the run exists; RunStartedEvent replaces it.
        public void Initialize(UIServices ui)
        {
            ageSystem = ui.AgeSystem;
            RunContext run = ui.RunManager != null ? ui.RunManager.CurrentRun : null;
            if (run != null) currentAge = run.currentAge;
        }

        private void Start()
        {
            RefreshAge();
            RefreshButton();
        }

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

        private void OnAgeStarted(AgeStartedEvent e)
        {
            currentAge = e.Age;
            RefreshAge();
            RefreshButton();
        }

        private void OnResourceChanged(ResourceChangedEvent e) => RefreshButton();

        // A prestige starts the ages over — show the new run's age, not the finished one's.
        private void OnRunStarted(RunStartedEvent e)
        {
            currentAge = e.Run != null ? e.Run.currentAge : RunContext.FirstAge;
            RefreshAge();
            RefreshButton();
        }

        // The bonus reads "+10% ALL PRODUCTION" in the data and stands in two lines here, the amount over
        // what it applies to. The Age I has no AgeDef (never bought), so it shows no bonus at all.
        private void RefreshAge()
        {
            AgeDef def = ageSystem != null ? ageSystem.DefOf(currentAge) : null;
            string bonus = def != null ? def.BonusText.Trim() : string.Empty;
            int space = bonus.IndexOf(' ');

            SetCentred(titleLabel, $"AGE {RomanNumeral.From(currentAge)}");
            SetCentred(bonusValueLabel, space < 0 ? bonus : bonus.Substring(0, space));
            SetCentred(bonusTextLabel, space < 0 ? string.Empty : bonus.Substring(space + 1).Trim());
        }

        private void RefreshButton()
        {
            if (nextAgeButton != null) nextAgeButton.interactable = ageSystem != null && ageSystem.CanAdvance;
        }

        // Centred by hand, on whole pixels: TMP's own centring puts a line whose width differs in parity
        // from its box half a pixel off, and a pixel font drawn between pixels comes out blurred. Centred by
        // the INK, not the advance — a pixel font's advance carries the empty column after the last letter,
        // which would sit every word a pixel left of the middle. The label is held by its parent's left edge.
        private static void SetCentred(TMP_Text label, string text)
        {
            if (label == null) return;

            label.text = text;
            label.ForceMeshUpdate(true);

            float left = float.MaxValue, right = float.MinValue;
            TMP_TextInfo info = label.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible) continue;

                GlyphMetrics glyph = c.textElement.glyph.metrics;
                float start = c.origin + glyph.horizontalBearingX * c.scale;
                left = Mathf.Min(left, start);
                right = Mathf.Max(right, start + glyph.width * c.scale);
            }

            if (left > right) return;   // nothing visible — nothing to centre

            RectTransform box = label.rectTransform;
            float width = ((RectTransform)box.parent).rect.width;
            float ink = Mathf.Round(right - left);
            float x = Mathf.Floor((width - ink) / 2f) - Mathf.Round(left);
            box.anchoredPosition = new Vector2(x, box.anchoredPosition.y);
        }
    }
}
