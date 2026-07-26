namespace LittlePeeps
{
    // A placed structure — anything that stands on the grid. Collision handling + effect dispatch
    // live in the CollisionTarget base; Structure adds the definition it was built from.
    // Spawner structures add a Spawner component; resource structures (Tree/Wheat/Forge/Church) add
    // a ResourceSource component.
    public class Structure : CollisionTarget
    {
        public StructureDef def;
    }
}
