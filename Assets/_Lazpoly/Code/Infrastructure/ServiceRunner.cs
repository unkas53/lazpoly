using UnityEngine;

namespace Lazpoly.Infra
{
    /// <summary>
    /// Persistent MonoBehaviour that forwards Unity's frame loop to the service registry.
    /// </summary>
    public sealed class ServiceRunner : MonoBehaviour
    {
        private ServiceRegistry _registry;

        public void Run(ServiceRegistry registry)
        {
            _registry = registry;
            _registry.InitializeAll();
        }

        private void Update()
        {
            _registry?.TickAll();
        }

        private void OnDestroy()
        {
            _registry?.ShutdownAll();
        }
    }
}
