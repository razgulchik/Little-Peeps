namespace LittlePeeps
{
    // The ground of a zone — one per biome. Set on every cell of a zone from its BiomeDef and read by
    // placement rules (StructureDef.allowedTerrain) and the tile painter. Water is not a terrain: the
    // sea is the absence of a cell, and a river is a structure standing on ordinary ground.
    public enum TerrainType
    {
        Grass,
        Forest,
        Desert,
    }
}
