namespace Lazpoly.Infra
{
    /// <summary>
    /// A long-lived service owned by the game process (Steam, audio, settings, save).
    /// Services are initialized in registration order and shut down in reverse order.
    /// </summary>
    public interface IGameService
    {
        void Initialize();

        /// <summary>Called once per frame while the game runs.</summary>
        void Tick();

        void Shutdown();
    }
}
