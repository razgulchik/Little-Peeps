using TMPro;
using UnityEngine;

// Displays a single resource amount; auto-updates via ReactiveValue subscription
public class ResourceUI : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private TMP_Text label;

    private ResourceSystem resourceSystem;

    public void Initialize(ResourceSystem resourceSystem)
    {
        this.resourceSystem = resourceSystem;
        // TODO: var rv = resourceSystem.GetReactive(resourceType); rv.OnChanged += OnValueChanged; OnValueChanged(rv.Value)
    }

    private void OnDestroy()
    {
        // TODO: resourceSystem.GetReactive(resourceType).OnChanged -= OnValueChanged (prevent memory leak)
    }

    private void OnValueChanged(float newValue)
    {
        // TODO: label.text = FormatNumber(newValue) — abbreviate large values (1.2k, 3.5M, etc.)
    }
}
