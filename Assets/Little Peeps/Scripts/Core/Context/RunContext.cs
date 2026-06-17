using System.Collections.Generic;
using UnityEngine;

// Holds all data for a single run; reset on prestige
public class RunContext
{
    public Dictionary<ResourceType, float> resources = new();
    public Dictionary<Vector2Int, StructureInstance> structures = new();
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
