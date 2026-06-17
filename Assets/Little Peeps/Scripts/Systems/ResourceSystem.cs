using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Manages resource amounts as ReactiveValues so UI auto-updates on change
public class ResourceSystem : MonoBehaviour
{
    [SerializeField] private bool logChanges = true; // debug: dump all resources to console on each change

    private readonly Dictionary<ResourceType, ReactiveValue<float>> resources = new();

    // Populate ReactiveValues from RunContext starting amounts (one per ResourceType)
    public void Initialize(RunContext context)
    {
        resources.Clear();
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            float start = context.resources.TryGetValue(type, out var v) ? v : 0f;
            resources[type] = new ReactiveValue<float>(start);
        }
    }

    // Add (or subtract) delta; clamp to 0; publish ResourceChangedEvent
    public void AddResource(ResourceType type, float delta)
    {
        if (!resources.TryGetValue(type, out var rv)) return;

        rv.Value = Mathf.Max(0f, rv.Value + delta);
        EventBus<ResourceChangedEvent>.Publish(new ResourceChangedEvent
        {
            ResourceType = type,
            NewValue     = rv.Value,
        });

        if (logChanges) LogChange(type, delta);
    }

    // Debug: print the changed resource plus all current totals to the console.
    private void LogChange(ResourceType changed, float delta)
    {
        var sb = new StringBuilder();
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(t).Append('=').Append(GetResource(t));
        }
        Debug.Log($"[Resources] {(delta >= 0 ? "+" : "")}{delta} {changed}  →  {sb}");
    }

    // Current amount for a resource type
    public float GetResource(ResourceType type)
    {
        return resources.TryGetValue(type, out var rv) ? rv.Value : 0f;
    }

    // Expose ReactiveValue so UI components can subscribe to OnChanged
    public ReactiveValue<float> GetReactive(ResourceType type)
    {
        return resources.TryGetValue(type, out var rv) ? rv : null;
    }
}
