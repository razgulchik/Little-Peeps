using UnityEngine;

namespace LittlePeeps
{
    // A meta perk that stands one more structure on the start island of every run: the tent, a house of one cell.
    // Put up by the island generator like the house and the pier, beside the pier (IslandContent.ExtraSlots), so it
    // costs nothing and needs no place in the build palette. Whether the player may sell it is the structure's own
    // Can Sell.
    [CreateAssetMenu(menuName = "LittlePeeps/Meta/Start Structure")]
    public class StartStructureUpgradeDef : GlobalUpgradeDef
    {
        [Tooltip("What stands on the start island. Its Allowed Terrain must take the start biome's ground.")]
        public StructureDef structure;

        // "+1 TENT" — the player-facing name, falling back to the asset's when it is blank.
        public override string GeneratedDescription() =>
            structure != null ? "+1 " + structure.ShownName.ToUpperInvariant() : string.Empty;

        public override void ApplyToRun(RunContext run)
        {
            if (run == null || structure == null) return;

            run.startStructures.Add(structure);
        }
    }
}
