using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Every structure in the game: the buildings of the palette, the natural features the biomes grow, the
    // start structures (house, pier, tent). A saved run stores what stood on its island by StructureDef.id and
    // finds the def here again (RunManager.TryRestoreRun) — the one place that knows them all, since the
    // palette, the biomes and the start config each know only their own.
    //
    // Registration is explicit, like the perk catalogue's: create the asset, then add it here. A structure
    // left out is dropped from every continued run (with a warning in the console), so the EditMode test
    // StructureCatalogueTests fails as soon as a StructureDef asset is missing from the list.
    [CreateAssetMenu(menuName = "LittlePeeps/Structure Catalogue")]
    public class StructureCatalogueDef : ScriptableObject
    {
        [Tooltip("Every StructureDef asset. Order does not matter. A new structure goes in here, or continued " +
                 "runs lose it.")]
        public List<StructureDef> structures = new();

        public StructureDef Find(string id)
        {
            if (string.IsNullOrEmpty(id) || structures == null) return null;
            foreach (var def in structures)
                if (def != null && def.id == id) return def;
            return null;
        }
    }
}
