using UnityEngine;

// Manages run lifecycle; creates RunContext and applies MetaContext multipliers
public class RunManager : MonoBehaviour
{
    [SerializeField] private ResourceSystem resourceSystem;
    [SerializeField] private IslandSystem islandSystem;

    private MetaContext metaContext;

    public RunContext CurrentRun { get; private set; }

    public void Initialize(MetaContext meta)
    {
        metaContext = meta;
    }

    // Create a fresh RunContext, apply global upgrade multipliers, re-generate island
    public void StartNewRun()
    {
        CurrentRun = new RunContext { currentAge = 0 };

        // TODO: seed CurrentRun.resources from a starting-amounts table, scaled by
        // GetMultiplier(MultiplierType.StartingResources). For the simplest start every
        // resource begins at 0 — ResourceSystem.Initialize fills in a ReactiveValue per type.

        resourceSystem.Initialize(CurrentRun);
        islandSystem.GenerateForRun();
    }

    // Sum valuePerLevel * level for all GlobalUpgrades matching the requested MultiplierType
    public float GetMultiplier(MultiplierType type)
    {
        // Baseline 1.0. Once the GlobalUpgradeDef catalogue is wired, add
        // def.valuePerLevel * metaContext.GetUpgradeLevel(def.id) for every upgrade of this type.
        return 1f;
    }
}
