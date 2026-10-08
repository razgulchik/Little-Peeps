using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Every meta perk the screen after a prestige offers. Its own asset, like PerkCatalogueDef, so adding a
    // meta perk is a diff in this file and not in SampleScene.unity. Registration is explicit: a half-made
    // upgrade can sit in the project without showing up on the screen.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Upgrade Catalogue")]
    public class GlobalUpgradeCatalogueDef : ScriptableObject
    {
        [Tooltip("Every meta perk on offer, listed on the meta screen top to bottom in this order.")]
        public List<GlobalUpgradeDef> upgrades = new();
    }
}
