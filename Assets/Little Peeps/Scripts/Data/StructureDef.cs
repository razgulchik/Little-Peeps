using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "LittlePeeps/StructureDef")]
public class StructureDef : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public GameObject prefab;
    public Vector2Int size = Vector2Int.one;
    public List<ResourceCost> cost;
    public List<EffectConfig> effects;

    // Biomes this structure may be placed on. Empty/null = any terrain.
    public TerrainType[] allowedTerrain;

    // Fraction of build cost refunded when the structure is sold (0..1).
    [Range(0f, 1f)] public float sellRefundPercent = 0.5f;

    // Required clear margin (in cells) around the footprint: those cells must be on-island and free
    // of OTHER bordered structures. Keeps spawner-buildings off the map edge and apart from each
    // other (e.g. house = 1); passive structures like trees/fields use 0 (no margin, may sit anywhere).
    public int border = 0;
}
