using System.Text;
using UnityEngine;

namespace LittlePeeps
{
    // Ages are spoken in roman numerals everywhere the player sees one — a build card's "Open on the
    // Age IV", the timeline column — so the conversion lives in one place and every screen agrees.
    public static class RomanNumeral
    {
        private static readonly (int value, string numeral)[] Numerals =
        {
            (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
            (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
            (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
        };

        // Clamped to 1: there is no roman zero, and an age number below 1 is a bug upstream that must
        // not turn into an empty label the player has to interpret.
        public static string From(int value)
        {
            value = Mathf.Max(1, value);

            var result = new StringBuilder();
            for (int i = 0; i < Numerals.Length; i++)
            {
                while (value >= Numerals[i].value)
                {
                    result.Append(Numerals[i].numeral);
                    value -= Numerals[i].value;
                }
            }

            return result.ToString();
        }
    }
}
