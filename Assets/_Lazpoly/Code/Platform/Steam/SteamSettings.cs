namespace Lazpoly.Platform.Steam
{
    public static class SteamSettings
    {
        /// <summary>Valve's public test app (Spacewar). Replace with Lazpoly's App ID once Steamworks issues one.</summary>
        public const uint DevelopmentAppId = 480;

        /// <summary>The App ID the game runs as. Must match steam_appid.txt during development.</summary>
        public const uint AppId = DevelopmentAppId;

        public static bool IsDevelopmentAppId => AppId == DevelopmentAppId;
    }
}
