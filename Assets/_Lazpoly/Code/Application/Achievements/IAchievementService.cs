namespace Lazpoly.App.Achievements
{
    /// <summary>Where unlocked achievements go (Steam in the shipped game).</summary>
    public interface IAchievementService
    {
        /// <summary>Unlocks the achievement; unlocking one that is already unlocked does nothing.</summary>
        void Unlock(string achievementId);
    }
}
