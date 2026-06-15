using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows current age label and the Next Age button
public class AgeUI : MonoBehaviour
{
    [SerializeField] private TMP_Text ageLabel;
    [SerializeField] private Button nextAgeButton;

    private void OnEnable()
    {
        // TODO: EventBus<AgeStartedEvent>.Subscribe(OnAgeStarted)
    }

    private void OnDisable()
    {
        // TODO: EventBus<AgeStartedEvent>.Unsubscribe(OnAgeStarted)
    }

    private void OnAgeStarted(AgeStartedEvent e)
    {
        // TODO: ageLabel.text = $"Age {e.Age}"; update nextAgeButton.interactable based on whether next age cost is affordable
    }
}
