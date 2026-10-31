using Lazpoly.App.Lobby;
using Lazpoly.Infra;
using Lazpoly.Net.Transport;
using Lazpoly.Platform.Steam;
using UnityEngine;

namespace Lazpoly.Boot
{
    /// <summary>
    /// Composition root. Runs before the first scene loads, whichever scene that is,
    /// so pressing Play in any scene starts the same services.
    /// </summary>
    public static class LazpolyBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Start()
        {
            var steam = new SteamService();
            var lobby = new SteamLobbyService(steam);
            var transport = new SteamTransport(steam, lobby.IsMember);
            lobby.MemberLeft += transport.DropPeer;

            // Registration order is initialization order: Steam must be up before lobby and transport.
            var registry = new ServiceRegistry();
            registry.Register(steam);
            registry.Register<ILobbyService>(lobby);
            registry.Register<ITransport>(transport);

            GameServices.SetRegistry(registry);

            var host = new GameObject("[Lazpoly Services]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<ServiceRunner>().Run(registry);
        }
    }
}
