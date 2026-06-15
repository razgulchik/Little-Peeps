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
        // TODO: CurrentRun = new RunContext(); populate resources with starting amounts * GetMultiplier(Production)
        // TODO: resourceSystem.Initialize(CurrentRun); islandSystem.Generator.Generate(0)
    }

    // Sum valuePerLevel * level for all GlobalUpgrades matching the requested MultiplierType
    public float GetMultiplier(MultiplierType type)
    {
        // TODO: base = 1f; iterate metaContext.globalUpgrades, add def.valuePerLevel * level for matching MultiplierType; return base
        return 1f;
    }
}
