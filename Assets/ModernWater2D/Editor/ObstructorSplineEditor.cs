using UnityEditor;
using UnityEngine;

namespace Water2D
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ObstructorSpline))]
    public class ObstructorSplineEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            base.OnInspectorGUI();

            ObstructorSpline ob = (ObstructorSpline)target;

            float oldV = ob.height;
            ob.height = EditorGUILayout.Slider("height", ob.height, 0, 1);
            if (oldV != ob.height)
                foreach (ObstructorSpline obs in targets) obs.height = ob.height;

            if (GUILayout.Button("Create"))
                foreach (ObstructorSpline obs in targets) { obs.CreateData(); EditorUtility.SetDirty(obs); }

            if (GUILayout.Button("Destroy"))
                foreach (ObstructorSpline obs in targets) { obs.Destroy(); EditorUtility.SetDirty(obs); }

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(ob);
        }
    }
}
