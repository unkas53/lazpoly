using System.Collections.Generic;
using Lazpoly.App.Achievements;
using Steamworks;
using UnityEngine;

namespace Lazpoly.Platform.Steam
{
    /// <summary>
    /// Unlocks achievements through Steam. An achievement must exist on the Steamworks partner site
    /// for the game's App ID; with the development App ID (480) none of ours exist, so Steam refuses
    /// them and this only logs a warning once per achievement.
    /// </summary>
    public sealed class SteamAchievementService : IAchievementService
    {
        private readonly SteamService _steam;
        private readonly HashSet<string> _warned = new HashSet<string>();

        public SteamAchievementService(SteamService steam)
        {
            _steam = steam;
        }

        public void Unlock(string achievementId)
        {
            if (!_steam.IsInitialized || string.IsNullOrEmpty(achievementId)) return;

            if (!SteamUserStats.GetAchievement(achievementId, out bool achieved))
            {
                if (_warned.Add(achievementId))
                {
                    Debug.LogWarning($"[Steam] Achievement '{achievementId}' is not set up for App ID {SteamUtils.GetAppID()}.");
                }

                return;
            }

            if (achieved) return;
            if (SteamUserStats.SetAchievement(achievementId)) SteamUserStats.StoreStats();
        }
    }
}
