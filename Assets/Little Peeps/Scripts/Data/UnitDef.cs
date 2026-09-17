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

        // What a unit of this def is BORN as: the profession it leaves the house with and comes back
        // to at the end of every outing (Unit.Unequip). The doc's villager points at the Unassigned
        // profession and gets a real one from a rack; a def born with a profession (the old Farmer)
        // never takes a tool because it is never Unassigned. Empty reads as Unassigned with no look
        // of its own — assign one. Population is counted per UnitDef, not per profession: a house
        // spawns "this kind of unit", whatever it goes on to do.
        public ProfessionDef profession;

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
