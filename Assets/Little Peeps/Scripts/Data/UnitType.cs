namespace LittlePeeps
{
    // Values are serialized by number into assets (UnitDef.unitType, WorkerYield.worker, stat
    // scopes) — never renumber or reuse existing values, only append new ones. (Renumbered ONCE, on
    // 2026-09-17, to put Unassigned at 0 before racks, the villager def and saves existed; every
    // asset value was migrated in the same change. Not again.)
    // Animals (alpaca/boar/fox) are NOT unit types: they are mobile resource sources identified
    // by their ResourceSourceDef asset, like trees and wheat (see Animal).
    //
    // The id of a PROFESSION — what a worker currently does: what it harvests (WorkerYield.worker),
    // how the stat sheet scopes it. Each value has a ProfessionDef asset (look + future numbers); the
    // unit carries one for the outing (Unit.Profession, id read through Unit.Type), takes a new one
    // from a tool rack (Unit.Equip) and drops back to the one its UnitDef was born with at the end
    // (Unit.Unequip). This enum says nothing about POPULATION: SpawnSystem counts houses and live
    // units per UnitDef, whatever those units go on to do.
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
