using UnityEngine;

namespace LittlePeeps
{
    [CreateAssetMenu(menuName = "LittlePeeps/UnitDef")]
    public class UnitDef : ScriptableObject
    {
        public string id;
        public GameObject prefab;
        public float speed = 5f;
        public UnitType unitType;

        // Seconds of WORK a unit gets after its launch boost settles, before fatigue sets in. Until then
        // it harvests and refuses to enter ANY house (so it never ducks straight back into the house next
        // door); once tired it harvests nothing and the next house of its type takes it in. A tap
        // re-launches and restarts the whole thing. 0 is not "off": it means the unit works only while
        // boosted — tired the moment its boost settles.
        public float fatigueDelay = 2f;
    }
}
