using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Holds all data for a single run; reset on prestige
    public class RunContext
    {
        public Dictionary<ResourceType, float> resources = new();
        public Dictionary<Vector2Int, StructureInstance> structures = new();
        public Dictionary<Edge, EdgeInstance> fences = new();

        // How many of each structure stand on the island: cells and fences, generated ones included, and
        // the one MoveTool is carrying (a move is not a sale, so it never leaves this count while it is
        // off the grid). Kept by StructureSystem where structures are built and removed; read through
        // CountOf, for the build limit and the growing price (StructureDef.AtLimit / CostAt). Derivable
        // from structures + fences, so it needs no place in a save.
        public Dictionary<StructureDef, int> standing = new();

        // ReferenceEquals: the dictionary only needs a non-null key, not Unity's "destroyed equals null".
        public int CountOf(StructureDef def) =>
            !ReferenceEquals(def, null) && standing.TryGetValue(def, out int n) ? n : 0;

        // The age NUMBER the player sees: a run starts in the Age I, and every transition bought adds one.
        // Every age-gated field the designer types in (StructureDef.requiredAge, PerkDef.minAge,
        // PrestigeSystem.pierUnlockAge) is on this same scale, so it is compared as it stands — no +1
        // anywhere. Transitions bought so far = currentAge - FirstAge.
        public const int FirstAge = 1;
        public int currentAge = FirstAge;

        public List<PerkDef> perksChosen = new();

        // Structures a perk has opened this run (UnlockStructurePerkDef), read by StructureDef.LockState
        // for the ones marked lockedUntilPerk. Derived from perksChosen, so it stays rebuildable like the
        // stat sheet: replaying the chosen perks restores it.
        public HashSet<StructureDef> unlockedStructures = new();

        // Everything PRODUCED this run, per type, as credited by ResourceSystem.AddHarvest — the single
        // production gateway. Spends and sell refunds go through AddResource and never land here, so the
        // prestige payout built on this ledger cannot be farmed by cycling build → sell → build.
        // Plain numbers, so it stays inside the "run state must be rebuildable" rule for free.
        public Dictionary<ResourceType, float> harvested = new();

        // Accumulated bonus layer (base+modifiers stat system). Fresh per run → resets on prestige.
        public RunStats stats = new();
    }

    // Runtime pairing of a structure's definition and its live MonoBehaviour
    public class StructureInstance
    {
        public StructureDef Def;
        public Structure RuntimeObject;
        public Vector2Int Cell;
    }

    // Runtime pairing of an edge-placed structure (fence) with its live MonoBehaviour. Parallel to
    // StructureInstance, but keyed by the Edge it sits on instead of a cell.
    public class EdgeInstance
    {
        public StructureDef Def;
        public Structure RuntimeObject;
        public Edge Edge;
    }
}
