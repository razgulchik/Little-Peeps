using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Manages resource amounts as ReactiveValues so UI auto-updates on change
public class ResourceSystem : MonoBehaviour
{
    [SerializeField] private bool logChanges = true; // debug: dump all resources to console on each change

    private readonly Dictionary<ResourceType, ReactiveValue<float>> resources = new();

    // The run's bonus layer, held so harvest gains can be scaled by yield/production modifiers.
    private RunStats stats;

    // Populate ReactiveValues from RunContext starting amounts (one per ResourceType)
    public void Initialize(RunContext context)
    {
        stats = context.stats;

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

    // Credit a resource GAIN from a worker harvesting a source: applies the per-(worker, resource)
    // yield modifier, then the global production multiplier, then adds the result. This is the one
    // gateway for production — route every resource-generating path through it. AddResource/Spend
    // stay raw for spends, refunds and exact changes (which must NOT be production-boosted).
    public void AddHarvest(ResourceType type, UnitType worker, float baseAmount)
    {
        float amount = stats != null
            ? stats.Apply(stats.Apply(baseAmount, StatId.ResourceYield, worker, type), StatId.ProductionGlobal)
            : baseAmount;
        AddResource(type, amount);
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

    // True if every entry in the cost list is currently affordable.
    public bool CanAfford(List<ResourceCost> cost)
    {
        if (cost == null) return true;
        for (int i = 0; i < cost.Count; i++)
            if (GetResource(cost[i].resourceType) < cost[i].amount) return false;
        return true;
    }

    // Deduct every entry in the cost list. Caller is responsible for checking CanAfford first.
    public void Spend(List<ResourceCost> cost)
    {
        if (cost == null) return;
        for (int i = 0; i < cost.Count; i++)
            AddResource(cost[i].resourceType, -cost[i].amount);
    }
}
