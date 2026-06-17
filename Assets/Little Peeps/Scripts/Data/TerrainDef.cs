using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "LittlePeeps/TerrainDef")]
public class TerrainDef : ScriptableObject
{
    public string id;
    public List<StructureDef> allowedBuildings;
    public List<UnitDef> allowedSpawners;
}
