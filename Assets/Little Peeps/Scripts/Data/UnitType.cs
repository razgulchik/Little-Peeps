// Values are serialized by number into assets (UnitDef.unitType, WorkerYield.worker, stat
// scopes) — never renumber or reuse existing values, only append new ones.
// Animals (alpaca/boar/fox) are NOT unit types: they are mobile resource sources identified
// by their ResourceSourceDef asset, like trees and wheat (see Animal).
public enum UnitType
{
    // Worker units (produced by spawner buildings; harvest sources)
    Farmer = 0,
    Lumberjack = 1,
    Hunter = 2,
    Miner = 3,
    // Military units
    Swordsman = 4,
}
