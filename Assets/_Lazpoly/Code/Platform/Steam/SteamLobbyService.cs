using System;
using System.Collections.Generic;
using Lazpoly.App.Lobby;
using Lazpoly.Core.Rules;
using Lazpoly.Infra;
using Steamworks;

namespace Lazpoly.Platform.Steam
{
    /// <summary>
    /// <see cref="ILobbyService"/> on Steam friends-only lobbies. Joins automatically when the player
    /// accepts an invite in the Steam overlay or launches the game from one (+connect_lobby).
    /// </summary>
    public sealed class SteamLobbyService : ILobbyService, IGameService
    {
        private const string StartedKey = "lazpoly_started";
        private const uint ChatEnterSuccess = (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
        private const uint ChatEnterFull = (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseFull;
        private const uint MemberGone = (uint)(EChatMemberStateChange.k_EChatMemberStateChangeLeft |
                                               EChatMemberStateChange.k_EChatMemberStateChangeDisconnected |
                                               EChatMemberStateChange.k_EChatMemberStateChangeKicked |
                                               EChatMemberStateChange.k_EChatMemberStateChangeBanned);

        private readonly SteamService _steam;
        private readonly List<LobbyMember> _members = new List<LobbyMember>();
        private readonly HashSet<ulong> _memberIds = new HashSet<ulong>();

        private CallResult<LobbyCreated_t> _created;
        private Callback<LobbyEnter_t> _entered;
        private Callback<LobbyChatUpdate_t> _chatUpdate;
        private Callback<LobbyDataUpdate_t> _dataUpdate;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private CSteamID _lobby = CSteamID.Nil;
        private ulong _joiningLobby;

        public SteamLobbyService(SteamService steam)
        {
            _steam = steam ?? throw new ArgumentNullException(nameof(steam));
        }

        public bool IsAvailable => _steam.IsInitialized;
        public ulong LocalId => IsAvailable ? SteamUser.GetSteamID().m_SteamID : 0UL;
        public LobbyStatus Status { get; private set; }
        public ulong OwnerId => InLobby ? SteamMatchmaking.GetLobbyOwner(_lobby).m_SteamID : 0UL;
        public bool IsOwner => InLobby && OwnerId == LocalId;
        public IReadOnlyList<LobbyMember> Members => _members;
        public bool IsGameStarted => InLobby && SteamMatchmaking.GetLobbyData(_lobby, StartedKey) == "1";

        public event Action Changed;
        public event Action<LobbyError> Failed;
        public event Action<ulong> MemberLeft;

        private bool InLobby => Status == LobbyStatus.InLobby && IsAvailable;

        public void Initialize()
        {
            if (!IsAvailable) return;

            _created = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            _entered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _chatUpdate = Callback<LobbyChatUpdate_t>.Create(OnChatUpdate);
            _dataUpdate = Callback<LobbyDataUpdate_t>.Create(OnDataUpdate);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(request => Join(request.m_steamIDLobby.m_SteamID));

            ulong launchLobby = LobbyFromCommandLine(Environment.GetCommandLineArgs());
            if (launchLobby != 0) Join(launchLobby);
        }

        public void Tick()
        {
        }

        public void Shutdown()
        {
            Leave();
            _created?.Dispose();
            _entered?.Dispose();
            _chatUpdate?.Dispose();
            _dataUpdate?.Dispose();
            _joinRequested?.Dispose();
        }

        public bool IsMember(ulong peer) => _memberIds.Contains(peer);

        public void Create(int maxMembers)
        {
            if (!IsAvailable)
            {
                Failed?.Invoke(LobbyError.Unavailable);
                return;
            }

            if (Status != LobbyStatus.Idle) return;

            int max = Math.Max(RulesConfig.AbsoluteMinPlayers, Math.Min(RulesConfig.AbsoluteMaxPlayers, maxMembers));
            SetStatus(LobbyStatus.Creating);
            _created.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, max));
        }

        public void Join(ulong lobbyId)
        {
            if (!IsAvailable)
            {
                Failed?.Invoke(LobbyError.Unavailable);
                return;
            }

            if (lobbyId == 0 || (InLobby && _lobby.m_SteamID == lobbyId)) return;
            if (Status != LobbyStatus.Idle) Leave();

            _joiningLobby = lobbyId;
            SetStatus(LobbyStatus.Joining);
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
        }

        public bool IsOverlayAvailable => IsAvailable && SteamUtils.IsOverlayEnabled();

        public void InviteFriends()
        {
            if (InLobby) SteamFriends.ActivateGameOverlayInviteDialog(_lobby);
        }

        public bool Invite(ulong friendId)
        {
            return InLobby && SteamMatchmaking.InviteUserToLobby(_lobby, new CSteamID(friendId));
        }

        public IReadOnlyList<LobbyFriend> GetFriends()
        {
            var friends = new List<LobbyFriend>();
            if (!IsAvailable) return friends;

            uint appId = SteamUtils.GetAppID().m_AppId;
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                CSteamID friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                bool online = SteamFriends.GetFriendPersonaState(friend) != EPersonaState.k_EPersonaStateOffline;

                ulong lobbyId = 0;
                if (SteamFriends.GetFriendGamePlayed(friend, out FriendGameInfo_t game) &&
                    game.m_gameID.AppID().m_AppId == appId && game.m_steamIDLobby.IsValid())
                {
                    lobbyId = game.m_steamIDLobby.m_SteamID;
                }

                friends.Add(new LobbyFriend(friend.m_SteamID, SteamFriends.GetFriendPersonaName(friend), online, lobbyId));
            }

            friends.Sort((a, b) => a.IsOnline != b.IsOnline
                ? (a.IsOnline ? -1 : 1)
                : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return friends;
        }

        public void StartGame()
        {
            if (IsOwner) SteamMatchmaking.SetLobbyData(_lobby, StartedKey, "1");
        }

        public void Leave()
        {
            if (Status == LobbyStatus.Idle) return;
            if (IsAvailable && _lobby != CSteamID.Nil) SteamMatchmaking.LeaveLobby(_lobby);

            _lobby = CSteamID.Nil;
            _joiningLobby = 0;
            _members.Clear();
            _memberIds.Clear();
            SetStatus(LobbyStatus.Idle);
        }

        private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            // Already entered (Steam may report the entry first), or the player gave up meanwhile.
            if (Status != LobbyStatus.Creating) return;
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                SetStatus(LobbyStatus.Idle);
                Failed?.Invoke(LobbyError.CreateFailed);
                return;
            }

            // Steam enters the creator into the lobby; OnLobbyEntered completes the setup.
            _joiningLobby = result.m_ulSteamIDLobby;
            SetStatus(LobbyStatus.Joining);
        }

        private void OnLobbyEntered(LobbyEnter_t entered)
        {
            var lobby = new CSteamID(entered.m_ulSteamIDLobby);
            bool wanted = Status == LobbyStatus.Creating ||
                          (Status == LobbyStatus.Joining && entered.m_ulSteamIDLobby == _joiningLobby);
            if (!wanted)
            {
                // An answer to a request this service no longer wants (the player left meanwhile).
                if (entered.m_EChatRoomEnterResponse == ChatEnterSuccess) SteamMatchmaking.LeaveLobby(lobby);
                return;
            }

            _joiningLobby = 0;
            if (entered.m_EChatRoomEnterResponse != ChatEnterSuccess)
            {
                SetStatus(LobbyStatus.Idle);
                Failed?.Invoke(entered.m_EChatRoomEnterResponse == ChatEnterFull ? LobbyError.LobbyFull : LobbyError.JoinFailed);
                return;
            }

            _lobby = lobby;
            RefreshMembers();
            SetStatus(LobbyStatus.InLobby);
        }

        private void OnChatUpdate(LobbyChatUpdate_t update)
        {
            if (!InLobby || update.m_ulSteamIDLobby != _lobby.m_SteamID) return;

            RefreshMembers();
            if ((update.m_rgfChatMemberStateChange & MemberGone) != 0) MemberLeft?.Invoke(update.m_ulSteamIDUserChanged);
            Changed?.Invoke();
        }

        private void OnDataUpdate(LobbyDataUpdate_t update)
        {
            if (!InLobby || update.m_ulSteamIDLobby != _lobby.m_SteamID) return;
            RefreshMembers();
            Changed?.Invoke();
        }

        private void RefreshMembers()
        {
            _members.Clear();
            _memberIds.Clear();
            int count = SteamMatchmaking.GetNumLobbyMembers(_lobby);
            for (int i = 0; i < count; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(_lobby, i);
                if (!_memberIds.Add(member.m_SteamID)) continue;
                _members.Add(new LobbyMember(member.m_SteamID, SteamFriends.GetFriendPersonaName(member)));
            }
        }

        private void SetStatus(LobbyStatus status)
        {
            Status = status;
            Changed?.Invoke();
        }

        /// <summary>Steam starts the game with "+connect_lobby &lt;id&gt;" when it is launched from an invite.</summary>
        public static ulong LobbyFromCommandLine(IReadOnlyList<string> args)
        {
            if (args == null) return 0;
            for (int i = 0; i < args.Count - 1; i++)
            {
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong id)) return id;
            }

            return 0;
        }
    }
}
