using UnityEngine;

namespace LittlePeeps
{
    // A profession: what a worker does once it has picked a tool off a rack — and how it looks while
    // doing it. One asset per UnitType value, Unassigned included: the plain villager is a profession
    // like any other here (its frames are the bare body), so a unit always has one and nothing has to
    // special-case "none". The rack points at one of these, the unit carries it for the outing
    // (Unit.Profession), and its `type` is the id every yield table and stat scope keys on.
    //
    // The look is frames, not an animation clip: ProfessionView cycles them itself. One frame means
    // the unit walks without stepping until a second is drawn; the order is the order they cycle in.
    //
    // Numbers a profession may one day carry (a speed factor, a stamina tariff) go here as they get a
    // consumer, one field and one read each; a profession with BEHAVIOUR of its own is a subclass
    // with a hook, the same rule perks and ages follow.
    [CreateAssetMenu(menuName = "LittlePeeps/ProfessionDef")]
    public class ProfessionDef : ScriptableObject
    {
        public UnitType type;

        [Tooltip("Walk cycle, in order. One sprite = stands still while moving; two or more cycle at " +
                 "ProfessionView's frame rate. Empty = the unit keeps whatever its renderer shows.")]
        public Sprite[] frames;
    }
}
