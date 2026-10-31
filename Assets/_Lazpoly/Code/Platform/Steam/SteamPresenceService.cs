using Lazpoly.App.Presence;
using Steamworks;

namespace Lazpoly.Platform.Steam
{
    /// <summary>
    /// Sets the Steam Rich Presence "status" line, shown in the friends list under "View game info".
    /// With Lazpoly's own App ID it also sets "steam_display" to the "#Status" token, which the Rich
    /// Presence files in the repository's Steam folder turn into that line in the friends list itself.
    /// The test App ID (480) has its own tokens, so there the display key is left alone.
    /// </summary>
    public sealed class SteamPresenceService : IPresenceService
    {
        private const string StatusKey = "status";
        private const string DisplayKey = "steam_display";
        private const string DisplayToken = "#Status";

        private readonly SteamService _steam;
        private string _shown;

        public SteamPresenceService(SteamService steam)
        {
            _steam = steam;
        }

        public void Show(string status)
        {
            if (!_steam.IsInitialized || status == _shown) return;
            if (!SteamFriends.SetRichPresence(StatusKey, status)) return;
            if (!SteamSettings.IsDevelopmentAppId) SteamFriends.SetRichPresence(DisplayKey, DisplayToken);
            _shown = status;
        }

        public void Clear()
        {
            if (!_steam.IsInitialized) return;
            SteamFriends.ClearRichPresence();
            _shown = null;
        }
    }
}
