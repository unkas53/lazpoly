using System;
using System.Collections.Generic;

namespace Lazpoly.Infra
{
    /// <summary>
    /// Holds the process-wide services and drives their lifecycle.
    /// Plain C# so it can be unit tested without a scene.
    /// </summary>
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _byType = new Dictionary<Type, object>();
        private readonly List<IGameService> _lifecycle = new List<IGameService>();
        private bool _initialized;
        private bool _shutDown;

        /// <summary>Registers a service under the type <typeparamref name="T"/>. Must happen before <see cref="InitializeAll"/>.</summary>
        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (_initialized) throw new InvalidOperationException("Services must be registered before InitializeAll.");
            if (_byType.ContainsKey(typeof(T)))
            {
                throw new InvalidOperationException($"A service of type {typeof(T).Name} is already registered.");
            }

            _byType.Add(typeof(T), service);
            if (service is IGameService lifecycleService && !_lifecycle.Contains(lifecycleService))
            {
                _lifecycle.Add(lifecycleService);
            }
        }

        public T Get<T>() where T : class
        {
            if (TryGet(out T service)) return service;
            throw new InvalidOperationException($"No service of type {typeof(T).Name} is registered.");
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_byType.TryGetValue(typeof(T), out object value))
            {
                service = (T)value;
                return true;
            }

            service = null;
            return false;
        }

        public void InitializeAll()
        {
            if (_initialized) return;
            _initialized = true;
            foreach (IGameService service in _lifecycle)
            {
                service.Initialize();
            }
        }

        public void TickAll()
        {
            if (!_initialized || _shutDown) return;
            foreach (IGameService service in _lifecycle)
            {
                service.Tick();
            }
        }

        public void ShutdownAll()
        {
            if (!_initialized || _shutDown) return;
            _shutDown = true;
            for (int i = _lifecycle.Count - 1; i >= 0; i--)
            {
                _lifecycle[i].Shutdown();
            }
        }
    }
}
