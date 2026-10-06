using UnityEngine;

namespace LittlePeeps
{
    // A perk that opens a building in the build palette for the rest of the run — the first perk whose
    // effect is not a number, so it is its own PerkDef subclass rather than a modifier in disguise (see
    // StatPerkDef). Opening is a GATE, not a stat: it lands in RunContext.unlockedStructures, and
    // StructureDef.LockState is where the palette asks. Only a structure marked lockedUntilPerk is shut
    // in the first place; on any other this perk changes nothing (its inspector says so).
    [CreateAssetMenu(menuName = "LittlePeeps/Perks/Unlock Structure Perk")]
    public class UnlockStructurePerkDef : PerkDef
    {
        [Tooltip("The building this perk opens. Tick 'Locked Until Perk' on it, or it is open from the " +
                 "start and this perk does nothing.")]
        public StructureDef structure;

        // "UNLOCKS RICE PADDY" — the player-facing name, falling back to the asset's when it is blank.
        public override string GeneratedDescription =>
            structure != null ? "UNLOCKS " + structure.ShownName.ToUpperInvariant() : string.Empty;

        public override void ApplyPerk(RunContext context)
        {
            if (context != null && structure != null) context.unlockedStructures.Add(structure);
        }
    }
}
