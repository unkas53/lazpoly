using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.AI
{
    /// <summary>
    /// How a computer player uses the strategic cards in its hand (Faz 30), as the plan the owner approved describes: each
    /// turn it weighs its cards in a fixed order and plays the first that is worth it; attacks go at the leading rival and
    /// never at a shielded town. Pure functions of the state; every command returned passes <see cref="GameRules.Validate"/>.
    /// </summary>
    public static class AiCards
    {
        /// <summary>The order the hand is weighed in each turn: the first card worth playing is played.</summary>
        private static readonly CardKind[] Priority =
        {
            CardKind.Lawyer, CardKind.CupTransfer,
            CardKind.Demolish, CardKind.ForceSale, CardKind.IllegalFloor, CardKind.Strike, CardKind.Audit,
            CardKind.Shield, CardKind.LandDeal, CardKind.Renewal, CardKind.Fund, CardKind.Chauffeur, CardKind.Lookout,
        };

        /// <summary>How many squares ahead the lookout looks for an expensive rival town.</summary>
        public const int LookoutReach = 12;

        /// <summary>Rent above this share of the cash (per cent) is worth a rent shield.</summary>
        public const int ShieldRentPercent = 15;

        /// <summary>The card to play now, or null to keep the hand for later.</summary>
        public static PlayCardCommand Choose(BoardDefinition board, RulesConfig rules, GameState state, PlayerState me, int reserve)
        {
            if (state.CardPlayedThisTurn || me.Hand.Count == 0) return null;
            foreach (CardKind kind in Priority)
            {
                int card = GameRules.HeldCard(board, me, kind);
                if (card < 0) continue;
                PlayCardCommand play = Consider(board, rules, state, me, reserve, card);
                if (play != null && GameRules.Validate(board, rules, state, play) == RejectionReason.None) return play;
            }

            return null;
        }

        /// <summary>Whether to use the rent shield on the rent waiting: when it is a big bite of the cash, or more than the cash.</summary>
        public static GameCommand AnswerRent(BoardDefinition board, RulesConfig rules, GameState state, PlayerState me)
        {
            var pay = new PayRentCommand(me.Id);
            int card = GameRules.HeldCard(board, me, CardKind.RentShield);
            DebtState due = state.RentDue;
            if (card < 0 || due == null) return pay;
            bool big = due.Amount > me.Money || (long)due.Amount * 100 > (long)me.Money * ShieldRentPercent;
            var shield = new PlayCardCommand(me.Id, card);
            return big && GameRules.Validate(board, rules, state, shield) == RejectionReason.None ? shield : (GameCommand)pay;
        }

        /// <summary>With one card too many, lets the least useful one go.</summary>
        public static DiscardCardCommand Discard(BoardDefinition board, PlayerState me)
        {
            int worst = -1;
            int worstValue = int.MaxValue;
            foreach (int card in me.Hand)
            {
                int value = Value(board.Cards[card].Kind);
                if (value >= worstValue) continue;
                worst = card;
                worstValue = value;
            }

            return new DiscardCardCommand(me.Id, worst);
        }

        /// <summary>How much a card is worth keeping, for <see cref="Discard"/>: attacks and defences most, movement least.</summary>
        public static int Value(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Demolish:
                case CardKind.ForceSale: return 10;
                case CardKind.Shield:
                case CardKind.RentShield: return 9;
                case CardKind.CupTransfer: return 8;
                case CardKind.Strike:
                case CardKind.IllegalFloor: return 7;
                case CardKind.Audit:
                case CardKind.LandDeal: return 6;
                case CardKind.Renewal: return 5;
                case CardKind.Fund:
                case CardKind.Lawyer: return 4;
                case CardKind.Chauffeur: return 3;
                default: return 2;
            }
        }

        private static PlayCardCommand Consider(BoardDefinition board, RulesConfig rules, GameState state, PlayerState me, int reserve, int card)
        {
            CardDefinition definition = board.Cards[card];
            List<int> targets = GameRules.CardTargets(board, state, me, card);
            int leader = Leader(board, state, me.Id);
            switch (definition.Kind)
            {
                case CardKind.Lawyer:
                    return state.Phase == GamePhase.AwaitingJailDecision && me.Money < rules.StartingMoney / 2 ? Play(me, card, -1) : null;

                case CardKind.CupTransfer:
                {
                    bool rivalHasIt = state.CupSquare != GameState.NoCup && state.GetOwner(state.CupSquare) != me.Id;
                    if (!rivalHasIt && state.CupSquare != GameState.NoCup) return null;
                    return Best(targets, square => GameRules.RentWithoutCup(board, rules, state, square), me, card);
                }

                case CardKind.Demolish:
                    return Best(RivalTowns(state, targets, leader), square =>
                        Worth(board, state, square) + (BreaksGroup(board, state, square) ? board[square].Price * 4 : 0), me, card);

                case CardKind.ForceSale:
                    return Best(RivalTowns(state, targets, leader), square =>
                        GameRules.CalculateRent(board, rules, state, square) + (BreaksGroup(board, state, square) || square == state.CupSquare ? int.MaxValue / 4 : 0),
                        me, card);

                case CardKind.IllegalFloor:
                    return Best(RivalTowns(state, targets, leader), square => state.GetBuildingLevel(square) * 1000000 + board[square].BuildingCost, me, card);

                case CardKind.Strike:
                    return Best(RivalTowns(state, targets, leader), square => GameRules.CalculateRent(board, rules, state, square), me, card);

                case CardKind.Audit:
                    return targets.Contains(leader) ? Play(me, card, leader) : Best(targets, id => GameRules.NetWorth(board, state, id), me, card);

                case CardKind.Shield:
                    return Best(targets, square => GameRules.CalculateRent(board, rules, state, square), me, card);

                case CardKind.LandDeal:
                {
                    var affordable = targets.FindAll(square => me.Money - GameRules.LandDealCost(board, definition, square) >= reserve);
                    return Best(affordable, square => (CompletesGroup(board, state, square, me.Id) ? int.MaxValue / 4 : 0) - board[square].Price, me, card);
                }

                case CardKind.Renewal:
                {
                    var affordable = targets.FindAll(square => me.Money - GameRules.RenewalCost(board, definition, square) >= reserve);
                    return Best(affordable, square => GameRules.CalculateRent(board, rules, state, square), me, card);
                }

                case CardKind.Fund:
                {
                    var invest = new PlayCardCommand(me.Id, card, -1, 1);
                    bool rich = me.Money - definition.Cost >= rules.StartingMoney / 2;
                    return rich && GameRules.Validate(board, rules, state, invest) == RejectionReason.None ? invest : Play(me, card, -1);
                }

                case CardKind.Chauffeur:
                {
                    var wanted = targets.FindAll(square => board[square].Type == SquareType.Property && state.GetOwner(square) == GameState.NoOwner
                        && CompletesGroup(board, state, square, me.Id) && me.Money - board[square].Price >= reserve);
                    return Best(wanted, square => board[square].Price, me, card);
                }

                case CardKind.Lookout:
                    return DangerAhead(board, rules, state, me) && targets.Count > 0 ? Play(me, card, -1) : null;

                default:
                    return null;
            }
        }

        private static PlayCardCommand Play(PlayerState me, int card, int target) => new PlayCardCommand(me.Id, card, target);

        /// <summary>The target scoring highest, or null when there is none.</summary>
        private static PlayCardCommand Best(List<int> targets, Func<int, long> score, PlayerState me, int card)
        {
            int best = -1;
            long bestScore = long.MinValue;
            foreach (int target in targets)
            {
                long value = score(target);
                if (value <= bestScore) continue;
                best = target;
                bestScore = value;
            }

            return targets.Count == 0 ? null : Play(me, card, best);
        }

        /// <summary>The rival with the highest net worth.</summary>
        public static int Leader(BoardDefinition board, GameState state, int myId)
        {
            int leader = GameState.NoOwner;
            int best = int.MinValue;
            foreach (PlayerState player in state.Players)
            {
                if (player.Id == myId || player.IsBankrupt) continue;
                int worth = GameRules.NetWorth(board, state, player.Id);
                if (worth <= best) continue;
                leader = player.Id;
                best = worth;
            }

            return leader;
        }

        /// <summary>The leader's towns among the targets, shields left out; any rival's when the leader has none.</summary>
        private static List<int> RivalTowns(GameState state, List<int> targets, int leader)
        {
            var open = targets.FindAll(square => !state.HasShield(square));
            var leaders = open.FindAll(square => state.GetOwner(square) == leader);
            return leaders.Count > 0 ? leaders : open;
        }

        private static long Worth(BoardDefinition board, GameState state, int square) =>
            board[square].Price + (long)state.GetBuildingLevel(square) * board[square].BuildingCost;

        /// <summary>Whether the town is part of a colour group its owner holds whole.</summary>
        private static bool BreaksGroup(BoardDefinition board, GameState state, int square) =>
            GameRules.OwnsWholeGroup(board, state, state.GetOwner(square), board[square].GroupId);

        private static bool CompletesGroup(BoardDefinition board, GameState state, int square, int playerId)
        {
            int group = board[square].GroupId;
            if (group == 0) return false;
            foreach (int other in GameRules.GroupSquares(board, group))
            {
                if (other != square && state.GetOwner(other) != playerId) return false;
            }

            return true;
        }

        /// <summary>Whether a rival town within reach ahead charges a tenth of the cash or more.</summary>
        private static bool DangerAhead(BoardDefinition board, RulesConfig rules, GameState state, PlayerState me)
        {
            for (int step = 2; step <= LookoutReach; step++)
            {
                int square = (me.Position + step) % board.Count;
                int owner = state.GetOwner(square);
                if (owner == GameState.NoOwner || owner == me.Id) continue;
                if ((long)GameRules.CalculateRent(board, rules, state, square) * 10 >= me.Money) return true;
            }

            return false;
        }
    }
}
