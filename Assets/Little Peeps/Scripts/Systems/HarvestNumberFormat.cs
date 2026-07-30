using TMPro;
using UnityEngine;

namespace LittlePeeps
{
    // How a harvested amount is spelled on a floating number: "+1", "+2.5", "+1.2k". Its own type
    // because the game and the Edit Mode authoring tool both need it and neither owns it — the tool
    // previewing "+1" while the game showed "+1.0" would defeat the point of previewing at all.
    public static class HarvestNumberFormat
    {
        // Writes the amount straight into the label's char buffer. TMP's SetText overloads take the
        // value as an argument and format in place, so no string is built and nothing lands on the GC —
        // which matters when this runs on every single harvest in the village.
        //
        // "{0:1}" is TMP's own spec for one decimal place, not string.Format's.
        public static void Write(TMP_Text label, float amount)
        {
            if (label == null) return;

            if (amount < 1000f)
            {
                // A whole number reads better bare: "+1", not "+1.0". Yields stay whole until a
                // percentage modifier lands on them, so most of the game this is the branch taken.
                if (Mathf.Approximately(amount, Mathf.Round(amount))) label.SetText("+{0:0}", amount);
                else label.SetText("+{0:1}", amount);
                return;
            }

            float v = amount;
            int tier = 0;
            while (v >= 1000f && tier < 4)
            {
                v /= 1000f;
                tier++;
            }

            switch (tier)
            {
                case 1:  label.SetText("+{0:1}k", v); break;
                case 2:  label.SetText("+{0:1}M", v); break;
                case 3:  label.SetText("+{0:1}B", v); break;
                default: label.SetText("+{0:1}T", v); break;
            }
        }
    }
}
