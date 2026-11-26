using System;
using UnityEngine;

namespace LittlePeeps
{
    // The run's file format: RunSnapshot as compact JSON, with a version and a checksum — the profile's
    // rules (ProfileFile): readable, a damage check and not a lock.
    //
    // The run is a BOOKMARK (agreed with the user, 2026-11-26): written only when the game is left — the
    // pause's Main Menu or Quit, the window closed — and deleted the moment it is continued. While a run is
    // being played there is nothing on disk to go back to, so a run cannot be rolled back by killing the
    // game; the price is that a crash or a power cut loses the run (never the profile).
    //
    // The checksum is over the file's own TEXT (with the checksum left empty), not over the snapshot
    // re-serialized: a field added later reads as its default in an older file and leaves that file's text —
    // and so its checksum — as it was. So adding to the format needs no new version; only a change an older
    // file cannot be read under does.
    public static class RunFile
    {
        // 2: the checksum moved onto the text (2026-11-26). Version 1 files are refused, not migrated: nobody
        // but the team had one. Once players have saves, an older version is read here, not refused.
        public const int CurrentVersion = 2;

        private const string EmptyChecksum = "\"checksum\":\"\"";

        public static string ToJson(RunSnapshot run)
        {
            run.version = CurrentVersion;
            run.checksum = "";
            string text = JsonUtility.ToJson(run);
            run.checksum = SafeFile.Checksum(text);

            // The root's field is the only "checksum" in the file: nothing nested carries one.
            return text.Replace(EmptyChecksum, $"\"checksum\":\"{run.checksum}\"");
        }

        public static bool TryRead(string json, out RunSnapshot run, out string problem)
        {
            run = null;
            problem = null;

            RunSnapshot data;
            try
            {
                data = JsonUtility.FromJson<RunSnapshot>(json);
            }
            catch (Exception e)
            {
                problem = $"not valid JSON ({e.Message})";
                return false;
            }

            if (data == null)
            {
                problem = "empty";
                return false;
            }

            if (data.version != CurrentVersion)
            {
                problem = $"version {data.version}, this game reads {CurrentVersion}";
                return false;
            }

            string written = $"\"checksum\":\"{data.checksum}\"";
            if (string.IsNullOrEmpty(data.checksum) || !json.Contains(written) ||
                SafeFile.Checksum(json.Replace(written, EmptyChecksum)) != data.checksum)
            {
                problem = "the checksum does not match — the file is damaged or was edited";
                return false;
            }

            run = data;
            return true;
        }
    }
}
