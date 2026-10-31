using Lazpoly.App.Presence;
using Steamworks;

namespace Lazpoly.Platform.Steam
{
    /// <summary>
    /// Sets the Steam Rich Presence "status" line, shown in the friends list under "View game info".
    /// A localized line in the friends list itself needs a Rich Presence file on the partner site (real App ID).
    /// </summary>
    public sealed class SteamPresenceService : IPresenceService
    {
        private const string StatusKey = "status";

        private readonly SteamService _steam;
        private string _shown;

        public SteamPresenceService(SteamService steam)
        {
            _steam = steam;
        }

        public void Show(string status)
        {
            if (!_steam.IsInitialized || status == _shown) return;
            if (SteamFriends.SetRichPresence(StatusKey, status)) _shown = status;
        }

        public void Clear()
        {
            if (!_steam.IsInitialized) return;
            SteamFriends.ClearRichPresence();
            _shown = null;
        }
    }
}
