using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Manages run lifecycle: creates and tears down RunContext — fresh (StartNewRun) or from a saved run
    // (TryRestoreRun), and the snapshot a run is saved from (Capture).
    public class RunManager : MonoBehaviour
    {
        [SerializeField] private ResourceSystem resourceSystem;
        [SerializeField] private IslandSystem islandSystem;
        [SerializeField] private StructureSystem structureSystem;
        [SerializeField] private SpawnSystem spawnSystem;

        [Tooltip("The run's starting state: island seed, rules and start biome, resources and modifiers. One asset per " +
                 "preset — swap it here to change what a fresh run begins with (useful for tests/debug).")]
        [SerializeField] private StartConfigDef startConfig;

        [Tooltip("Every structure in the game. A saved run puts its structures back by id through it; one missing " +
                 "here is dropped from every continued run.")]
        [SerializeField] private StructureCatalogueDef structureCatalogue;

        public RunContext CurrentRun { get; private set; }

        // Where the meta perks come from: every run starts with the levels bought with prestige points.
        // Injected by GameBootstrap before the first run rather than wired in the scene, so it cannot be
        // left unassigned — a forgotten field here would silently start every run without them. Null in
        // tests and in a scene without bootstrap: the run then starts with none.
        private PrestigeSystem metaUpgrades;

        // What a saved run is rebuilt with: the ages and the perks replay their stat modifiers, and the saved
        // run is let go whenever a fresh one starts. Injected like the meta perks; null in tests.
        private AgeSystem ageSystem;
        private PerkSystem perkSystem;
        private SaveSystem saveSystem;

        public void Initialize(PrestigeSystem prestigeSystem, AgeSystem ages = null, PerkSystem perks = null,
                               SaveSystem saves = null)
        {
            metaUpgrades = prestigeSystem;
            ageSystem = ages;
            perkSystem = perks;
            saveSystem = saves;
        }

        // Another start config for every run from the next StartNewRun on (TestMode). Changes only this
        // play session: the scene keeps the real one.
        public void UseStartConfig(StartConfigDef config) => startConfig = config;

        // Create a fresh RunContext and re-generate the island.
        // Also the prestige entry point: it tears the previous run down FIRST, so there is exactly one
        // way to start a run and it can never be the one that leaks. On the very first call (from
        // GameBootstrap.Awake) EndRun sees no run and returns immediately. Everything below therefore
        // registers into books the teardown has just emptied — see StructureSystem.ClearAll for why
        // that ordering is the difference between a working village and one that never spawns.
        public void StartNewRun()
        {
            // A fresh run replaces whatever run was saved: it must never come back after this one has begun.
            if (saveSystem != null) saveSystem.DiscardRun();

            EndRun();

            CurrentRun = new RunContext();   // starts in RunContext.FirstAge

            // Seed the run's starting state from the StartConfig. Everything below only holds a
            // reference to CurrentRun (stats/resources) and reads lazily, so populating it here —
            // before those systems initialise — is order-safe. A missing config is tolerated: the
            // run boots with an empty bonus layer, zero resources and a random-seed default island.
            if (startConfig != null)
            {
                // Bonus layer: config baseline first; ages/perks add theirs in-run.
                CurrentRun.stats.Add(startConfig.startingModifiers);
                SeedStartingResources();
                GrantStartingUpgrades();
            }

            // Then the meta perks bought with prestige — a run-start bonus like the config's, so it goes
            // into the sheet here, before any system below can read it. Outside the config check: a
            // missing config must not cost the player what they paid for.
            if (metaUpgrades != null) metaUpgrades.ApplyUpgrades(CurrentRun);

            resourceSystem.Initialize(CurrentRun);
            structureSystem.Initialize(CurrentRun);
            spawnSystem.Initialize(CurrentRun);

            // The run's seed — the config's when it names one (a test island), else a fresh one — is chosen HERE,
            // not by the island: every roll of the run is derived from it too (RunContext.seed).
            int seed = startConfig != null ? startConfig.islandSeed : 0;
            if (seed == 0) seed = UnityEngine.Random.Range(1, int.MaxValue);
            CurrentRun.seed = seed;

            // The island brings its own starting content (house, pier, food, wood, and what a meta perk adds)
            // through StructureSystem.
            if (startConfig != null) islandSystem.GenerateForRun(seed, startConfig.islandRules, startConfig.startBiome, startConfig.house, startConfig.pier, CurrentRun.startStructures);
            else                     islandSystem.GenerateForRun(seed);

            // Last: the run is fully built, so observers that cache the context can safely re-bind.
            // On the FIRST run this reaches nobody — GameBootstrap.Awake publishes it before the other
            // systems' OnEnable has run, which is exactly why GameBootstrap still injects them by hand.
            // Every later run (i.e. every prestige) is carried by this event alone.
            EventBus<RunStartedEvent>.Publish(new RunStartedEvent { Run = CurrentRun });
        }

        // Debug: restart the run from the Inspector while playing (right-click the component header).
        // A way to reach a fresh island without leaving play mode — and without a prestige, so no points
        // move; the meta perks already bought still apply, since StartNewRun is the same path. Fine with
        // the build panel up too: nothing is ever held off the grid, so ClearAll's sweep is complete.
        [ContextMenu("Restart Run")]
        private void DebugRestartRun()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Restart Run only works in play mode.", this);
                return;
            }
            StartNewRun();
        }

        // Tear the current run down to nothing: no structures, no units, no bookkeeping, no RunContext.
        // Safe to call twice, and safe to call with no run in progress.
        //
        // The order:
        //   1. structures first — each spawner gives its capacity back and despawns the units resting
        //      inside it as it goes, against books that are still whole.
        //   2. SpawnSystem second — it collects whatever is still roaming (units that were out and about
        //      belong to no slot) and then clears the registries the structures have just emptied.
        public void EndRun()
        {
            if (CurrentRun == null) return;

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

        // The meta perks the config itself gives. After its resources: a meta perk adds to those.
        private void GrantStartingUpgrades()
        {
            var list = startConfig.startingUpgrades;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++) CurrentRun.Grant(list[i]);
        }

        // --- Saved runs ----------------------------------------------------------------------------

        // The run as it stands, as plain data (RunSnapshot) — a copy, so the game may go on changing the run
        // while the snapshot waits to be written. Null with no run or no island.
        public RunSnapshot Capture()
        {
            var run = CurrentRun;
            var generator = islandSystem != null ? islandSystem.Generator : null;
            if (run == null || generator == null) return null;

            var save = new RunSnapshot
            {
                seed = generator.Seed,
                perkRolls = run.perkRolls,
                currentAge = run.currentAge,
                offeringCycle = run.offeringCycle,
                offeringElapsed = run.offeringElapsed,
                playSeconds = run.playSeconds,
                perkPicksOwed = run.perkPicksOwed,
                freeAgesLeft = run.freeAgesLeft,
                tiredGoHome = run.tiredGoHome,
            };

            foreach (var section in generator.Sections) save.sections.Add(IslandSnapshot.Save(section));

            AddStructures(run.structures.Values, save.structures);
            AddStructures(run.bridges.Values, save.bridges);
            foreach (var fence in run.fences.Values)
            {
                if (fence?.Def == null || string.IsNullOrEmpty(fence.Def.id)) continue;
                save.fences.Add(new FenceSave
                {
                    id = fence.Def.id,
                    x = fence.Edge.anchor.x,
                    y = fence.Edge.anchor.y,
                    horizontal = fence.Edge.horizontal,
                    site = fence.UnderConstruction,
                });
            }

            AddAmounts(run.resources, save.resources);
            AddAmounts(run.harvested, save.harvested);

            foreach (var perk in run.perksChosen)
                if (perk != null) save.perks.Add(perk.id);
            foreach (var upgrade in run.upgrades)
                if (upgrade != null) save.upgrades.Add(upgrade.id);

            return save;
        }

        // Put a saved run back as the current run: its island, every structure where it stood, its numbers —
        // and the stat sheet rebuilt from what built it, in the order StartNewRun and the run itself built it:
        // the start config, the run's meta perks, the ages, the perks. The units come fresh, each in its house,
        // as at a run's start (the decision behind that: RunSnapshot). Like StartNewRun, it tears the previous
        // run down first and announces the new one last.
        //
        // False when the save cannot be put back — its island no longer reads (a biome it grew is gone); the
        // caller then starts a fresh run, which replaces whatever was half built here. What is merely gone from
        // the game (a structure, a perk, a meta perk) is left out with a warning, and the run goes on.
        public bool TryRestoreRun(RunSnapshot save)
        {
            if (save == null || islandSystem == null) return false;

            // Without it every structure would be "gone from the game" and the run would come back as a bare
            // island — refused instead, loudly; the save is set aside, not lost (GameBootstrap).
            if (structureCatalogue == null)
            {
                Debug.LogError("[RunManager] no Structure Catalogue assigned — the saved run cannot put its structures " +
                               "back. Assign 'Structure Catalogue Def' on RunManager.", this);
                return false;
            }

            EndRun();
            var run = new RunContext();
            CurrentRun = run;

            run.seed = save.seed;
            run.perkRolls = Mathf.Max(0, save.perkRolls);
            run.currentAge = Mathf.Max(RunContext.FirstAge, save.currentAge);
            run.offeringCycle = Mathf.Max(1, save.offeringCycle);
            run.offeringElapsed = Math.Max(0.0, save.offeringElapsed);
            run.playSeconds = Math.Max(0.0, save.playSeconds);
            run.perkPicksOwed = Mathf.Max(0, save.perkPicksOwed);
            run.freeAgesLeft = Mathf.Max(0, save.freeAgesLeft);
            run.tiredGoHome = save.tiredGoHome;

            var missing = new List<string>();
            ReadAmounts(save.resources, run.resources, missing);
            ReadAmounts(save.harvested, run.harvested, missing);
            RebuildStats(run, save, missing);

            resourceSystem.Initialize(run);
            structureSystem.Initialize(run);
            spawnSystem.Initialize(run);

            if (!islandSystem.TryRestoreForRun(save.seed, startConfig != null ? startConfig.islandRules : null,
                                               startConfig != null ? startConfig.startBiome : null, save.sections,
                                               out string problem))
            {
                Debug.LogError($"[RunManager] the saved run cannot be continued: {problem}. A new run starts instead.",
                               this);
                return false;
            }

            // Ground first, then what stands over water (it needs its river under it), then the fences.
            foreach (var s in save.structures) RestoreStructure(s, missing);
            foreach (var s in save.bridges) RestoreStructure(s, missing);
            foreach (var f in save.fences)
            {
                var def = FindStructure(f.id);
                if (def == null) { missing.Add($"structure '{f.id}'"); continue; }
                if (!structureSystem.RestoreEdgeStructure(def, new Edge(new Vector2Int(f.x, f.y), f.horizontal), f.site))
                    missing.Add($"'{f.id}' on the edge at ({f.x}, {f.y}) — the edge no longer takes it");
            }

            if (missing.Count > 0)
                Debug.LogWarning($"[RunManager] the saved run is continued without {missing.Count} thing(s) no longer " +
                                 $"in the game or no longer fitting: {string.Join("; ", missing)}.", this);

            // Last, as in StartNewRun: the run is fully built, so observers that cache the context can re-bind.
            EventBus<RunStartedEvent>.Publish(new RunStartedEvent { Run = run });
            return true;
        }

        // The stat sheet a saved run had, from what built it. Never stored: every modifier in it comes from the
        // start config, a meta perk, an age or a perk, so stored modifiers could only disagree with those. The
        // meta perks add only their stats (GlobalUpgradeDef.AddStats) — their one-time grants are already in
        // the saved numbers — and the perks replay level by level, which also reopens the structures they
        // unlock.
        private void RebuildStats(RunContext run, RunSnapshot save, List<string> missing)
        {
            if (startConfig != null) run.stats.Add(startConfig.startingModifiers);

            foreach (string id in save.upgrades)
            {
                var upgrade = FindUpgrade(id);
                if (upgrade == null) { missing.Add($"meta perk '{id}'"); continue; }
                if (run.upgrades.Contains(upgrade)) continue;

                run.upgrades.Add(upgrade);
                upgrade.AddStats(run.stats);
            }

            if (ageSystem != null)
                for (int age = RunContext.FirstAge + 1; age <= run.currentAge; age++)
                {
                    var def = ageSystem.DefOf(age);
                    if (def != null) run.stats.Add(def.modifiers);
                }

            foreach (string id in save.perks)
            {
                var perk = perkSystem != null ? perkSystem.FindPerk(id) : null;
                if (perk == null) { missing.Add($"perk '{id}'"); continue; }

                int level = run.PerkLevel(perk) + 1;
                if (level > perk.MaxLevel) continue;
                perk.ApplyPerk(run, level);
                run.perksChosen.Add(perk);
            }
        }

        private void RestoreStructure(StructureSave s, List<string> missing)
        {
            var def = FindStructure(s.id);
            if (def == null) { missing.Add($"structure '{s.id}'"); return; }

            var instance = structureSystem.RestoreStructure(def, new Vector2Int(s.x, s.y), s.site);
            if (instance == null)
            {
                missing.Add($"'{s.id}' at ({s.x}, {s.y}) — the spot no longer takes it");
                return;
            }

            if (!s.site && s.states.Count > 0) LoadStates(instance, s, missing);
        }

        // Its parts' states back onto the structure just built — before any Start of theirs (IRunState). Matched
        // in order and by kind: a part whose kind is not where the save had it — the prefab changed since —
        // keeps its fresh state, and is reported once with the structure.
        private static void LoadStates(StructureInstance instance, StructureSave s, List<string> missing)
        {
            if (instance.RuntimeObject == null) return;

            var parts = instance.RuntimeObject.GetComponentsInChildren<IRunState>(true);
            bool mismatch = parts.Length != s.states.Count;
            for (int i = 0; i < parts.Length && i < s.states.Count; i++)
            {
                if (parts[i].StateKey != s.states[i].key) { mismatch = true; continue; }
                parts[i].LoadState(s.states[i].values);
            }
            if (mismatch)
                missing.Add($"part of the state of '{s.id}' at ({s.x}, {s.y}) — its prefab changed since the save");
        }

        // Its parts' states, in the order they are found — the order LoadStates puts them back in.
        private static void SaveStates(StructureInstance instance, List<StateSave> into)
        {
            if (instance.UnderConstruction || instance.RuntimeObject == null) return;

            foreach (var part in instance.RuntimeObject.GetComponentsInChildren<IRunState>(true))
            {
                var state = new StateSave { key = part.StateKey };
                part.SaveState(state.values);
                into.Add(state);
            }
        }

        private StructureDef FindStructure(string id) =>
            structureCatalogue != null ? structureCatalogue.Find(id) : null;

        // The meta perks a run can hold come from the profile's catalogue or from the start config.
        private GlobalUpgradeDef FindUpgrade(string id)
        {
            var found = metaUpgrades != null ? metaUpgrades.FindUpgrade(id) : null;
            if (found != null || startConfig == null || startConfig.startingUpgrades == null) return found;

            foreach (var upgrade in startConfig.startingUpgrades)
                if (upgrade != null && upgrade.id == id) return upgrade;
            return null;
        }

        // In cell order, so the same run always writes the same file.
        private static void AddStructures(IEnumerable<StructureInstance> instances, List<StructureSave> into)
        {
            var sorted = new List<StructureInstance>();
            foreach (var instance in instances)
                if (instance?.Def != null && !string.IsNullOrEmpty(instance.Def.id)) sorted.Add(instance);
            sorted.Sort((a, b) => a.Cell.y != b.Cell.y ? a.Cell.y.CompareTo(b.Cell.y) : a.Cell.x.CompareTo(b.Cell.x));

            foreach (var instance in sorted)
            {
                var save = new StructureSave
                {
                    id = instance.Def.id,
                    x = instance.Cell.x,
                    y = instance.Cell.y,
                    site = instance.UnderConstruction,
                };
                SaveStates(instance, save.states);
                into.Add(save);
            }
        }

        private static void AddAmounts(Dictionary<ResourceType, float> amounts, List<AmountSave> into)
        {
            foreach (var pair in amounts)
                into.Add(new AmountSave { type = pair.Key.ToString(), amount = pair.Value });
        }

        private static void ReadAmounts(List<AmountSave> saved, Dictionary<ResourceType, float> into, List<string> missing)
        {
            foreach (var amount in saved)
            {
                if (amount != null && Enum.TryParse(amount.type, out ResourceType type)) into[type] = amount.amount;
                else missing.Add($"resource '{amount?.type}'");
            }
        }
    }
}
