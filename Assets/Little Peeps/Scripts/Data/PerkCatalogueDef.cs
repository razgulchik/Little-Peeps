using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Every perk that can be rolled. Its own asset rather than a list on the PerkSystem component: the
    // catalogue is heading for ~100 entries, and holding it in the scene would make "add a perk" a diff
    // in SampleScene.unity every single time.
    //
    // Registration stays explicit (create the asset, then add it here) instead of scanning a folder, so
    // a half-finished perk can sit in the project without turning up in a run. Setting weight to 0 is
    // the other off-switch, for a perk that is already listed.
    [CreateAssetMenu(menuName = "LittlePeeps/Perk Catalogue")]
    public class PerkCatalogueDef : ScriptableObject
    {
        [Tooltip("Every rollable perk. Order does not matter — the roll is weighted, not sequential.")]
        public List<PerkDef> perks = new();
    }
}
