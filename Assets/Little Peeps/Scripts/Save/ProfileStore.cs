using System.Collections.Generic;

namespace LittlePeeps
{
    public struct ProfileLoad
    {
        public MetaContext Meta;          // never null: a fresh profile when nothing could be read
        public SaveSource Source;
        public List<string> Problems;     // why a file that was there was not used; empty when all is well
    }

    // The profile on disk: ProfileFile's format in a SafeFile, so a crash or a full disk mid-write can never
    // cost the profile that was there before (see SafeFile for how). A plain class over a folder and a file
    // name, so the tests run it in a temp folder.
    public class ProfileStore
    {
        private readonly SafeFile file;

        public ProfileStore(string folder, string fileName)
        {
            file = new SafeFile(folder, fileName);
        }

        public string MainPath => file.MainPath;
        public string BackupPath => file.BackupPath;
        public string TempPath => file.TempPath;

        // Null on success, else what went wrong. A failed save leaves the files as they were.
        public string Save(MetaContext meta)
        {
            if (meta == null) return "no profile to save";

            string json;
            try
            {
                json = ProfileFile.ToJson(meta);
            }
            catch (System.Exception e)
            {
                return $"could not write {MainPath}: {e.Message}";
            }
            return file.Write(json);
        }

        public ProfileLoad Load()
        {
            var problems = new List<string>();
            bool read = file.TryLoad<MetaContext>(ProfileFile.TryRead, out MetaContext meta, out SaveSource source,
                                                  problems);
            return new ProfileLoad { Meta = read ? meta : new MetaContext(), Source = source, Problems = problems };
        }

        // The profile and its working copies. The set-aside unreadable files stay: they are evidence, not
        // the profile.
        public void Delete() => file.Delete();
    }
}
