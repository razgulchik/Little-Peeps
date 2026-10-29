using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // The test mode switch (see TestMode): a checked menu item, stored per machine in EditorPrefs, so it
    // never touches the scene. Read when play starts — flipping it mid-play takes effect from the next Play.
    public static class TestModeMenu
    {
        private const string Path = "Little Peeps/Test Mode";

        [MenuItem(Path)]
        private static void Toggle()
        {
            bool on = !TestMode.IsOn;
            EditorPrefs.SetBool(TestMode.PrefKey, on);

            string when = EditorApplication.isPlaying ? " — from the next Play" : "";
            Debug.Log($"[TEST MODE] {(on ? "on" : "off")}{when}.");
        }

        [MenuItem(Path, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(Path, TestMode.IsOn);
            return true;
        }
    }
}
