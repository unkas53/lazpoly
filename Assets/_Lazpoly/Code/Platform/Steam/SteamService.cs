using System;
using Lazpoly.Infra;
using Steamworks;
using UnityEngine;

namespace Lazpoly.Platform.Steam
{
    /// <summary>
    /// Owns the Steamworks API lifetime: init, per-frame callbacks and shutdown.
    /// The only class that calls SteamAPI directly; other Steam features build on <see cref="IsInitialized"/>.
    /// </summary>
    public sealed class SteamService : IGameService
    {
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (!Packsize.Test())
            {
                Debug.LogError("[Steam] Packsize test failed: wrong Steamworks.NET build for this platform.");
                return;
            }

            if (!DllCheck.Test())
            {
                Debug.LogError("[Steam] DllCheck failed: steam_api native library is missing or outdated.");
                return;
            }

            try
            {
#if !UNITY_EDITOR
                // Relaunches through Steam when the exe was started directly. Skipped for the test App ID.
                if (!SteamSettings.IsDevelopmentAppId &&
                    SteamAPI.RestartAppIfNecessary(new AppId_t(SteamSettings.AppId)))
                {
                    Application.Quit();
                    return;
                }
#endif
                IsInitialized = SteamAPI.Init();
            }
            catch (DllNotFoundException exception)
            {
                Debug.LogError($"[Steam] steam_api native library could not be loaded: {exception.Message}");
                return;
            }

            if (!IsInitialized)
            {
                Debug.LogError("[Steam] SteamAPI.Init failed. Is the Steam client running and logged in, " +
                               "and does steam_appid.txt sit in the project root (or next to the build's exe)?");
                return;
            }

            Debug.Log($"[Steam] Initialized as '{SteamFriends.GetPersonaName()}' (App ID {SteamUtils.GetAppID()}).");
        }

        public void Tick()
        {
            if (IsInitialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            SteamAPI.Shutdown();
            IsInitialized = false;
        }
    }
}
