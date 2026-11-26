using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LittlePeeps
{
    public enum SaveSource
    {
        None,     // no usable file
        Main,     // the file itself
        Backup,   // the main file was missing or unreadable; the copy before the last write
    }

    // Reads one kind of save from its text: false with the reason when the text is not one this game can use.
    public delegate bool SaveParser<T>(string text, out T value, out string problem);

    // One save file on disk, written so that a crash or a full disk mid-write can never cost what was there
    // before. Text in, text out — what the text means is the caller's (ProfileFile, RunFile).
    //
    //   - Write puts the new text in <file>.tmp first and only then swaps it in (File.Replace), keeping the
    //     text it replaces as <file>.bak. At every moment one complete file is on disk.
    //   - Load reads the main file, and the backup if the main one is missing or unreadable.
    //   - A file that is there but unreadable (damaged, edited, or from a newer game) is set aside as
    //     <file>.unreadable-<time> — never deleted and never overwritten, so nothing is lost for good, and a
    //     player's report can still be looked into.
    //
    // A plain class over a folder and a file name, so the tests run it in a temp folder. It reports and never
    // logs: SaveSystem says what happened, in the console, once.
    public class SafeFile
    {
        private readonly string folder;
        private readonly string fileName;

        public SafeFile(string folder, string fileName)
        {
            this.folder = folder;
            this.fileName = fileName;
        }

        public string MainPath => Path.Combine(folder, fileName);
        public string BackupPath => MainPath + ".bak";
        public string TempPath => MainPath + ".tmp";

        public bool Exists => File.Exists(MainPath) || File.Exists(BackupPath);

        // Null on success, else what went wrong. A failed write leaves the files as they were.
        public string Write(string text)
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(TempPath, text);

                if (File.Exists(MainPath)) File.Replace(TempPath, MainPath, BackupPath, ignoreMetadataErrors: true);
                else File.Move(TempPath, MainPath);

                return null;
            }
            catch (Exception e)
            {
                return $"could not write {MainPath}: {e.Message}";
            }
        }

        // The first readable of the main file and the backup. `problems` collects why a file that was there
        // was not used; empty when all is well.
        public bool TryLoad<T>(SaveParser<T> parse, out T value, out SaveSource source, List<string> problems)
        {
            if (TryRead(MainPath, parse, problems, out value)) { source = SaveSource.Main; return true; }
            if (TryRead(BackupPath, parse, problems, out value)) { source = SaveSource.Backup; return true; }

            source = SaveSource.None;
            return false;
        }

        // Move a file that reads but cannot be used (a saved run whose island no longer loads) out of the way,
        // as an unreadable one is: kept, never loaded again. Returns what was done, for the log.
        public string SetAsideMain()
        {
            if (!File.Exists(MainPath)) return "nothing to set aside";
            string done = SetAside(MainPath);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            return done;
        }

        // The file and its working copies. The set-aside unreadable files stay: they are evidence, not the save.
        public void Delete()
        {
            foreach (string path in new[] { MainPath, BackupPath, TempPath })
                if (File.Exists(path)) File.Delete(path);
        }

        private static bool TryRead<T>(string path, SaveParser<T> parse, List<string> problems, out T value)
        {
            value = default;
            if (!File.Exists(path)) return false;

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                problems.Add($"could not read {path}: {e.Message}");
                return false;
            }

            if (parse(text, out value, out string why)) return true;

            problems.Add($"{Path.GetFileName(path)} is unreadable ({why}); {SetAside(path)}");
            return false;
        }

        private static string SetAside(string path)
        {
            string aside = $"{path}.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}";
            try
            {
                File.Move(path, aside);
                return $"kept as {Path.GetFileName(aside)}";
            }
            catch (Exception e)
            {
                return $"left where it is — could not set it aside ({e.Message})";
            }
        }

        // The damage check every save file carries: SHA-256 of its content as hex. A DAMAGE check, not a lock —
        // see ProfileFile for why.
        public static string Checksum(string canonical)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }
    }
}
