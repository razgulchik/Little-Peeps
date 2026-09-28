using UnityEditor;

namespace LittlePeeps.EditorTools
{
    // Inspector for ResourceSourceDef: the default drawing, minus the depletion fields the chosen mode
    // never reads — Never hides all three, Despawn keeps only the hit count, Regrow shows everything.
    // A hidden value is kept, not cleared, so switching the mode back finds it as it was.
    [CustomEditor(typeof(ResourceSourceDef))]
    [CanEditMultipleObjects]
    public class ResourceSourceDefEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // A multi-selection that disagrees on the mode shows every field rather than guess.
            SerializedProperty depletion = serializedObject.FindProperty("depletion");
            bool mixed = depletion.hasMultipleDifferentValues;
            var mode = (Depletion)depletion.enumValueIndex;

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

                if (!mixed && !IsRead(property.propertyPath, mode)) continue;
                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // Whether a source in `mode` ever reads this field. Everything outside the depletion block is.
        private static bool IsRead(string field, Depletion mode)
        {
            if (field == "hitsToDeplete") return mode != Depletion.Never;
            if (field == "regrowTime" || field == "keepBodyWhileDepleted") return mode == Depletion.Regrow;
            return true;
        }
    }
}
