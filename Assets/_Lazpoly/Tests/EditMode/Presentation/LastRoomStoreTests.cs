using System.Collections.Generic;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Setup;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class LastRoomStoreTests
    {
        private sealed class MemoryStorage : ISettingsStorage
        {
            public readonly Dictionary<string, string> Texts = new Dictionary<string, string>();
            public int Saves;
            public bool HasKey(string key) => Texts.ContainsKey(key);
            public int GetInt(string key) => 0;
            public float GetFloat(string key) => 0f;
            public void SetInt(string key, int value) { }
            public void SetFloat(string key, float value) { }
            public string GetString(string key) => Texts.TryGetValue(key, out string text) ? text : string.Empty;
            public void SetString(string key, string value) => Texts[key] = value;
            public void Save() => Saves++;
        }

        private MemoryStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _storage = new MemoryStorage();
            LastRoomStore.Storage = _storage;
        }

        [TearDown]
        public void TearDown() => LastRoomStore.Storage = null;

        [Test]
        public void RemembersARoomUntilItIsForgotten()
        {
            Assert.IsFalse(LastRoomStore.TryGet(out _));

            LastRoomStore.Remember(109775241234567890UL);
            Assert.IsTrue(LastRoomStore.TryGet(out ulong room));
            Assert.AreEqual(109775241234567890UL, room);
            Assert.AreEqual(1, _storage.Saves);

            LastRoomStore.Forget();
            Assert.IsFalse(LastRoomStore.TryGet(out _));
            LastRoomStore.Forget();
            Assert.AreEqual(2, _storage.Saves, "Forgetting nothing writes nothing.");
        }

        [Test]
        public void IgnoresNothingAndRubbish()
        {
            LastRoomStore.Remember(0);
            Assert.IsFalse(LastRoomStore.TryGet(out _));
            Assert.AreEqual(0, _storage.Saves);

            _storage.Texts["lazpoly.online.lastRoom"] = "not a room";
            Assert.IsFalse(LastRoomStore.TryGet(out _));
        }
    }
}
