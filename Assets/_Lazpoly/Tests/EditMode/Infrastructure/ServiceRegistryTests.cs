using System;
using System.Collections.Generic;
using Lazpoly.Infra;
using NUnit.Framework;

namespace Lazpoly.Infra.Tests
{
    public class ServiceRegistryTests
    {
        private sealed class RecordingService : IGameService
        {
            private readonly string _name;
            private readonly List<string> _log;

            public RecordingService(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            public void Initialize() => _log.Add($"init {_name}");
            public void Tick() => _log.Add($"tick {_name}");
            public void Shutdown() => _log.Add($"shutdown {_name}");
        }

        private sealed class OtherService : IGameService
        {
            public void Initialize() { }
            public void Tick() { }
            public void Shutdown() { }
        }

        [Test]
        public void LifecycleRunsInOrderAndShutsDownInReverse()
        {
            var log = new List<string>();
            var registry = new ServiceRegistry();
            registry.Register(new RecordingService("a", log));
            registry.Register<IGameService>(new RecordingService("b", log));

            registry.InitializeAll();
            registry.TickAll();
            registry.ShutdownAll();

            CollectionAssert.AreEqual(
                new[] { "init a", "init b", "tick a", "tick b", "shutdown b", "shutdown a" }, log);
        }

        [Test]
        public void TickAndShutdownDoNothingBeforeInitialize()
        {
            var log = new List<string>();
            var registry = new ServiceRegistry();
            registry.Register(new RecordingService("a", log));

            registry.TickAll();
            registry.ShutdownAll();

            Assert.IsEmpty(log);
        }

        [Test]
        public void ShutdownRunsOnlyOnce()
        {
            var log = new List<string>();
            var registry = new ServiceRegistry();
            registry.Register(new RecordingService("a", log));
            registry.InitializeAll();

            registry.ShutdownAll();
            registry.ShutdownAll();
            registry.TickAll();

            CollectionAssert.AreEqual(new[] { "init a", "shutdown a" }, log);
        }

        [Test]
        public void GetReturnsRegisteredService()
        {
            var registry = new ServiceRegistry();
            var service = new OtherService();
            registry.Register(service);

            Assert.AreSame(service, registry.Get<OtherService>());
        }

        [Test]
        public void GetThrowsForMissingService()
        {
            var registry = new ServiceRegistry();
            Assert.Throws<InvalidOperationException>(() => registry.Get<OtherService>());
            Assert.IsFalse(registry.TryGet(out OtherService _));
        }

        [Test]
        public void DuplicateRegistrationThrows()
        {
            var registry = new ServiceRegistry();
            registry.Register(new OtherService());
            Assert.Throws<InvalidOperationException>(() => registry.Register(new OtherService()));
        }

        [Test]
        public void RegisteringAfterInitializeThrows()
        {
            var registry = new ServiceRegistry();
            registry.InitializeAll();
            Assert.Throws<InvalidOperationException>(() => registry.Register(new OtherService()));
        }
    }
}
