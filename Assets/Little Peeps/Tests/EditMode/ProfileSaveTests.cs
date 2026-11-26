using System;
using System.IO;
using NUnit.Framework;

namespace LittlePeeps.Tests
{
    // The profile's file format. JsonUtility is native, so these run in the Test Runner, not offline.
    public class ProfileFileTests
    {
        private static MetaContext Sample()
        {
            var meta = new MetaContext { prestigePoints = 7, agePointsAwarded = 4, harvestPointsAwarded = 2 };
            meta.TryTake("productivity", 2);
            meta.TryTake("magnet", 1);
            return meta;
        }

        [Test]
        public void RoundTrip_KeepsTheWholeProfile()
        {
            Assert.That(ProfileFile.TryRead(ProfileFile.ToJson(Sample()), out MetaContext read, out string problem),
                        Is.True, problem);

            Assert.That(read.prestigePoints, Is.EqualTo(7));
            Assert.That(read.agePointsAwarded, Is.EqualTo(4));
            Assert.That(read.harvestPointsAwarded, Is.EqualTo(2));
            Assert.That(read.globalUpgrades["productivity"], Is.EqualTo(2), "an upgrade keeps the points it holds");
            Assert.That(read.globalUpgrades["magnet"], Is.EqualTo(1));
            Assert.That(read.FreePoints, Is.EqualTo(4));
        }

        // The same profile, the same file — whatever order the upgrades were taken in.
        [Test]
        public void TheSameProfile_WritesTheSameFile()
        {
            var other = new MetaContext { prestigePoints = 7, agePointsAwarded = 4, harvestPointsAwarded = 2 };
            other.TryTake("magnet", 1);
            other.TryTake("productivity", 2);

            Assert.That(ProfileFile.ToJson(other), Is.EqualTo(ProfileFile.ToJson(Sample())));
        }

        [Test]
        public void AChangedNumber_FailsTheChecksum()
        {
            string json = ProfileFile.ToJson(Sample());
            string edited = json.Replace("\"prestigePoints\": 7", "\"prestigePoints\": 70");
            Assume.That(edited, Is.Not.EqualTo(json), "the edit must land for the test to mean anything");

            Assert.That(ProfileFile.TryRead(edited, out _, out string problem), Is.False);
            Assert.That(problem, Does.Contain("checksum"));
        }

        // Layout is not content: the checksum is over what the file says, not how it is indented.
        [Test]
        public void Reformatting_KeepsTheChecksum()
        {
            string compact = ProfileFile.ToJson(Sample()).Replace("\n", "").Replace("    ", "");

            Assert.That(ProfileFile.TryRead(compact, out MetaContext read, out string problem), Is.True, problem);
            Assert.That(read.prestigePoints, Is.EqualTo(7));
        }

        [Test]
        public void AProfileFromANewerGame_IsRefusedAsSuch()
        {
            string original = ProfileFile.ToJson(Sample());
            string json = original
                .Replace($"\"version\": {ProfileFile.CurrentVersion}", $"\"version\": {ProfileFile.CurrentVersion + 1}");
            Assume.That(json, Is.Not.EqualTo(original), "the edit must land for the test to mean anything");

            Assert.That(ProfileFile.TryRead(json, out _, out string problem), Is.False);
            Assert.That(problem, Does.Contain("version"), "a newer profile is not 'damaged'");
        }

        [TestCase("")]
        [TestCase("{ not json")]
        [TestCase("{}")]
        public void NotAProfile_IsRefused(string json)
        {
            Assert.That(ProfileFile.TryRead(json, out MetaContext meta, out string problem), Is.False);
            Assert.That(meta, Is.Null);
            Assert.That(problem, Is.Not.Empty);
        }
    }

    // The profile on disk, in a temp folder of its own. Native through ProfileFile — Test Runner only.
    public class ProfileStoreTests
    {
        private string folder;
        private ProfileStore store;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "LittlePeepsProfileTests_" + Guid.NewGuid().ToString("N"));
            store = new ProfileStore(folder, "meta.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        [Test]
        public void NothingOnDisk_IsAFreshProfile_AndNoProblem()
        {
            ProfileLoad load = store.Load();

            Assert.That(load.Source, Is.EqualTo(SaveSource.None));
            Assert.That(load.Meta, Is.Not.Null);
            Assert.That(load.Meta.prestigePoints, Is.Zero);
            Assert.That(load.Problems, Is.Empty);
        }

        [Test]
        public void Save_ThenLoad_ReadsItBack()
        {
            Assert.That(store.Save(new MetaContext { prestigePoints = 3 }), Is.Null);

            ProfileLoad load = store.Load();

            Assert.That(load.Source, Is.EqualTo(SaveSource.Main));
            Assert.That(load.Meta.prestigePoints, Is.EqualTo(3));
            Assert.That(File.Exists(store.TempPath), Is.False, "the working copy is swapped in, not left behind");
        }

        [Test]
        public void ASecondSave_KeepsTheFirstAsTheBackup()
        {
            Assert.That(store.Save(new MetaContext { prestigePoints = 3 }), Is.Null);
            Assert.That(store.Save(new MetaContext { prestigePoints = 5 }), Is.Null);

            Assert.That(File.Exists(store.BackupPath), Is.True);
            Assert.That(ProfileFile.TryRead(File.ReadAllText(store.BackupPath), out MetaContext backup, out _), Is.True);
            Assert.That(backup.prestigePoints, Is.EqualTo(3));
        }

        [Test]
        public void ADamagedProfile_FallsBackToTheBackup_AndIsKeptAside()
        {
            Assert.That(store.Save(new MetaContext { prestigePoints = 3 }), Is.Null);
            Assert.That(store.Save(new MetaContext { prestigePoints = 5 }), Is.Null);
            File.WriteAllText(store.MainPath, "{ \"version\": 1, \"prest");   // a write cut short

            ProfileLoad load = store.Load();

            Assert.That(load.Source, Is.EqualTo(SaveSource.Backup));
            Assert.That(load.Meta.prestigePoints, Is.EqualTo(3));
            Assert.That(load.Problems, Has.Count.EqualTo(1));
            Assert.That(File.Exists(store.MainPath), Is.False, "the damaged file is moved, not left to be loaded again");
            Assert.That(Directory.GetFiles(folder, "meta.json.unreadable-*"), Has.Length.EqualTo(1),
                        "and never deleted");
        }

        [Test]
        public void AfterAFallback_TheNextSaveWritesANewProfile()
        {
            Assert.That(store.Save(new MetaContext { prestigePoints = 3 }), Is.Null);
            Assert.That(store.Save(new MetaContext { prestigePoints = 5 }), Is.Null);
            File.WriteAllText(store.MainPath, "garbage");
            store.Load();

            Assert.That(store.Save(new MetaContext { prestigePoints = 8 }), Is.Null);

            ProfileLoad load = store.Load();
            Assert.That(load.Source, Is.EqualTo(SaveSource.Main));
            Assert.That(load.Meta.prestigePoints, Is.EqualTo(8));
        }

        [Test]
        public void NothingReadable_IsAFreshProfile_WithTheProblemsSaid()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(store.MainPath, "garbage");
            File.WriteAllText(store.BackupPath, "garbage too");

            ProfileLoad load = store.Load();

            Assert.That(load.Source, Is.EqualTo(SaveSource.None));
            Assert.That(load.Meta.prestigePoints, Is.Zero);
            Assert.That(load.Problems, Has.Count.EqualTo(2));
        }

        [Test]
        public void Delete_RemovesTheProfileAndItsBackup()
        {
            store.Save(new MetaContext { prestigePoints = 3 });
            store.Save(new MetaContext { prestigePoints = 5 });

            store.Delete();

            Assert.That(File.Exists(store.MainPath), Is.False);
            Assert.That(File.Exists(store.BackupPath), Is.False);
            Assert.That(store.Load().Source, Is.EqualTo(SaveSource.None));
        }
    }
}
