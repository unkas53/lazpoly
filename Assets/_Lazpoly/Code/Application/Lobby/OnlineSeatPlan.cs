using System;
using System.Collections.Generic;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.Lobby
{
    /// <summary>
    /// Turns the lobby's member list into the game's seats on the host: the host plays first,
    /// the others follow in lobby order, and each remote peer is mapped to the player it controls.
    /// Each seat gets the character its member picked, or a free one when that is taken (<see cref="FigurePicks"/>).
    /// </summary>
    public sealed class OnlineSeatPlan
    {
        private OnlineSeatPlan(IReadOnlyList<PlayerSeat> seats, int hostPlayerId, IReadOnlyDictionary<ulong, int> remotePlayers)
        {
            Seats = seats;
            HostPlayerId = hostPlayerId;
            RemotePlayers = remotePlayers;
        }

        public IReadOnlyList<PlayerSeat> Seats { get; }
        public int HostPlayerId { get; }
        public IReadOnlyDictionary<ulong, int> RemotePlayers { get; }

        /// <param name="figures">The characters there are; null leaves the seats without one.</param>
        public static OnlineSeatPlan Create(ulong hostPeer, IReadOnlyList<LobbyMember> members, IReadOnlyList<string> figures = null)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));

            var ordered = new List<LobbyMember>(members.Count);
            var seen = new HashSet<ulong>();
            foreach (LobbyMember member in members)
            {
                if (member.Id == hostPeer && seen.Add(member.Id)) ordered.Insert(0, member);
            }

            if (ordered.Count == 0) throw new ArgumentException("The host is not a member of the lobby.", nameof(members));

            foreach (LobbyMember member in members)
            {
                if (seen.Add(member.Id)) ordered.Add(member);
            }

            if (ordered.Count < RulesConfig.AbsoluteMinPlayers || ordered.Count > RulesConfig.AbsoluteMaxPlayers)
            {
                throw new ArgumentException(
                    $"An online game needs {RulesConfig.AbsoluteMinPlayers}-{RulesConfig.AbsoluteMaxPlayers} players, the lobby has {ordered.Count}.",
                    nameof(members));
            }

            var seats = new PlayerSeat[ordered.Count];
            var remote = new Dictionary<ulong, int>();
            IReadOnlyList<string> picks = figures != null ? FigurePicks.Resolve(Picks(ordered), figures) : null;
            for (int i = 0; i < ordered.Count; i++)
            {
                int playerId = i + 1;
                string name = string.IsNullOrWhiteSpace(ordered[i].Name) ? $"Oyuncu {playerId}" : ordered[i].Name.Trim();
                seats[i] = new PlayerSeat(playerId, name, picks?[i]);
                if (i > 0) remote.Add(ordered[i].Id, playerId);
            }

            return new OnlineSeatPlan(seats, 1, remote);
        }

        /// <summary>The members in the order the game seats them: the host first, then lobby order.</summary>
        public static IReadOnlyList<LobbyMember> SeatOrder(ulong hostPeer, IReadOnlyList<LobbyMember> members)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            var order = new List<LobbyMember>(members.Count);
            foreach (LobbyMember member in members) if (member.Id == hostPeer) order.Add(member);
            foreach (LobbyMember member in members) if (member.Id != hostPeer) order.Add(member);
            return order;
        }

        /// <summary>Each member's pick, in the order given.</summary>
        public static IReadOnlyList<string> Picks(IReadOnlyList<LobbyMember> members)
        {
            var picks = new string[members.Count];
            for (int i = 0; i < members.Count; i++) picks[i] = members[i].Figure;
            return picks;
        }
    }
}
