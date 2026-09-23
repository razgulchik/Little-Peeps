using UnityEngine;
using UnityEditor;

namespace Water2D
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ReflectorSpline))]
    public class ReflectorSplineEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var reflector = target as ReflectorSpline;
            Inspector(reflector);
            base.OnInspectorGUI();
        }

        void Inspector(ReflectorSpline reflector)
        {
            EditorGUI.BeginChangeCheck();

            GUILayout.Space(10);
            GUILayout.Label("Spline Reflection", GUIStyleUtils.Label(14, "87F6FF"));

            reflector.flipX.value = EditorGUILayout.Toggle("flip reflection x-axis", reflector.flipX.value);
            foreach (ReflectorSpline r in targets) r.flipX = reflector.flipX;

            reflector.displacement.value = EditorGUILayout.Vector2Field("displacement", reflector.displacement.value);
            foreach (ReflectorSpline r in targets) r.displacement = reflector.displacement;

            GUILayout.Space(10);
            GUILayout.Label("Control", GUIStyleUtils.Label(14, "FFBFA0"));

            if (GUILayout.Button("Update Options"))
                foreach (ReflectorSpline r in targets) { r.UpdateData(); EditorUtility.SetDirty(r); }

            if (GUILayout.Button("CreateReflection"))
                foreach (ReflectorSpline r in targets) { r.CreateData(); EditorUtility.SetDirty(r); }

            if (GUILayout.Button("DestroyReflection"))
                foreach (ReflectorSpline r in targets) { r.DeleteData(); EditorUtility.SetDirty(r); }

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(reflector);
        }
    }
}