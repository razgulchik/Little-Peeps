using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LittlePeeps
{
    // The two saves on disk, in Application.persistentDataPath (on Windows AppData/LocalLow/<company>/<product>/):
    //
    //   - the PROFILE (MetaContext, ProfileFile) — saved after every change to it (PrestigeSystem: a cash-in,
    //     each take and return on the meta screen, before the next run), so quitting at any moment loses
    //     nothing of it;
    //   - the RUN (RunSnapshot, RunFile) — a BOOKMARK: written only when the game is left, deleted the moment
    //     the run is continued (see RunFile for why). What it writes is the snapshot HELD here: taken by
    //     GameBootstrap whenever normal play is left (so quitting in a window writes the run as it was just
    //     before that window), and on the way out from normal play itself.
    //
    // The format is ProfileFile's and RunFile's, the safe writing SafeFile's; this picks the files and says in
    // the console what happened. The settings are not here: they belong to the machine (GameSettings).
    public class SaveSystem : MonoBehaviour
    {
        public const string ProfileFileName = "meta.json";
        public const string RunFileName = "run.json";

        // Which files this play session uses. Test mode swaps in its own (TestMode.Apply), before Load.
        private string profileFile = ProfileFileName;
        private string runFile = RunFileName;

        // The run as it stood when normal play was last left, waiting to be written when the game is.
        private RunSnapshot heldRun;

        public string ProfilePath => Store.MainPath;

        // Built per use: persistentDataPath may not be read before the engine is up (no field initializer).
        private ProfileStore Store => new(Application.persistentDataPath, profileFile);
        private SafeFile RunStore => new(Application.persistentDataPath, runFile);

        public void UseFiles(string profileFileName, string runFileName)
        {
            if (!string.IsNullOrEmpty(profileFileName)) profileFile = profileFileName;
            if (!string.IsNullOrEmpty(runFileName)) runFile = runFileName;
        }

        // --- Profile -------------------------------------------------------------------------------

        public void Save(MetaContext meta)
        {
            string error = Store.Save(meta);
            if (error != null)
                Debug.LogError($"[Save] {error} — the progress since the last save lives only until the game " +
                               "closes.", this);
        }

        // The saved profile, or a fresh one. Never null.
        public MetaContext Load()
        {
            var store = Store;
            ProfileLoad result = store.Load();

            foreach (string problem in result.Problems) Debug.LogError($"[Save] {problem}", this);

            MetaContext meta = result.Meta;
            switch (result.Source)
            {
                case SaveSource.Main:
                    Debug.Log($"[Save] profile loaded from {store.MainPath}: {meta.prestigePoints} points, " +
                              $"{meta.globalUpgrades.Count} wisdoms taken.", this);
                    break;

                case SaveSource.Backup:
                    Debug.LogWarning($"[Save] profile restored from the backup {store.BackupPath} — the last change " +
                                     "before it may be missing.", this);
                    break;

                default:
                    if (result.Problems.Count > 0)
                        Debug.LogError($"[Save] no readable profile in {store.MainPath} — starting with a fresh " +
                                       "one. The unreadable files are kept beside it.", this);
                    else
                        Debug.Log($"[Save] no profile yet at {store.MainPath} — a fresh one.", this);
                    break;
            }

            return meta;
        }

        // Settings → Reset Progress. The profile and its backup go; files set aside as unreadable stay.
        public void DeleteSave()
        {
            var store = Store;
            try
            {
                store.Delete();
                Debug.Log($"[Save] profile deleted: {store.MainPath}", this);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Save] could not delete {store.MainPath}: {e.Message} — the old profile comes " +
                               "back on the next launch.", this);
            }
        }

        // --- Run -----------------------------------------------------------------------------------

        // The run left last time, or null. Read at boot; the file stays until the run is continued
        // (ConsumeRun) or replaced (DiscardRun), so quitting from the menu without playing keeps it.
        public RunSnapshot LoadRun()
        {
            var store = RunStore;
            var problems = new List<string>();
            bool read = store.TryLoad<RunSnapshot>(RunFile.TryRead, out RunSnapshot run, out SaveSource source, problems);

            foreach (string problem in problems) Debug.LogError($"[Save] {problem}", this);

            if (!read)
            {
                if (problems.Count > 0)
                    Debug.LogError($"[Save] no readable run in {store.MainPath} — the menu offers a new one. The " +
                                   "unreadable files are kept beside it.", this);
                return null;
            }

            if (source == SaveSource.Backup)
                Debug.LogWarning($"[Save] run restored from the backup {store.BackupPath}.", this);
            else
                Debug.Log($"[Save] run found at {store.MainPath}: age {run.currentAge}, cycle {run.offeringCycle}, " +
                          $"{run.playSeconds / 60.0:0.0} min played.", this);
            return run;
        }

        // The run as it stands, kept to be written when the game is left. A snapshot is a copy of numbers, so
        // taking one costs nothing a frame would notice; the time is logged to keep it that way.
        public void HoldRun(RunSnapshot run, double captureMs)
        {
            heldRun = run;
            if (run != null)
                Debug.Log($"[Save] run snapshot taken in {captureMs:0.00} ms ({run.structures.Count + run.bridges.Count} " +
                          $"structures, {run.fences.Count} fences, {run.sections.Count} island sections).", this);
        }

        // The game is being left: write the held run. Nothing held — no run, or the one on disk is already the
        // latest — writes nothing.
        public void WriteRun()
        {
            if (heldRun == null) return;

            var clock = Stopwatch.StartNew();
            string error;
            try
            {
                error = RunStore.Write(RunFile.ToJson(heldRun));
            }
            catch (System.Exception e)
            {
                error = e.Message;
            }

            if (error != null) Debug.LogError($"[Save] the run could not be saved: {error}", this);
            else Debug.Log($"[Save] run saved to {RunStore.MainPath} in {clock.Elapsed.TotalMilliseconds:0.0} ms.", this);
        }

        // Continue: the bookmark is taken out. The run goes on in memory, and the file is gone, so nothing on
        // disk can take the game back to it. What is held stays: it is still the latest the game knows.
        public void ConsumeRun()
        {
            DeleteRunFile("continued");
        }

        // The saved run read, but could not be put back (RunManager.TryRestoreRun said why): it is set aside
        // rather than deleted, so a wiring mistake or a missing asset costs no one their run — fix it, rename the
        // file back, and it continues.
        public void SetAsideRun()
        {
            heldRun = null;
            try
            {
                Debug.LogError($"[Save] the saved run {RunStore.MainPath} is {RunStore.SetAsideMain()}.", this);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Save] could not set the saved run aside: {e.Message}", this);
            }
        }

        // The run is over or replaced — cashed in, a new run, a progress reset: neither the file nor the held
        // snapshot may bring it back.
        public void DiscardRun()
        {
            heldRun = null;
            DeleteRunFile("discarded");
        }

        private void DeleteRunFile(string why)
        {
            var store = RunStore;
            if (!store.Exists) return;

            try
            {
                store.Delete();
                Debug.Log($"[Save] run file {why}: {store.MainPath}", this);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Save] could not delete {store.MainPath}: {e.Message} — the run may come back on " +
                               "the next launch.", this);
            }
        }
    }
}
