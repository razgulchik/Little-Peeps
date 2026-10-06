using UnityEngine;

namespace LittlePeeps
{
    // How a building's price grows with how many of it already stand — the one formula behind
    // StructureDef.CostAt, kept apart from the def so it is plain arithmetic the offline harness can pin.
    //
    // Shaped like the stat formula, with every standing copy acting as one more modifier:
    //
    //     linear       price(n) = (base + n·flat) × (1 + n·percent)
    //     exponential  price(n) = (base + n·flat) × (1 + percent)ⁿ
    //
    // The switch changes only how the percents combine: linear adds percent of the BASE each time (a
    // constant step), exponential adds percent of the PREVIOUS price (×1.5 per building at 0.5). Flat
    // stays linear in both modes — a flat amount compounded on itself is just a steeper percent.
    public static class StructurePrice
    {
        // `standing` = how many of this structure already stand, so 0 is the first one's price.
        //
        // Rounded to a whole number, half up: a percent step makes fractions (22.5), the card floors
        // what it prints, and a player holding "22" must not be refused a price that reads "22". The
        // epsilon is float noise only — 25 × 1.06 comes out of single precision as 26.499998, which is
        // 27, not 26. Never below zero: a negative step is a discount, never a payout.
        public static float At(float baseAmount, int standing, float stepFlat, float stepPercent, bool exponential)
        {
            int n = Mathf.Max(0, standing);
            float raw = baseAmount + n * stepFlat;
            float factor = exponential
                ? Mathf.Pow(Mathf.Max(0f, 1f + stepPercent), n)
                : 1f + n * stepPercent;
            return Mathf.Max(0f, Mathf.Floor(raw * factor + 0.5f + 1e-4f));
        }
    }
}
