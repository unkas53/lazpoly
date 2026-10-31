using UnityEngine;

namespace Lazpoly.Infra.Settings
{
    /// <summary>Key-value storage for settings, so the reading and writing rules can be tested without PlayerPrefs.</summary>
    public interface ISettingsStorage
    {
        bool HasKey(string key);
        int GetInt(string key);
        float GetFloat(string key);
        void SetInt(string key, int value);
        void SetFloat(string key, float value);
        void Save();
    }

    /// <summary>Unity's PlayerPrefs (the registry on Windows).</summary>
    public sealed class PlayerPrefsStorage : ISettingsStorage
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key) => PlayerPrefs.GetInt(key);
        public float GetFloat(string key) => PlayerPrefs.GetFloat(key);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>Reads and writes <see cref="GameSettings"/>; missing or broken values fall back to the defaults.</summary>
    public static class SettingsSerializer
    {
        private const string Prefix = "lazpoly.settings.";
        private const string VolumeKey = Prefix + "volume";
        private const string FullscreenKey = Prefix + "fullscreen";
        private const string VSyncKey = Prefix + "vsync";
        private const string AiSpeedKey = Prefix + "aiSpeed";
        private const string FastAnimationsKey = Prefix + "fastAnimations";

        public static GameSettings Read(ISettingsStorage storage)
        {
            GameSettings d = GameSettings.Default;
            return new GameSettings(
                storage.HasKey(VolumeKey) ? storage.GetFloat(VolumeKey) : d.MasterVolume,
                storage.HasKey(FullscreenKey) ? storage.GetInt(FullscreenKey) != 0 : d.Fullscreen,
                storage.HasKey(VSyncKey) ? storage.GetInt(VSyncKey) != 0 : d.VSync,
                storage.HasKey(AiSpeedKey) ? (AiSpeed)storage.GetInt(AiSpeedKey) : d.AiSpeed,
                storage.HasKey(FastAnimationsKey) ? storage.GetInt(FastAnimationsKey) != 0 : d.FastAnimations);
        }

        public static void Write(ISettingsStorage storage, GameSettings settings)
        {
            storage.SetFloat(VolumeKey, settings.MasterVolume);
            storage.SetInt(FullscreenKey, settings.Fullscreen ? 1 : 0);
            storage.SetInt(VSyncKey, settings.VSync ? 1 : 0);
            storage.SetInt(AiSpeedKey, (int)settings.AiSpeed);
            storage.SetInt(FastAnimationsKey, settings.FastAnimations ? 1 : 0);
            storage.Save();
        }
    }
}
