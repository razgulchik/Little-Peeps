using UnityEngine;

namespace LittlePeeps
{
    // The one abbreviation rule for resource amounts. Wallets and prices are read side by side — a
    // build card's cost against the bar above it, an age price against the same bar — so they have to
    // shorten numbers identically, or the comparison the player is making quietly lies to them.
    public static class ResourceFormat
    {
        private static readonly string[] Suffixes = { "", "k", "M", "B", "T" };

        // Up to 4 digits + a suffix letter (1_256_000 → "1256k", 10_000 → "10k", 999 → "999"). Steps to
        // the next suffix only when the floored value would need a 5th digit, so the number stays ≤ 9999.
        // Floors (never rounds up) so the label only shows fully-earned units — 1.5 reads as "1". The
        // stored amount stays a precise float; only this display is truncated.
        public static string Abbreviate(float value)
        {
            int tier = 0;
            float v = value;
            while (Mathf.Abs(v) >= 10000f && tier < Suffixes.Length - 1)
            {
                v /= 1000f;
                tier++;
            }

            return Mathf.FloorToInt(v) + Suffixes[tier];
        }
    }
}
