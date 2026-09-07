using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector for AgeDef. Everything is the default drawing except the bonus line, which gets the
    // affordance the "generate it, then reword it" workflow needs: the line StatModifierText produces
    // is shown live under the override field, and one button copies it in as a starting point.
    //
    // The override deliberately stays EMPTY by default, and empty means "follow the modifier". So a
    // balance pass never has to remember to re-type a label, and only a card someone worded by hand on
    // purpose can go stale. Same principle as StatModifierDrawer next door: no second copy of the
    // truth unless an author explicitly asked for one.
    [CustomEditor(typeof(AgeDef))]
    public class AgeDefEditor : Editor
    {
        private const string OverrideField = "bonusOverride";

        // A button press is recorded here and applied AFTER the property walk below: writing to the
        // serialized object while its iterator is live would invalidate the iterator mid-draw.
        private bool hasPendingWrite;
        private string pendingOverride;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property);
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
                if (property.propertyPath == OverrideField) DrawBonusPreview();
            }

            if (hasPendingWrite)
            {
                serializedObject.FindProperty(OverrideField).stringValue = pendingOverride;
                hasPendingWrite = false;

                // Drop focus, or the text field keeps drawing the string the user was looking at
                // before the button rewrote the property underneath it.
                GUI.FocusControl(null);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBonusPreview()
        {
            // Multi-select has no single generated line to show. The override field itself still edits
            // across the selection normally — only this preview steps aside.
            if (targets.Length != 1) return;

            var def = (AgeDef)target;
            bool overridden = !string.IsNullOrWhiteSpace(def.bonusOverride);

            if (def.modifiers == null || def.modifiers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    overridden
                        ? "No modifier on this age — the card shows the override text only."
                        : "No modifier and no override: this age advertises no bonus.",
                    MessageType.None);
                return;
            }

            string generated = StatModifierText.Describe(def.modifiers[0]);

            using (new EditorGUI.IndentLevelScope())
            {
                if (string.IsNullOrEmpty(generated))
                {
                    EditorGUILayout.HelpBox("The first modifier changes nothing — both flat and percent " +
                                            "are zero, so there is no bonus to describe.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField(overridden ? "Generated (unused)" : "On the card",
                                               generated, EditorStyles.miniLabel);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(GUIContent.none, GUILayout.Width(EditorGUIUtility.labelWidth));

                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(generated)))
                    {
                        if (GUILayout.Button("Override with this"))
                        {
                            pendingOverride = generated;
                            hasPendingWrite = true;
                        }
                    }

                    using (new EditorGUI.DisabledScope(!overridden))
                    {
                        if (GUILayout.Button("Back to auto"))
                        {
                            pendingOverride = string.Empty;
                            hasPendingWrite = true;
                        }
                    }
                }

                if (def.modifiers.Count > 1)
                {
                    EditorGUILayout.HelpBox(
                        $"{def.modifiers.Count} modifiers — all of them apply, but the card only speaks " +
                        "for the first. Word the rest into the override if they should be visible.",
                        MessageType.Info);
                }
            }
        }
    }
}
