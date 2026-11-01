using Lazpoly.Infra.Settings;
using UnityEngine;

namespace Lazpoly.Presentation.Setup
{
    /// <summary>
    /// The room of the online game this machine was in as a guest, kept between runs so the main menu can
    /// offer "reconnect" after a crash or a closed window. Forgotten when the game ends, is left on
    /// purpose, or the room turns out to be gone.
    /// </summary>
    public static class LastRoomStore
    {
        private const string Key = "lazpoly.online.lastRoom";
        private static ISettingsStorage _storage;

        /// <summary>Where the room is kept; tests swap in a memory store. Null means the player's preferences.</summary>
        public static ISettingsStorage Storage
        {
            get => _storage ??= new PlayerPrefsStorage();
            set => _storage = value;
        }

        public static void Remember(ulong roomId)
        {
            if (roomId == 0) return;
            Storage.SetString(Key, roomId.ToString());
            Storage.Save();
        }

        public static void Forget()
        {
            if (!Storage.HasKey(Key)) return;
            Storage.SetString(Key, string.Empty);
            Storage.Save();
        }

        public static bool TryGet(out ulong roomId)
        {
            roomId = 0;
            return Storage.HasKey(Key) && ulong.TryParse(Storage.GetString(Key), out roomId) && roomId != 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _storage = null;
        }
    }
}
