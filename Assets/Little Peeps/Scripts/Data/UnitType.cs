namespace LittlePeeps
{
    // Values are serialized by number into assets (UnitDef.unitType, WorkerYield.worker, stat
    // scopes) — never renumber or reuse existing values, only append new ones. (Renumbered ONCE, on
    // 2026-09-17, to put Unassigned at 0 before racks, the villager def and saves existed; every
    // asset value was migrated in the same change. Not again.)
    // Animals (alpaca/boar/fox) are NOT unit types: they are mobile resource sources identified
    // by their ResourceSourceDef asset, like trees and wheat (see Animal).
    //
    // One enum, two roles — keep them apart when reading code:
    //   - POPULATION key: `UnitDef.unitType` is what a worker is BORN as and what SpawnSystem counts
    //     houses and live units by. Immutable for the life of the unit.
    //   - PROFESSION: `Unit.Profession` (read through `Unit.Type`) is what the worker currently does —
    //     what it harvests, how fast it walks. It starts equal to the def's value and CHANGES when the
    //     worker picks a tool off a rack (Unit.Equip); Unit.Unequip puts it back at the end of an
    //     outing. A worker def with Unassigned is the doc's villager: born with no profession, gets one
    //     from a rack. The old per-profession defs (Farmer) still work — born a farmer, never
    //     Unassigned, so no rack ever hands them anything.
    public enum UnitType
    {
        // No profession, and the DEFAULT on purpose: a unit with no def, an untouched unitScope in a
        // modifier, a zeroed field — all read as "nobody in particular" rather than as a farmer.
        // Harvests nothing (no ResourceSourceDef lists it as a worker); the ONLY state a rack equips.
        Unassigned = 0,
        // Worker professions (harvest sources; a tool rack hands each one out)
        Farmer = 1,
        Lumberjack = 2,
        Hunter = 3,
        Miner = 4,
        Blacksmith = 5,
        // Military
        Swordsman = 6,
    }
}
