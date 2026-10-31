using System;
using System.Collections.Generic;

namespace Lazpoly.App.Lobby
{
    public readonly struct LobbyMember
    {
        public LobbyMember(ulong id, string name)
        {
            Id = id;
            Name = name ?? string.Empty;
        }

        /// <summary>Network peer id (the Steam id in production).</summary>
        public ulong Id { get; }

        public string Name { get; }
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

        /// <summary>Raised when status, members or the started flag change.</summary>
        event Action Changed;

        event Action<LobbyError> Failed;

        /// <summary>Raised when a member leaves or drops out of the lobby.</summary>
        event Action<ulong> MemberLeft;

        bool IsMember(ulong peer);

        void Create(int maxMembers);

        void Join(ulong lobbyId);

        /// <summary>Opens the platform's invite dialog for the current lobby.</summary>
        void InviteFriends();

        /// <summary>Owner only: tells every member that the game has begun.</summary>
        void StartGame();

        void Leave();
    }
}
