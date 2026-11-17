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

        [Tooltip("A line or two on what the biome is like, under the collage.")]
        [TextArea(2, 4)] public string description;

        [Tooltip("A piece of this biome drawn from its own tiles, decor and objects, larger than the " +
                 "card's window: the window travels over it while the card is hovered (ZoneCollageView).")]
        public Sprite collage;

        [Tooltip("Colour the zone's preview is drawn in on the island while it is on offer. The alpha " +
                 "is the overlay's, not this one's.")]
        public Color previewColor = Color.white;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    }
}
