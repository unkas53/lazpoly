using Lazpoly.Infra;
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
            var registry = new ServiceRegistry();
            registry.Register(new SteamService());

            GameServices.SetRegistry(registry);

            var host = new GameObject("[Lazpoly Services]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<ServiceRunner>().Run(registry);
        }
    }
}
