using System;
using UnityEngine;

namespace LittlePeeps
{
    // Tunables of the island shape generator. Lives on StartConfigDef so a run's island is data like
    // everything else about its start; the defaults are the prototype's (Prototypes/IslandShapeLab,
    // README "First design rule"), which is where the reasoning behind each number is written up.
    //
    // Every rule is a hard constraint, not a preference: a candidate section either satisfies all of
    // them or is thrown away. The generator never loosens them on its own — when no valid section can
    // be found it reports failure, so a set of rules that fails in play is a tuning bug to fix here,
    // and the seed sweep test (IslandShapeTests) is what catches it before play does.
    [Serializable]
    public class IslandRules
    {
        [Tooltip("Land cells of the starting island. Hard ±10% band, rounded inward: 36 → 33..39.")]
        public int startArea = 36;

        [Tooltip("Land cells each age adds. Hard ±10% band after all shore repairs: 48 → 44..52.")]
        public int sectionArea = 48;

        [Tooltip("Shortest horizontal or vertical run of land allowed anywhere in the island. Keeps " +
                 "out necks and spurs; also the minimum straight join between a new section and " +
                 "the old coast.")]
        public int minWidth = 3;

        [Tooltip("Water pockets narrower than this many cells are filled in with land.")]
        public int minBay = 4;

        [Tooltip("A bay deeper than this × its width is filled in; broad shallow bays survive.")]
        public float depthRatio = 0.75f;

        [Tooltip("Proposals tried before a start or expansion gives up. Only bounds the failure case " +
                 "— the search normally stops much earlier, once enough distinct shapes are found.")]
        public int attempts = 2000;

        // Hard ±10% bounds, rounded inward to whole cells.
        public void AreaLimits(int target, out int lower, out int upper)
        {
            int allowance = target / 10;
            lower = target - allowance;
            upper = target + allowance;
        }

        public void Validate()
        {
            if (minWidth < 2 || minBay < 2 || depthRatio <= 0f)
                throw new ArgumentException("IslandRules: widths must be at least 2 and the bay depth ratio must be positive.");
            if (Math.Min(startArea, sectionArea) < (minWidth + 2) * (minWidth + 2))
                throw new ArgumentException("IslandRules: area targets must be at least (minimum land width + 2) squared.");
            if (attempts < 1)
                throw new ArgumentException("IslandRules: attempts must be positive.");
        }
    }
}
