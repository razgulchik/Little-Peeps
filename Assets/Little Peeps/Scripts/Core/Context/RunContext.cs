using System.Collections.Generic;
using UnityEngine;

// Holds all data for a single run; reset on prestige
public class RunContext
{
    public Dictionary<ResourceType, float> resources = new();
    public Dictionary<Vector2Int, BuildingInstance> buildings = new();
    public int currentAge;
    public List<PerkDef> perksChosen = new();
}

// Runtime pairing of a building's definition and its live MonoBehaviour
public class BuildingInstance
{
    public BuildingDef Def;
    public Building RuntimeObject;
    public Vector2Int Cell;
}
