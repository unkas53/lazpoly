using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Lazpoly.Infra;
using Lazpoly.Net.Transport;
using Steamworks;

namespace Lazpoly.Platform.Steam
{
    /// <summary>
    /// <see cref="ITransport"/> over SteamNetworkingMessages: reliable, ordered, relayed through
    /// Valve's network so no ports need opening. Only peers that pass the accept filter (members of
    /// the current lobby) may open a session with this machine.
    /// </summary>
    public sealed class SteamTransport : ITransport, IGameService
    {
        private const int Channel = 0;
        private const int MaxMessagesPerPoll = 64;
        private const int SendFlags = Constants.k_nSteamNetworkingSend_Reliable |
                                      Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession;

        private readonly SteamService _steam;
        private readonly Func<ulong, bool> _acceptPeer;
        private readonly IntPtr[] _incoming = new IntPtr[MaxMessagesPerPoll];
        private readonly List<KeyValuePair<ulong, byte[]>> _received = new List<KeyValuePair<ulong, byte[]>>();
        private readonly Queue<ulong> _dropped = new Queue<ulong>();

        private Callback<SteamNetworkingMessagesSessionRequest_t> _sessionRequest;
        private Callback<SteamNetworkingMessagesSessionFailed_t> _sessionFailed;

        /// <param name="acceptPeer">Decides whether a peer that contacts this machine is let in.</param>
        public SteamTransport(SteamService steam, Func<ulong, bool> acceptPeer)
        {
            _steam = steam ?? throw new ArgumentNullException(nameof(steam));
            _acceptPeer = acceptPeer ?? throw new ArgumentNullException(nameof(acceptPeer));
        }

        public ulong LocalPeer => _steam.IsInitialized ? SteamUser.GetSteamID().m_SteamID : 0UL;

        public event Action<ulong, byte[]> Received;
        public event Action<ulong> PeerDisconnected;

        public void Initialize()
        {
            if (!_steam.IsInitialized) return;

            SteamNetworkingUtils.InitRelayNetworkAccess();
            _sessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
            _sessionFailed = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnSessionFailed);
        }

        // Messages are read by the active game session through Poll, not every frame.
        public void Tick()
        {
        }

        public void Shutdown()
        {
            _sessionRequest?.Dispose();
            _sessionFailed?.Dispose();
            _sessionRequest = null;
            _sessionFailed = null;
        }

        public bool Send(ulong peer, byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (!_steam.IsInitialized || payload.Length == 0) return false;

            SteamNetworkingIdentity identity = Identity(peer);
            IntPtr buffer = Marshal.AllocHGlobal(payload.Length);
            try
            {
                Marshal.Copy(payload, 0, buffer, payload.Length);
                EResult result = SteamNetworkingMessages.SendMessageToUser(ref identity, buffer, (uint)payload.Length, SendFlags, Channel);
                return result == EResult.k_EResultOK;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void Poll()
        {
            while (_dropped.Count > 0) PeerDisconnected?.Invoke(_dropped.Dequeue());
            if (!_steam.IsInitialized) return;

            int count;
            do
            {
                count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, _incoming, MaxMessagesPerPoll);

                // Copy and release every native message first, so a throwing handler cannot leak them.
                _received.Clear();
                for (int i = 0; i < count; i++)
                {
                    SteamNetworkingMessage_t message = SteamNetworkingMessage_t.FromIntPtr(_incoming[i]);
                    var data = new byte[message.m_cbSize];
                    Marshal.Copy(message.m_pData, data, 0, message.m_cbSize);
                    _received.Add(new KeyValuePair<ulong, byte[]>(message.m_identityPeer.GetSteamID().m_SteamID, data));
                    SteamNetworkingMessage_t.Release(_incoming[i]);
                }

                foreach (KeyValuePair<ulong, byte[]> pair in _received) Received?.Invoke(pair.Key, pair.Value);
            }
            while (count == MaxMessagesPerPoll);
        }

        /// <summary>Closes the session with a peer that left, and reports it on the next poll.</summary>
        public void DropPeer(ulong peer)
        {
            if (_steam.IsInitialized)
            {
                SteamNetworkingIdentity identity = Identity(peer);
                SteamNetworkingMessages.CloseSessionWithUser(ref identity);
            }

            _dropped.Enqueue(peer);
        }

        private void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t request)
        {
            SteamNetworkingIdentity identity = request.m_identityRemote;
            if (_acceptPeer(identity.GetSteamID().m_SteamID))
            {
                SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
            }
        }

        private void OnSessionFailed(SteamNetworkingMessagesSessionFailed_t failure)
        {
            _dropped.Enqueue(failure.m_info.m_identityRemote.GetSteamID().m_SteamID);
        }

        private static SteamNetworkingIdentity Identity(ulong peer)
        {
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(peer));
            return identity;
        }
    }
}
