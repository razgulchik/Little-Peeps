using System.Collections.Generic;
using UnityEngine;

// Manages resource amounts as ReactiveValues so UI auto-updates on change
public class ResourceSystem : MonoBehaviour
{
    private readonly Dictionary<ResourceType, ReactiveValue<float>> resources = new();

    // Populate ReactiveValues from RunContext starting amounts
    public void Initialize(RunContext context)
    {
        // TODO: foreach ResourceType r, resources[r] = new ReactiveValue<float>(context.resources.GetValueOrDefault(r))
    }

    // Add (or subtract) delta; clamp to 0; publish ResourceChangedEvent
    public void AddResource(ResourceType type, float delta)
    {
        // TODO: resources[type].Value = Mathf.Max(0, resources[type].Value + delta); EventBus<ResourceChangedEvent>.Publish(...)
    }

    // Current amount for a resource type
    public float GetResource(ResourceType type)
    {
        // TODO: return resources.TryGetValue(type, out var rv) ? rv.Value : 0f
        return 0f;
    }

    // Expose ReactiveValue so UI components can subscribe to OnChanged
    public ReactiveValue<float> GetReactive(ResourceType type)
    {
        // TODO: return resources.TryGetValue(type, out var rv) ? rv : null
        return null;
    }
}
