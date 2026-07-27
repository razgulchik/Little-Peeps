using UnityEngine;

namespace LittlePeeps
{
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

        // Create a fresh RunContext, apply global upgrade multipliers, re-generate island.
        // Also the prestige entry point: it tears the previous run down FIRST, so there is exactly one
        // way to start a run and it can never be the one that leaks. On the very first call (from
        // GameBootstrap.Awake) EndRun sees no run and returns immediately. Everything below therefore
        // registers into books the teardown has just emptied — see StructureSystem.ClearAll for why
        // that ordering is the difference between a working village and one that never spawns.
        public void StartNewRun()
        {
            EndRun();

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

            // Last: the run is fully built, so observers that cache the context can safely re-bind.
            // On the FIRST run this reaches nobody — GameBootstrap.Awake publishes it before the other
            // systems' OnEnable has run, which is exactly why GameBootstrap still injects them by hand.
            // Every later run (i.e. every prestige) is carried by this event alone.
            EventBus<RunStartedEvent>.Publish(new RunStartedEvent { Run = CurrentRun });
        }

        // Debug: restart the run from the Inspector while playing (right-click the component header).
        // The only trigger that exists until prestige is wired, and useful long after as a way to reach
        // a fresh island without leaving play mode.
        //
        // Refused from build mode ON PURPOSE, matching the design rule that a run can be neither
        // prestiged nor saved while building. That rule is what makes ClearAll's sweep over
        // run.structures complete: a structure carried by MoveTool is off the grid and out of that
        // dictionary, so a run ending mid-drag would miss it — and no legitimate path can end one there.
        // This guard keeps the debug trigger from being the exception that breaks the rule.
        [ContextMenu("Restart Run")]
        private void DebugRestartRun()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Restart Run only works in play mode.", this);
                return;
            }
            if (spawnSystem != null && spawnSystem.IsBuildMode)
            {
                Debug.LogWarning("Leave build mode before restarting — a run never ends from there.", this);
                return;
            }
            StartNewRun();
        }

        // Tear the current run down to nothing: no structures, no units, no bookkeeping, no RunContext.
        // Safe to call twice, and safe to call with no run in progress.
        //
        // ORDER IS LOAD-BEARING, in both directions:
        //   1. structures first — each spawner despawns the units resting inside it as it goes, so they
        //      leave through the one path that decrements the active count. Wiping units first instead
        //      would leave those slots holding pooled Units, and the teardown would release them again.
        //   2. SpawnSystem second — it collects whatever is still roaming (units that were out and about
        //      belong to no slot) and then clears the registries the structures have just emptied.
        // Doing it the other way round double-releases into UnitPool, which has no guard against it.
        public void EndRun()
        {
            if (CurrentRun == null) return;

            if (pierSystem != null) pierSystem.ClearForRun();
            structureSystem.ClearAll();
            spawnSystem.ResetForNewRun();

            CurrentRun = null;
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
}
