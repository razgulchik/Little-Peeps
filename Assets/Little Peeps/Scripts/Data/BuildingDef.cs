using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "LittlePeeps/BuildingDef")]
public class BuildingDef : ScriptableObject
{
    public string id;
    public GameObject prefab;
    public Vector2Int size = Vector2Int.one;
    public List<ResourceCost> cost;
    public List<EffectConfig> effects;
    public TerrainType allowedTerrain;
}
