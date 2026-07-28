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
        public int currentAge;
        public List<PerkDef> perksChosen = new();

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
