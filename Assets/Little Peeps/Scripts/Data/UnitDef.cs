using UnityEngine;
using UnityEngine.Serialization;

namespace LittlePeeps
{
    [CreateAssetMenu(menuName = "LittlePeeps/UnitDef")]
    public class UnitDef : ScriptableObject
    {
        public string id;
        public GameObject prefab;
        public float speed = 5f;

        // What a unit of this def is BORN as, and the key its houses and SpawnSystem count it under.
        // Not what it does: that is Unit.Profession, which a tool rack changes and a house entry
        // resets to this. A villager that gets its profession from racks is authored Unassigned.
        public UnitType unitType;

        // Stamina: seconds of field work per outing, ticking from the moment the unit leaves a house,
        // boosted or not. While it lasts the unit harvests and refuses to enter ANY house (so it never
        // ducks straight back into the house next door); at zero it is tired — harvests nothing, moves
        // at tiredSpeedMultiplier, and the next house of its type takes it in and refills it. Only a
        // house refills; a tap is speed only unless TapSystem says otherwise. 0 = tired on the doorstep.
        [FormerlySerializedAs("fatigueDelay")]
        [Min(0f)] public float stamina = 5f;

        // Speed of a tired unit as a fraction of its working speed. Never zero: a tired unit keeps
        // moving and bouncing until a house takes it in, and must stay visibly distinct from one that
        // has stopped — that is why the range starts well above 0.
        [Range(0.1f, 1f)] public float tiredSpeedMultiplier = 0.5f;
    }
}
