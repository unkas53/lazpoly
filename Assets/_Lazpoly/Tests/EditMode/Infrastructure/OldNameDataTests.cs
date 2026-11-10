using System.Collections.Generic;
using System.IO;
using Lazpoly.Infra.Settings;
using NUnit.Framework;

namespace Lazpoly.Infra.Tests
{
    /// <summary>The rename to Cash Father: the save and the error log under the old name come along, once.</summary>
    public class OldNameDataTests
    {
        private sealed class MemoryStorage : ISettingsStorage
        {
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public int Saves;

            public bool HasKey(string key) => Ints.ContainsKey(key);
            public int GetInt(string key) => Ints[key];
            public float GetFloat(string key) => Ints[key];
            public void SetInt(string key, int value) => Ints[key] = value;
            public void SetFloat(string key, float value) => Ints[key] = (int)value;
            public string GetString(string key) => string.Empty;
            public void SetString(string key, string value) { }
            public void Save() => Saves++;
        }

        private string _root;
        private string _old;
        private string _new;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "lazpoly-old-name-" + System.Guid.NewGuid().ToString("N"));
            _old = Path.Combine(_root, "DefaultCompany", OldNameData.OldProductName);
            _new = Path.Combine(_root, "DefaultCompany", "Cash Father");
            Directory.CreateDirectory(_old);
            File.WriteAllText(Path.Combine(_old, "lazpoly-kayit.dat"), "old save");
            File.WriteAllText(Path.Combine(_old, "lazpoly-hata.log"), "old log");
            Directory.CreateDirectory(Path.Combine(_old, "Screenshots"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void TheOldFolderLiesBesideTheNewOne()
        {
            Assert.AreEqual(Path.GetFullPath(_old), Path.GetFullPath(OldNameData.OldFolder(_new)));
            Assert.AreEqual(Path.GetFullPath(_old), Path.GetFullPath(OldNameData.OldFolder(_new + Path.DirectorySeparatorChar)), "a closing slash changes nothing");
            Assert.IsNull(OldNameData.OldFolder(""));
        }

        [Test]
        public void TheFirstStartBringsTheOldFilesOverAndLeavesThemInPlace()
        {
            var storage = new MemoryStorage();
            OldNameData.Adopt(storage, _new, "DefaultCompany", "Cash Father");

            Assert.AreEqual("old save", File.ReadAllText(Path.Combine(_new, "lazpoly-kayit.dat")));
            Assert.AreEqual("old log", File.ReadAllText(Path.Combine(_new, "lazpoly-hata.log")));
            Assert.IsFalse(Directory.Exists(Path.Combine(_new, "Screenshots")), "folders inside stay behind");
            Assert.IsTrue(File.Exists(Path.Combine(_old, "lazpoly-kayit.dat")), "the old save is not moved away");
            Assert.IsTrue(storage.HasKey(OldNameData.DoneKey), "it notes that it ran");
            Assert.AreEqual(1, storage.Saves);
        }

        [Test]
        public void AFileTheNewFolderHasIsKept()
        {
            Directory.CreateDirectory(_new);
            File.WriteAllText(Path.Combine(_new, "lazpoly-kayit.dat"), "new save");
            Assert.AreEqual(1, OldNameData.CopyMissingFiles(_old, _new), "only the log was missing");
            Assert.AreEqual("new save", File.ReadAllText(Path.Combine(_new, "lazpoly-kayit.dat")));
        }

        [Test]
        public void ASaveDeletedLaterNeverComesBack()
        {
            var storage = new MemoryStorage();
            OldNameData.Adopt(storage, _new, "DefaultCompany", "Cash Father");
            File.Delete(Path.Combine(_new, "lazpoly-kayit.dat")); // The game ended.
            OldNameData.Adopt(storage, _new, "DefaultCompany", "Cash Father");
            Assert.IsFalse(File.Exists(Path.Combine(_new, "lazpoly-kayit.dat")));
            Assert.AreEqual(1, storage.Saves, "the second start does nothing");
        }

        [Test]
        public void UnderTheOldNameOrWithoutOldDataNothingHappens()
        {
            var storage = new MemoryStorage();
            OldNameData.Adopt(storage, _old, "DefaultCompany", OldNameData.OldProductName);
            Assert.IsFalse(storage.HasKey(OldNameData.DoneKey), "the old build does not take part");

            string lonely = Path.Combine(_root, "Elsewhere", "Cash Father");
            Assert.AreEqual(0, OldNameData.CopyMissingFiles(OldNameData.OldFolder(lonely), lonely));
            Assert.IsFalse(Directory.Exists(lonely), "no empty folder is made");
        }
    }
}
