#if UNITY_EDITOR
using UnityEditor;

namespace Water2D
{
    [InitializeOnLoad]
    internal static class LegacyGlobalSettingsCleanup
    {
        private const string LegacyGlobalSettingsGuid = "c34f7978791ad99429f315a5d351bbf2";

        static LegacyGlobalSettingsCleanup()
        {
            EditorApplication.delayCall += RemoveLegacyGlobalSettings;
        }

        private static void RemoveLegacyGlobalSettings()
        {
            string path = AssetDatabase.GUIDToAssetPath(LegacyGlobalSettingsGuid);
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.DeleteAsset(path);
        }
    }
}
#endif
