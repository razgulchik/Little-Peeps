using UnityEngine;

[CreateAssetMenu(menuName = "LittlePeeps/GlobalUpgradeDef")]
public class GlobalUpgradeDef : ScriptableObject
{
    public UpgradeId id;
    [TextArea] public string description;
    public MultiplierType multiplierType;
    public float valuePerLevel;
}
