using System.Collections.Generic;
using Lazpoly.Infra.Settings;
using NUnit.Framework;

namespace Lazpoly.Infra.Tests
{
    public class GameSettingsTests
    {
        private sealed class MemoryStorage : ISettingsStorage
        {
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
            public int Saves;

            public bool HasKey(string key) => Values.ContainsKey(key);
            public int GetInt(string key) => (int)Values[key];
            public float GetFloat(string key) => Values[key];
            public void SetInt(string key, int value) => Values[key] = value;
            public void SetFloat(string key, float value) => Values[key] = value;
            public void Save() => Saves++;
        }

        [Test]
        public void EmptyStorageGivesTheDefaults()
        {
            Assert.IsTrue(SettingsSerializer.Read(new MemoryStorage()).SameAs(GameSettings.Default));
        }

        [Test]
        public void SettingsSurviveARoundTrip()
        {
            var storage = new MemoryStorage();
            GameSettings chosen = GameSettings.Default.WithMasterVolume(0.25f).WithFullscreen(false).WithVSync(false)
                .WithAiSpeed(AiSpeed.Fast).WithFastAnimations(true);

            SettingsSerializer.Write(storage, chosen);

            Assert.AreEqual(1, storage.Saves);
            Assert.IsTrue(SettingsSerializer.Read(storage).SameAs(chosen));
        }

        [Test]
        public void BrokenStoredValuesAreRepaired()
        {
            var storage = new MemoryStorage();
            SettingsSerializer.Write(storage, GameSettings.Default);
            storage.Values["lazpoly.settings.volume"] = 7f;
            storage.Values["lazpoly.settings.aiSpeed"] = 42;

            GameSettings read = SettingsSerializer.Read(storage);

            Assert.AreEqual(1f, read.MasterVolume);
            Assert.AreEqual(AiSpeed.Normal, read.AiSpeed);
        }

        [Test]
        public void SpeedsMeanWhatTheySay()
        {
            Assert.Greater(GameSettings.Default.WithAiSpeed(AiSpeed.Slow).AiMoveDelaySeconds, GameSettings.Default.AiMoveDelaySeconds);
            Assert.Less(GameSettings.Default.WithAiSpeed(AiSpeed.Fast).AiMoveDelaySeconds, GameSettings.Default.AiMoveDelaySeconds);
            Assert.AreEqual(2f, GameSettings.Default.WithFastAnimations(true).AnimationSpeed);
            Assert.AreEqual(0f, GameSettings.Default.WithMasterVolume(-1f).MasterVolume);
        }
    }
}
