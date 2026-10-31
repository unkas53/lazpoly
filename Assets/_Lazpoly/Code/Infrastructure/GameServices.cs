using UnityEngine;

namespace Lazpoly.Infra
{
    /// <summary>
    /// Access point for the registry created by the bootstrap. Scene code reads services from here;
    /// game rules never do (they receive what they need through constructors).
    /// </summary>
    public static class GameServices
    {
        public static ServiceRegistry Registry { get; private set; }

        public static void SetRegistry(ServiceRegistry registry)
        {
            Registry = registry;
        }

        // Clears static state when Domain Reload is disabled in Enter Play Mode settings.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Registry = null;
        }
    }
}
