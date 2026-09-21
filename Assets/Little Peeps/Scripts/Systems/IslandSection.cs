using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // One piece of the island as the generator grew it: the start (index 0) or an age's zone. Cells are
    // absolute grid coordinates and never change after the section is committed — the island only ever
    // gains sections, so a section's cells are also its permanent identity on the grid.
    public sealed class IslandSection
    {
        public int Index { get; }
        public IReadOnlyCollection<Vector2Int> Cells => cells;

        // Biome and natural features, or null for a zone committed as bare land (the content pass failed
        // and the shape was kept — see IslandSystem.Expand).
        public IslandSectionContent Content { get; }

        private readonly HashSet<Vector2Int> cells;

        internal IslandSection(int index, HashSet<Vector2Int> cells, IslandSectionContent content)
        {
            Index = index;
            this.cells = cells;
            Content = content;
        }

        public bool Contains(Vector2Int cell) => cells.Contains(cell);
    }

    // A proposed zone: the new land it would add, valid against the island as it was when proposed.
    // Commit it to make it a section, or drop it — a candidate that outlives another commit is stale
    // and the generator refuses it.
    public sealed class IslandCandidate
    {
        public IReadOnlyCollection<Vector2Int> Cells => cells;

        // Cells the shore repair added on top of the proposed mass (filled pockets and holes).
        public int RepairFill { get; }

        // Compactness of the whole island with this zone attached; higher is tidier. Used to keep the
        // best placement per shape family — never to weight the choice between families.
        public double Score { get; }

        internal readonly HashSet<Vector2Int> cells;
        internal readonly int islandVersion;   // section count it was proposed against

        internal IslandCandidate(HashSet<Vector2Int> cells, int repairFill, double score, int islandVersion)
        {
            this.cells = cells;
            RepairFill = repairFill;
            Score = score;
            this.islandVersion = islandVersion;
        }

        public bool Contains(Vector2Int cell) => cells.Contains(cell);
    }
}
