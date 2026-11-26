using System.IO;
using UnityEditor;
using UnityEngine;

namespace LittlePeeps.EditorTools
{
    // The save files from the editor: open their folder, or delete the real or the test profile / saved run to
    // play from a fresh one. The editor shares the folder with a build on the same machine (persistentDataPath
    // is per company and product, not per editor), so these also clear what a local build saved.
    //
    // Not while playing: the running game holds both in memory and writes them back — the profile on its next
    // change, the run when Play stops. Reset Progress and New Run are the ways to do it from inside a game.
    public static class SaveMenu
    {
        private const string Root = "Little Peeps/Saves/";

        [MenuItem(Root + "Open Save Folder")]
        private static void OpenFolder()
        {
            string folder = Application.persistentDataPath;
            Directory.CreateDirectory(folder);

            // Revealing a file opens its folder with the file selected; a folder alone would open its parent.
            string profile = Path.Combine(folder, SaveSystem.ProfileFileName);
            EditorUtility.RevealInFinder(File.Exists(profile) ? profile : folder);
        }

        private const string ProfileLoss = "Prestige points, records and wisdoms go with it.";
        private const string RunLoss = "The run left in the middle goes with it — the next Play offers a new one.";

        [MenuItem(Root + "Delete Profile")]
        private static void DeleteProfile() => Delete(SaveSystem.ProfileFileName, "the profile", ProfileLoss);

        [MenuItem(Root + "Delete Saved Run")]
        private static void DeleteRun() => Delete(SaveSystem.RunFileName, "the saved run", RunLoss);

        [MenuItem(Root + "Delete Test Profile")]
        private static void DeleteTestProfile() => Delete(TestMode.ProfileFileName, "the test mode profile", ProfileLoss);

        [MenuItem(Root + "Delete Test Saved Run")]
        private static void DeleteTestRun() => Delete(TestMode.RunFileName, "the test mode saved run", RunLoss);

        [MenuItem(Root + "Delete Profile", true)]
        [MenuItem(Root + "Delete Saved Run", true)]
        [MenuItem(Root + "Delete Test Profile", true)]
        [MenuItem(Root + "Delete Test Saved Run", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private static void Delete(string fileName, string what, string loss)
        {
            var file = new SafeFile(Application.persistentDataPath, fileName);
            if (!file.Exists)
            {
                Debug.Log($"[Save] no {what} to delete at {file.MainPath}.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Delete " + what + "?", $"{file.MainPath}\n\n{loss} This cannot be undone.",
                                             "Delete", "Cancel"))
                return;

            file.Delete();
            Debug.Log($"[Save] {what} deleted: {file.MainPath}");
        }
    }
}
