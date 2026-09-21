using UnityEngine;

namespace LittlePeeps
{
    // A biome as an asset: the content profile the generator works from plus how the zone looks. One
    // per biome the player can be offered (meadow, forest, desert) and one for the starting zone, which
    // has its own gentler profile. Ages don't own biomes — every age offers the whole catalogue on
    // IslandSystem, one zone per biome.
    [CreateAssetMenu(menuName = "LittlePeeps/BiomeDef")]
    public class BiomeDef : ScriptableObject
    {
        [Tooltip("Budgets and object rules. This is what the generator reads.")]
        public IslandBiome profile = new();

        [Tooltip("Ground and coastline tiles for zones of this biome. Empty = IslandSystem's default set.")]
        public IslandTileSet tileSet;

        [Header("Zone card")]
        [Tooltip("Name on the zone card. Empty = the asset's name.")]
        public string displayName;

        public Sprite icon;

        [Tooltip("Colour the zone's preview is drawn in on the island while it is on offer. The alpha " +
                 "is the overlay's, not this one's.")]
        public Color previewColor = Color.white;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    }
}
