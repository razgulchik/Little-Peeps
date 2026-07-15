using System.Collections.Generic;
using UnityEngine;

// Manages run lifecycle; creates RunContext and applies MetaContext multipliers
public class RunManager : MonoBehaviour
{
    [SerializeField] private ResourceSystem resourceSystem;
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private StructureSystem structureSystem;
    [SerializeField] private SpawnSystem spawnSystem;
    [SerializeField] private StartingLayoutDef startingLayout;

    [Header("Debug")]
    [Tooltip("Stat modifiers applied at run start for testing the bonus system before ages/perks " +
             "exist. Leave empty in production — real sources (ages/perks/meta) push their own.")]
    [SerializeField] private List<StatModifier> debugStartModifiers = new();

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

        // Seed the run's bonus layer. For now only the debug list (and, once meta is implemented,
        // MetaContext.Production etc. would be translated into StatModifiers here). Ages/perks add
        // theirs later during the run. Consumers below only hold a reference to CurrentRun.stats and
        // read lazily, so populating it here — before they initialise — is order-safe.
        CurrentRun.stats.Add(debugStartModifiers);

        // TODO: seed CurrentRun.resources from a starting-amounts table, scaled by
        // GetMultiplier(MultiplierType.StartingResources). For the simplest start every
        // resource begins at 0 — ResourceSystem.Initialize fills in a ReactiveValue per type.

        resourceSystem.Initialize(CurrentRun);
        structureSystem.Initialize(CurrentRun);
        spawnSystem.Initialize(CurrentRun);
        islandSystem.GenerateForRun();
        PlaceStartingStructures();
    }

    // Instantiate the run's starting structures from the layout asset, through the same
    // placement path as player-built ones (grid-aligned, registered). Re-runs every new run.
    private void PlaceStartingStructures()
    {
        if (startingLayout == null) return;
        foreach (var entry in startingLayout.entries)
        {
            if (entry.def == null) continue;
            structureSystem.PlaceInitial(entry.def, entry.cell);
        }
    }

    // Sum valuePerLevel * level for all GlobalUpgrades matching the requested MultiplierType
    public float GetMultiplier(MultiplierType type)
    {
        // Baseline 1.0. Once the GlobalUpgradeDef catalogue is wired, add
        // def.valuePerLevel * metaContext.GetUpgradeLevel(def.id) for every upgrade of this type.
        return 1f;
    }
}
