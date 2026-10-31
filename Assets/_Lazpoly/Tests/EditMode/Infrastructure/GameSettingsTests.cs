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
            GameSettings chosen = GameSettings.Default.WithMasterVolume(0.25f).WithEffectsVolume(0.5f).WithMusicVolume(0f)
                .WithFullscreen(false).WithVSync(false).WithAiSpeed(AiSpeed.Fast).WithFastAnimations(true)
                .WithQuality(GraphicsQuality.Low).WithScreenSize(1600, 900);

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
            storage.Values["lazpoly.settings.quality"] = -3;
            storage.Values["lazpoly.settings.width"] = 20;
            storage.Values["lazpoly.settings.height"] = 2000;

            GameSettings read = SettingsSerializer.Read(storage);

            Assert.AreEqual(1f, read.MasterVolume);
            Assert.AreEqual(AiSpeed.Normal, read.AiSpeed);
            Assert.AreEqual(GraphicsQuality.High, read.Quality);
            Assert.IsFalse(read.HasScreenSize, "A silly size means the screen stays as it is.");
        }

        [Test]
        public void SettingsSavedByAnOlderVersionGetTheNewDefaults()
        {
            var storage = new MemoryStorage();
            storage.Values["lazpoly.settings.volume"] = 0.3f;
            storage.Values["lazpoly.settings.fullscreen"] = 0;

            GameSettings read = SettingsSerializer.Read(storage);

            Assert.AreEqual(0.3f, read.MasterVolume);
            Assert.IsFalse(read.Fullscreen);
            Assert.AreEqual(GameSettings.Default.EffectsVolume, read.EffectsVolume);
            Assert.AreEqual(GameSettings.Default.MusicVolume, read.MusicVolume);
            Assert.AreEqual(GameSettings.Default.Quality, read.Quality);
        }

        [Test]
        public void VolumesStayBetweenSilentAndFull()
        {
            Assert.AreEqual(1f, GameSettings.Default.WithEffectsVolume(3f).EffectsVolume);
            Assert.AreEqual(0f, GameSettings.Default.WithMusicVolume(-2f).MusicVolume);
            Assert.AreEqual(GameSettings.Default.MusicVolume, GameSettings.Default.WithMusicVolume(float.NaN).MusicVolume);
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
