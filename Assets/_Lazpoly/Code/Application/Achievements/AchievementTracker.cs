using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;

namespace Lazpoly.App.Achievements
{
    /// <summary>
    /// Watches a game's events and says which achievements the people at this machine earned.
    /// Only people count: moves of computer players and of other machines' players never unlock anything.
    /// Each achievement is reported at most once per tracker.
    /// </summary>
    public sealed class AchievementTracker
    {
        private readonly BoardDefinition _board;
        private readonly Func<int, bool> _isLocalPerson;
        private readonly Func<int, bool> _isComputer;
        private readonly bool _online;
        private readonly HashSet<string> _reported = new HashSet<string>();
        private TradeOffer _openOffer;

        /// <param name="isLocalPerson">Whether a person at this machine plays the player.</param>
        /// <param name="isComputer">Whether the computer plays the player (for <see cref="AchievementCatalog.BeatComputers"/>).</param>
        public AchievementTracker(BoardDefinition board, Func<int, bool> isLocalPerson, Func<int, bool> isComputer, bool online)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _isLocalPerson = isLocalPerson ?? throw new ArgumentNullException(nameof(isLocalPerson));
            _isComputer = isComputer ?? throw new ArgumentNullException(nameof(isComputer));
            _online = online;
        }

        /// <summary>
        /// Looks at one published batch of events; <paramref name="state"/> is the state after them.
        /// Returns the achievements earned by this batch (new ones only).
        /// </summary>
        public IReadOnlyList<string> Observe(IReadOnlyList<GameEvent> events, GameState state)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (state == null) throw new ArgumentNullException(nameof(state));

            var earned = new List<string>();
            foreach (GameEvent gameEvent in events)
            {
                switch (gameEvent)
                {
                    case BuildingLevelChangedEvent e when e.Level >= SquareDefinition.MaxBuildingLevel:
                        if (IsMine(state.GetOwner(e.SquareIndex))) Earn(AchievementCatalog.FirstHotel, earned);
                        break;
                    case PropertyOwnerChangedEvent e:
                        int group = _board[e.SquareIndex].GroupId;
                        if (group != 0 && IsMine(e.NewOwnerId) && GameRules.OwnsWholeGroup(_board, state, e.NewOwnerId, group))
                        {
                            Earn(AchievementCatalog.FullGroup, earned);
                        }

                        break;
                    case TradeProposedEvent e:
                        _openOffer = e.Offer;
                        break;
                    case TradeResolvedEvent e:
                        if (e.Outcome == TradeOutcome.Accepted && _openOffer != null &&
                            (IsMine(_openOffer.FromPlayerId) || IsMine(_openOffer.ToPlayerId)))
                        {
                            Earn(AchievementCatalog.TradeDone, earned);
                        }

                        _openOffer = null;
                        break;
                    case PlayerBankruptedEvent e:
                        if (IsMine(e.CreditorId)) Earn(AchievementCatalog.BankruptRival, earned);
                        break;
                    case GameEndedEvent e:
                        if (!IsMine(e.WinnerId)) break;
                        Earn(AchievementCatalog.FirstWin, earned);
                        if (_online) Earn(AchievementCatalog.OnlineWin, earned);
                        if (AnyComputerOpponent(state, e.WinnerId)) Earn(AchievementCatalog.BeatComputers, earned);
                        break;
                }
            }

            foreach (PlayerState player in state.Players)
            {
                if (!player.IsBankrupt && player.Money >= AchievementCatalog.TycoonMoney && IsMine(player.Id))
                {
                    Earn(AchievementCatalog.Tycoon, earned);
                }
            }

            return earned;
        }

        private bool IsMine(int playerId) => GameState.IsPlayerId(playerId) && _isLocalPerson(playerId);

        private bool AnyComputerOpponent(GameState state, int winnerId)
        {
            foreach (PlayerState player in state.Players)
            {
                if (player.Id != winnerId && _isComputer(player.Id)) return true;
            }

            return false;
        }

        private void Earn(string id, List<string> earned)
        {
            if (_reported.Add(id)) earned.Add(id);
        }
    }
}
