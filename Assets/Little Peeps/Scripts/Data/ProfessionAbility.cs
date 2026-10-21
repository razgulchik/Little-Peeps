using UnityEngine;

namespace LittlePeeps
{
    // Something a profession can DO besides harvesting by contact — the hunter's bow is the first.
    // A ProfessionDef lists these (ProfessionDef.abilities); every unit wearing that profession gets
    // them for the outing, and the unit's UnitAbilities runs them.
    //
    // The asset holds the numbers and the mechanics, never the state: one asset serves every unit of
    // the profession at once, and in the editor a field written in Play Mode would even stay in the
    // asset. So the asset is a FACTORY — CreateState makes one small runtime object per unit (its
    // cooldown, its target), and that object is what ticks. Fields here are read-only at run time.
    //
    // An abstract ScriptableObject and not an interface because a ProfessionDef has to hold the assets
    // in the inspector, and Unity serializes a reference only to a UnityEngine.Object type.
    public abstract class ProfessionAbility : ScriptableObject
    {
        // A fresh state for one unit that has just taken up the profession. Called by UnitAbilities each
        // time the unit's profession changes to one listing this asset, so an outing never inherits the
        // previous one's state. Null = nothing to run for this unit.
        public abstract IAbilityState CreateState(Unit unit);
    }
}
