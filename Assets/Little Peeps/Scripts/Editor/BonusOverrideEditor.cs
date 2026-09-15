using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // Inspector base for an asset whose card text is GENERATED from its modifiers unless an author
    // words it by hand — AgeDef.bonusOverride, StatPerkDef.description. Everything is the default
    // drawing except the override field, which gets the affordance the "generate it, then reword it"
    // workflow needs: the text the card would show is previewed live under the field, and one button
    // copies it in as a starting point.
    //
    // The override deliberately stays EMPTY by default, and empty means "follow the modifiers". So a
    // balance pass never has to remember to re-type a label, and only a card someone worded by hand
    // on purpose can go stale. Same principle as StatModifierDrawer next door: no second copy of the
    // truth unless an author explicitly asked for one.
    //
    // What differs per asset is kept to four questions below: which field, what the data would say,
    // whether there is any data at all, and an optional note. The generated text is asked of the
    // ASSET (AgeDef.GeneratedBonusText, PerkDef.GeneratedDescription), never rebuilt here, so the
    // preview is by construction what the card shows.
    public abstract class BonusOverrideEditor : Editor
    {
        // Serialized name of the override string field.
        protected abstract string OverrideField { get; }

        // Noun for the messages: "age", "perk".
        protected abstract string Noun { get; }

        // What the card would show with an empty override. Empty when the data has nothing to say.
        protected abstract string Generated(Object target);

        // Whether the asset carries any modifier — tells "no modifier" from "a modifier that changes
        // nothing", which want different words.
        protected abstract bool HasModifiers(Object target);

        // An optional line under the buttons. Null for none.
        protected virtual string Note(Object target) => null;

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
                if (property.propertyPath == OverrideField) DrawPreview(property.stringValue);
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

        private void DrawPreview(string currentOverride)
        {
            // Multi-select has no single generated text to show. The override field itself still edits
            // across the selection normally — only this preview steps aside.
            if (targets.Length != 1) return;

            bool overridden = !string.IsNullOrWhiteSpace(currentOverride);

            if (!HasModifiers(target))
            {
                EditorGUILayout.HelpBox(
                    overridden
                        ? $"No modifier on this {Noun} — the card shows the override text only."
                        : $"No modifier and no override: this {Noun} says nothing on its card.",
                    MessageType.None);
                return;
            }

            string generated = Generated(target);

            using (new EditorGUI.IndentLevelScope())
            {
                if (string.IsNullOrEmpty(generated))
                {
                    EditorGUILayout.HelpBox("The modifiers change nothing — flat and percent are zero " +
                                            "on every one the card speaks for, so there is no bonus to " +
                                            "describe.", MessageType.Warning);
                }
                else
                {
                    // One LabelField per line: the generated text can be several lines (a perk lists
                    // every modifier), and a single label would clip everything past the first.
                    string label = overridden ? "Generated (unused)" : "On the card";
                    foreach (string line in generated.Split('\n'))
                    {
                        EditorGUILayout.LabelField(label, line, EditorStyles.miniLabel);
                        label = " ";
                    }
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

                string note = Note(target);
                if (note != null) EditorGUILayout.HelpBox(note, MessageType.Info);
            }
        }
    }
}
