using System.Collections.Generic;
using UnityEngine;

namespace LittlePeeps
{
    // Test mode: presets that stand in for the real ones during a playtest, so testing never means
    // re-pointing the real references (RunManager's start config, OfferingSystem's schedule) or editing
    // the real assets. Place on a dedicated "TestMode" GameObject; GameBootstrap applies it once, before
    // the profile is loaded and the first run is built, and it holds for every run of that play session.
    //
    // It also plays on a profile and a saved run of its own (ProfileFileName, RunFileName), so test runs bank
    // and spend prestige without touching the real profile — and Reset Progress in test mode resets only the
    // test one.
    //
    // The switch is NOT on this component: it lives per machine in EditorPrefs (menu Little Peeps → Test
    // Mode), so turning it on never dirties the shared scene and never reaches a teammate or a build.
    // Editor only: in a build IsOn is always false.
    public class TestMode : MonoBehaviour
    {
        [Tooltip("Replaces RunManager's start config while test mode is on. Empty = the real one.")]
        [SerializeField] private StartConfigDef startConfig;

        [Tooltip("Replaces OfferingSystem's schedule while test mode is on. Empty = the real one.")]
        [SerializeField] private OfferingScheduleDef offeringSchedule;

        // Beside the real profile and run (SaveSystem.ProfileFileName / RunFileName), in the same folder.
        public const string ProfileFileName = "meta_test.json";
        public const string RunFileName = "run_test.json";

#if UNITY_EDITOR
        public const string PrefKey = "LittlePeeps.TestMode";
        public static bool IsOn => UnityEditor.EditorPrefs.GetBool(PrefKey, false);
#else
        public static bool IsOn => false;
#endif

        // Hands the presets to their systems and says loudly what was swapped — a test mode left on by
        // accident must be impossible to miss in the console.
        public void Apply(RunManager runManager, OfferingSystem offeringSystem, SaveSystem saveSystem)
        {
            var swapped = new List<string>();

            if (saveSystem != null)
            {
                saveSystem.UseFiles(ProfileFileName, RunFileName);
                swapped.Add($"profile → {ProfileFileName}, run → {RunFileName}");
            }

            if (startConfig != null && runManager != null)
            {
                runManager.UseStartConfig(startConfig);
                swapped.Add($"start config → '{startConfig.name}'");
            }
            if (offeringSchedule != null && offeringSystem != null)
            {
                offeringSystem.UseSchedule(offeringSchedule);
                swapped.Add($"offering schedule → '{offeringSchedule.name}'");
            }

            string what = swapped.Count > 0 ? string.Join(", ", swapped) : "nothing to swap (all fields empty)";
            Debug.LogWarning($"[TEST MODE] on: {what}. Turn it off in the menu Little Peeps → Test Mode.", this);
        }
    }
}
