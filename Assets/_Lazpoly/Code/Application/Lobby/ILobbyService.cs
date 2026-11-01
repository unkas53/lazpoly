using System;
using System.Collections.Generic;
using Lazpoly.Core.Rules;

namespace Lazpoly.App.Lobby
{
    public readonly struct LobbyMember
    {
        public LobbyMember(ulong id, string name, bool isReady = false)
        {
            Id = id;
            Name = name ?? string.Empty;
            IsReady = isReady;
        }

        /// <summary>Network peer id (the Steam id in production).</summary>
        public ulong Id { get; }

        public string Name { get; }

        /// <summary>The member has pressed "ready"; the owner counts as ready by hosting.</summary>
        public bool IsReady { get; }
    }

    /// <summary>A platform friend as the lobby menu shows them.</summary>
    public readonly struct LobbyFriend
    {
        public LobbyFriend(ulong id, string name, bool isOnline, ulong lobbyId)
        {
            Id = id;
            Name = name ?? string.Empty;
            IsOnline = isOnline;
            LobbyId = lobbyId;
        }

        public ulong Id { get; }
        public string Name { get; }
        public bool IsOnline { get; }

        /// <summary>The Lazpoly room the friend is in right now; 0 when none.</summary>
        public ulong LobbyId { get; }
    }

    public enum LobbyStatus
    {
        /// <summary>Not in a lobby.</summary>
        Idle = 0,
        Creating = 1,
        Joining = 2,
        InLobby = 3,
    }

    public enum LobbyError
    {
        /// <summary>The platform (Steam) is not running or not signed in.</summary>
        Unavailable = 1,
        CreateFailed = 2,
        JoinFailed = 3,
        LobbyFull = 4,
    }

    /// <summary>
    /// A room where players gather before a game: create, join through an invite, see who is in,
    /// and let the owner start the game. Implemented with Steam lobbies.
    /// </summary>
    public interface ILobbyService
    {
        bool IsAvailable { get; }

        /// <summary>This machine's peer id.</summary>
        ulong LocalId { get; }

        LobbyStatus Status { get; }

        /// <summary>The member who hosts the game; 0 when not in a lobby.</summary>
        ulong OwnerId { get; }

        bool IsOwner { get; }

        /// <summary>Members in a stable order, the same on every machine.</summary>
        IReadOnlyList<LobbyMember> Members { get; }

        /// <summary>True once the owner has started the game; members who (re)join go straight to it.</summary>
        bool IsGameStarted { get; }

        /// <summary>The room's id as a code a friend can type to join; 0 when not in a room.</summary>
        ulong RoomId { get; }

        /// <summary>The house rules the owner has chosen for the game, as every member sees them.</summary>
        TableRules Table { get; }

        /// <summary>Owner only: shares the house rules with the room.</summary>
        void SetTable(TableRules table);

        /// <summary>Marks this member ready (or not) for everyone to see.</summary>
        void SetReady(bool ready);

        /// <summary>Raised when status, members or the started flag change.</summary>
        event Action Changed;

        event Action<LobbyError> Failed;

        /// <summary>Raised when a member leaves or drops out of the lobby.</summary>
        event Action<ulong> MemberLeft;

        bool IsMember(ulong peer);

        void Create(int maxMembers);

        void Join(ulong lobbyId);

        /// <summary>Whether the platform's in-game overlay (and so its invite dialog) works in this process.</summary>
        bool IsOverlayAvailable { get; }

        /// <summary>Opens the platform's invite dialog for the current lobby. Needs the overlay.</summary>
        void InviteFriends();

        /// <summary>Sends an invite for the current lobby straight to one friend; works without the overlay.</summary>
        bool Invite(ulong friendId);

        /// <summary>The player's friends, online ones first.</summary>
        IReadOnlyList<LobbyFriend> GetFriends();

        /// <summary>Owner only: tells every member that the game has begun.</summary>
        void StartGame();

        void Leave();
    }
}
