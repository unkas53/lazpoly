using System;
using System.Collections.Generic;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.Lobby
{
    /// <summary>
    /// Turns the lobby's member list into the game's seats on the host: the host plays first,
    /// the others follow in lobby order, and each remote peer is mapped to the player it controls.
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

        public static OnlineSeatPlan Create(ulong hostPeer, IReadOnlyList<LobbyMember> members)
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
            for (int i = 0; i < ordered.Count; i++)
            {
                int playerId = i + 1;
                string name = string.IsNullOrWhiteSpace(ordered[i].Name) ? $"Oyuncu {playerId}" : ordered[i].Name.Trim();
                seats[i] = new PlayerSeat(playerId, name);
                if (i > 0) remote.Add(ordered[i].Id, playerId);
            }

            return new OnlineSeatPlan(seats, 1, remote);
        }
    }
}
