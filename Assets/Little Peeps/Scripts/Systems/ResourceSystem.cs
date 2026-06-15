using System;
using System.Collections.Generic;
using UnityEngine;

// Manages resource amounts as ReactiveValues so UI auto-updates on change
public class ResourceSystem : MonoBehaviour
{
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
