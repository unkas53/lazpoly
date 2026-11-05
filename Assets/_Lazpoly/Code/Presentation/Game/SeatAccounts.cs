using System;
using System.Collections.Generic;
using Lazpoly.App.Lobby;
using Lazpoly.Core.State;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Which platform (Steam) account plays which seat, so a real person's card can show their own profile picture.
    /// Computers have none, and neither do the other people at a shared computer: only the account signed in here
    /// is known to be one of them.
    /// </summary>
    public static class SeatAccounts
    {
        private static readonly IReadOnlyDictionary<int, ulong> None = new Dictionary<int, ulong>();

        /// <summary>A game on this computer: the person who set it up (<paramref name="chooser"/>) is the account signed in here.</summary>
        public static IReadOnlyDictionary<int, ulong> Local(int chooser, ulong localAccount)
        {
            if (chooser == GameState.NoOwner || localAccount == 0) return None;
            return new Dictionary<int, ulong> { [chooser] = localAccount };
        }

        /// <summary>
        /// An online game: the seats as the host laid them out from the lobby (<see cref="OnlineSeatPlan"/>), the
        /// same on every computer. A seat whose player is not named as the lobby member was (the room changed
        /// before the game began) gets no account rather than someone else's picture.
        /// </summary>
        public static IReadOnlyDictionary<int, ulong> Online(ulong hostAccount, IReadOnlyList<LobbyMember> members, IReadOnlyList<PlayerState> players)
        {
            if (hostAccount == 0 || members == null || players == null) return None;
            OnlineSeatPlan plan;
            try
            {
                plan = OnlineSeatPlan.Create(hostAccount, members);
            }
            catch (ArgumentException)
            {
                return None;
            }

            var accounts = new Dictionary<int, ulong> { [plan.HostPlayerId] = hostAccount };
            foreach (KeyValuePair<ulong, int> remote in plan.RemotePlayers) accounts[remote.Value] = remote.Key;

            var result = new Dictionary<int, ulong>();
            foreach (PlayerSeat seat in plan.Seats)
            {
                if (!accounts.TryGetValue(seat.Id, out ulong account)) continue;
                foreach (PlayerState player in players)
                {
                    if (player.Id == seat.Id && player.Name == seat.Name) result[player.Id] = account;
                }
            }

            return result;
        }
    }
}
