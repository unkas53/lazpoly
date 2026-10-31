using System;
using Lazpoly.App.Lobby;
using Lazpoly.Net.Transport;
using UnityEngine;

namespace Lazpoly.Presentation.Setup
{
    /// <summary>How this machine takes part in an online game that the Game scene is about to start.</summary>
    public sealed class OnlineLaunch
    {
        private OnlineLaunch(ITransport transport, OnlineSeatPlan hostPlan, ulong hostPeer)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            HostPlan = hostPlan;
            HostPeer = hostPeer;
        }

        public ITransport Transport { get; }

        /// <summary>Set when this machine hosts; null on a client.</summary>
        public OnlineSeatPlan HostPlan { get; }

        public bool IsHost => HostPlan != null;

        /// <summary>The host's peer id, on a client.</summary>
        public ulong HostPeer { get; }

        public static OnlineLaunch AsHost(ITransport transport, OnlineSeatPlan plan) =>
            new OnlineLaunch(transport, plan ?? throw new ArgumentNullException(nameof(plan)), 0UL);

        public static OnlineLaunch AsClient(ITransport transport, ulong hostPeer) => new OnlineLaunch(transport, null, hostPeer);
    }

    /// <summary>Hands the lobby's decision to the Game scene; taken once, like <see cref="LocalGameSetup"/>.</summary>
    public static class OnlineGameSetup
    {
        private static OnlineLaunch _launch;

        public static void Set(OnlineLaunch launch)
        {
            _launch = launch ?? throw new ArgumentNullException(nameof(launch));
        }

        /// <summary>Returns the pending online launch, or null for a local game.</summary>
        public static OnlineLaunch Take()
        {
            OnlineLaunch launch = _launch;
            _launch = null;
            return launch;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _launch = null;
        }
    }
}
