namespace Lazpoly.App.Presence
{
    /// <summary>What the player's friends see about them (Steam Rich Presence in the shipped game).</summary>
    public interface IPresenceService
    {
        /// <summary>Shows a short status line; the same line again does nothing.</summary>
        void Show(string status);

        void Clear();
    }
}
