using UnityEngine;

// Manages run lifecycle; creates RunContext and applies MetaContext multipliers
public class RunManager : MonoBehaviour
{
    [SerializeField] private ResourceSystem resourceSystem;
    [SerializeField] private IslandSystem islandSystem;
    [SerializeField] private StructureSystem structureSystem;
    [SerializeField] private SpawnSystem spawnSystem;
    [SerializeField] private PierSystem pierSystem;

    [Tooltip("The run's starting state: island size, layout, resources and modifiers. One asset per " +
             "preset — swap it here to change what a fresh run begins with (useful for tests/debug).")]
    [SerializeField] private StartConfigDef startConfig;

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

        // Seed the run's starting state from the StartConfig. Everything below only holds a
        // reference to CurrentRun (stats/resources) and reads lazily, so populating it here —
        // before those systems initialise — is order-safe. A missing config is tolerated: the
        // run boots with an empty bonus layer, zero resources and IslandSystem's default size.
        if (startConfig != null)
        {
            // Bonus layer: config baseline first; ages/perks (and later meta) add theirs in-run.
            CurrentRun.stats.Add(startConfig.startingModifiers);
            SeedStartingResources();
        }

        resourceSystem.Initialize(CurrentRun);
        structureSystem.Initialize(CurrentRun);
        spawnSystem.Initialize(CurrentRun);

        if (startConfig != null) islandSystem.GenerateForRun(startConfig.islandSize);
        else                     islandSystem.GenerateForRun();

        PlaceStartingStructures();
        if (pierSystem != null) pierSystem.PlaceForRun();   // after the island exists; owns its own cell
    }

    // Fill CurrentRun.resources from the config's starting amounts, before ResourceSystem.Initialize
    // reads them. Types not listed stay absent → ResourceSystem defaults them to 0.
    private void SeedStartingResources()
    {
        var list = startConfig.startingResources;
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            var r = list[i];
            if (r != null) CurrentRun.resources[r.resourceType] = r.amount;
        }
    }

    // Instantiate the run's starting structures from the config's layout asset, through the same
    // placement path as player-built ones (grid-aligned, registered). Re-runs every new run.
    private void PlaceStartingStructures()
    {
        var layout = startConfig != null ? startConfig.layout : null;
        if (layout == null) return;
        foreach (var entry in layout.entries)
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
