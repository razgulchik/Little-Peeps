using System.Collections.Generic;
using UnityEngine;

// Holds all data for a single run; reset on prestige
public class RunContext
{
    public Dictionary<ResourceType, float> resources = new();
    public Dictionary<Vector2Int, StructureInstance> structures = new();
    public Dictionary<Edge, EdgeInstance> fences = new();
    public int currentAge;
    public List<PerkDef> perksChosen = new();
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
