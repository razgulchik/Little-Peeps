using UnityEngine;

[CreateAssetMenu(menuName = "LittlePeeps/UnitDef")]
public class UnitDef : ScriptableObject
{
    public string id;
    public GameObject prefab;
    public float speed = 5f;
    public UnitType unitType;

    // Seconds after a unit leaves a house before fatigue sets in. Until it elapses the unit keeps
    // roaming and refuses to enter ANY house (even one with a free slot), so it doesn't immediately
    // duck into the house next door right after launching.
    public float fatigueDelay = 2f;
}
