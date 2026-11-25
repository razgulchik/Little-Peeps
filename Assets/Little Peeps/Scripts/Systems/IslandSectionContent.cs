using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // What a zone holds besides its land: the biome, the natural features on it and the cells kept free
    // for access. Built up by IslandContent.Populate and not touched again once its section is
    // committed — a zone's content is as permanent as its cells. Plain data, prototype-shaped
    // (SectionContent in island_resources.py), so the port reads against the original.
    //
    // Reservations (clearing, gates, mine and mill approaches, bridge banks) are the generator's own
    // bookkeeping: they keep natural features off cells a future zone or building will need. The game
    // never sees them — the player may build there freely.
    public sealed class IslandSectionContent
    {
        public readonly IslandBiome biome;
        public readonly HashSet<Vector2Int> land;

        // The starting zone carries the house: its footprint is all the generator needs, the def is
        // attached where structures are placed (Features). Empty for every later zone.
        public readonly Footprint houseFootprint;
        public bool Starting => houseFootprint.CellCount > 0;

        // The zone that carries the pier (the start, in play): it stands on the east coast for the whole
        // run, and the sea in front of it, row by row to the horizon, stays sea (IslandContent.PierLane).
        // Empty for every other zone.
        public readonly Footprint pierFootprint;

        // Start structures beyond those two (a meta perk's tent), each beside the pier. Like the house, only
        // their footprints matter here; the defs come with Features, in the same order. Empty for every later
        // zone.
        public readonly IReadOnlyList<Footprint> extraFootprints;

        public readonly Dictionary<Vector2Int, IslandObjectRule> objects = new();
        public readonly HashSet<Vector2Int> mountains = new();
        public readonly List<Vector2Int> river = new();          // ordered path; empty = no river
        public readonly HashSet<Vector2Int> house = new();       // footprint cells
        public Vector2Int houseOrigin;
        public readonly HashSet<Vector2Int> pier = new();        // footprint cells
        public Vector2Int pierOrigin;
        public readonly HashSet<Vector2Int> extras = new();      // footprint cells of all of them
        public readonly List<Vector2Int> extraOrigins = new();   // one per extra, in extraFootprints' order
        public readonly HashSet<Vector2Int> clearing = new();
        public readonly HashSet<Vector2Int> reserved = new();
        public readonly Dictionary<Vector2Int, (Vector2Int a, Vector2Int b)> bridges = new();  // river cell → its two clear banks
        public readonly HashSet<Vector2Int> mineAccess = new();
        public readonly HashSet<Vector2Int> millAccess = new();

        public IslandSectionContent(IslandBiome biome, IEnumerable<Vector2Int> land, Footprint houseFootprint = default, Footprint pierFootprint = default,
                                    IReadOnlyList<Footprint> extraFootprints = null)
        {
            this.biome = biome;
            this.land = new HashSet<Vector2Int>(land);
            this.houseFootprint = houseFootprint;
            this.pierFootprint = pierFootprint;
            this.extraFootprints = extraFootprints ?? System.Array.Empty<Footprint>();
        }

        // A cell a start structure stands on (the house, the pier, an extra): reserved like a clearing, but not
        // free.
        public bool Built(Vector2Int p) => house.Contains(p) || pier.Contains(p) || extras.Contains(p);

        // Natural features block movement (objects, mountains, river); so do the start structures.
        public HashSet<Vector2Int> Natural()
        {
            var result = new HashSet<Vector2Int>(objects.Keys);
            result.UnionWith(mountains);
            result.UnionWith(river);
            return result;
        }

        public HashSet<Vector2Int> Blocked()
        {
            var result = Natural();
            result.UnionWith(house);
            result.UnionWith(pier);
            result.UnionWith(extras);
            return result;
        }

        // The approved budget counts natural obstructions, not the start structures.
        public int OpenCells => land.Count - Natural().Count;

        // Everything to instantiate, in placement order: the start structures first (their slots are
        // reserved for them), then the terrain-like features, then the scattered objects. A null def (an
        // unassigned rule, or no def for a start structure the zone has) is handed back as such; the
        // caller reports it.
        public List<(Vector2Int cell, StructureDef def)> Features(StructureDef house, StructureDef pier = null, IReadOnlyList<StructureDef> extraDefs = null)
        {
            var result = new List<(Vector2Int, StructureDef)>();
            if (this.house.Count > 0) result.Add((houseOrigin, house));
            if (this.pier.Count > 0) result.Add((pierOrigin, pier));
            for (int i = 0; i < extraOrigins.Count; i++)
                result.Add((extraOrigins[i], extraDefs != null && i < extraDefs.Count ? extraDefs[i] : null));
            foreach (var cell in IslandShape.Sorted(mountains)) result.Add((cell, biome.mountain));
            foreach (var cell in river) result.Add((cell, biome.river));
            foreach (var cell in IslandShape.Sorted(objects.Keys)) result.Add((cell, objects[cell].def));
            return result;
        }
    }
}
