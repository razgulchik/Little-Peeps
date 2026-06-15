using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "LittlePeeps/AgeDef")]
public class AgeDef : ScriptableObject
{
    public List<ResourceCost> resourceCost;
    public Vector2Int expansionSize;
    public float productionBonus;
}
