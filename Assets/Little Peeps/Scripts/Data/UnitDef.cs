using UnityEngine;

[CreateAssetMenu(menuName = "LittlePeeps/UnitDef")]
public class UnitDef : ScriptableObject
{
    public string id;
    public GameObject prefab;
    public float speed = 5f;
    public UnitType unitType;
}
