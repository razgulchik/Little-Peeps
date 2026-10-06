using System.Text;
using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for StructureDef: the default drawing, plus a price preview under the Count & price growth
    // block — the first prices with 0, 1, 2 ... already standing, in both growth modes one above the other,
    // the active one in bold. Made for balancing: flip Exponential Cost Growth and the curve it picks is
    // right there. Computed by StructurePrice.At, the same formula placement charges by.
    [CustomEditor(typeof(StructureDef))]
    [CanEditMultipleObjects]
    public class StructureDefEditor : Editor
    {
        private const int Steps = 8;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (targets.Length == 1) DrawPricePreview((StructureDef)target);
        }

        private static void DrawPricePreview(StructureDef def)
        {
            if (def.cost == null || def.cost.Count == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Price preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(" ", "with 0, 1, 2 ... already standing"
                                            + (def.HasLimit ? $" - Max Count {def.maxCount}" : ""),
                                       EditorStyles.miniLabel);

            // No percent step = both modes give the same row, so show it once.
            bool oneCurve = Mathf.Approximately(def.costStepPercent, 0f);
            bool zeroBase = false;

            for (int i = 0; i < def.cost.Count; i++)
            {
                var entry = def.cost[i];
                if (entry == null) continue;
                if (Mathf.Approximately(entry.amount, 0f)) zeroBase = true;

                string resource = entry.resourceType.ToString();
                if (oneCurve)
                {
                    Row(resource, Prices(def, entry.amount, def.exponentialCostGrowth), true);
                    continue;
                }
                Row(resource + "  linear", Prices(def, entry.amount, false), !def.exponentialCostGrowth);
                Row(resource + "  exponential", Prices(def, entry.amount, true), def.exponentialCostGrowth);
            }

            if (!oneCurve && zeroBase && Mathf.Approximately(def.costStepFlat, 0f))
                EditorGUILayout.HelpBox("A percent step on a base cost of 0 stays 0. Set a base cost, or use " +
                                        "Cost Step Flat.", MessageType.Warning);
        }

        private static string Prices(StructureDef def, float baseAmount, bool exponential)
        {
            var sb = new StringBuilder();
            for (int n = 0; n < Steps; n++)
            {
                if (n > 0) sb.Append("  ");
                float price = StructurePrice.At(baseAmount, n, def.costStepFlat, def.costStepPercent, exponential);
                sb.Append(price.ToString("0"));
            }
            return sb.ToString();
        }

        private static void Row(string label, string prices, bool active) =>
            EditorGUILayout.LabelField(label, prices, active ? EditorStyles.boldLabel : EditorStyles.label);
    }
}
