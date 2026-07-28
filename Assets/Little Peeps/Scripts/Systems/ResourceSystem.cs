using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LittlePeeps
{
    // Manages resource amounts as ReactiveValues so UI auto-updates on change
    public class ResourceSystem : MonoBehaviour
    {
        [SerializeField] private bool logChanges = true; // debug: dump all resources to console on each change

        private readonly Dictionary<ResourceType, ReactiveValue<float>> resources = new();

        // The run's bonus layer, held so harvest gains can be scaled by yield/production modifiers.
        private RunStats stats;

        // The run's production ledger (RunContext.harvested), held so AddHarvest can credit it. Bound the
        // same way as `stats`: the dictionary belongs to the RunContext, so a new run brings a new one and
        // the totals reset with it — nothing here has to remember to zero anything.
        private Dictionary<ResourceType, float> harvested;

        // Populate ReactiveValues from RunContext starting amounts (one per ResourceType)
        public void Initialize(RunContext context)
        {
            stats = context.stats;
            harvested = context.harvested;

            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                float start = context.resources.TryGetValue(type, out var v) ? v : 0f;

                // REUSE the ReactiveValue; never replace it. The slot belongs to the resource TYPE,
                // which outlives every run — only the amount inside it belongs to the run. Replacing the
                // object on a new run would strand every subscriber on the finished run's instance,
                // which nothing writes to again: ResourcePanel binds once in Start(), so the whole
                // resource bar would freeze at whatever it read the moment the player prestiged, while
                // the real numbers moved on invisibly.
                //
                // Assigning also publishes the reset for free, so the bar visibly drops to the new run's
                // starting amounts instead of needing a second notification path to say so.
                if (resources.TryGetValue(type, out var rv)) rv.Value = start;
                else resources[type] = new ReactiveValue<float>(start);
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

        // Credit a resource GAIN from a worker harvesting a source: applies the per-(worker, resource,
        // source) yield modifier, then the global production multiplier, then adds the result. This is
        // the one gateway for production — route every resource-generating path through it.
        // AddResource/Spend stay raw for spends, refunds and exact changes (which must NOT be
        // production-boosted).
        //
        // It takes the whole ResourceSourceDef rather than just its ResourceType because the def IS the
        // yield modifier's third scope: Market and Alpaka are both Coins, Wheat and Boar are both Food,
        // so a bonus meant for one would otherwise land on the other as well.
        public void AddHarvest(ResourceSourceDef source, UnitType worker, float baseAmount)
        {
            if (source == null) return;

            ResourceType type = source.resource;
            float amount = stats != null
                ? stats.Apply(stats.Apply(baseAmount, StatId.ResourceYield, worker, type, source),
                              StatId.ProductionGlobal)
                : baseAmount;

            // Book the run's production for the prestige payout. The CREDITED amount, after both
            // multipliers — a better-built village is worth more prestige, which is the whole point of
            // paying on production rather than on time.
            if (harvested != null)
                harvested[type] = (harvested.TryGetValue(type, out float total) ? total : 0f) + amount;

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
}
