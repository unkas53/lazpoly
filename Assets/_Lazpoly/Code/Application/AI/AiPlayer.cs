using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.App.AI
{
    /// <summary>
    /// The decision maker for one computer player. It only reads the state and proposes a command;
    /// every command it returns has passed <see cref="GameRules.Validate"/>, and the session still
    /// sends it through the engine like any other player's.
    /// </summary>
    public sealed class AiPlayer
    {
        public const int NeverProposed = -1;

        /// <param name="lastProposalTurn">Turn of the last trade offer, when resuming a saved game.</param>
        public AiPlayer(int playerId, AiDifficulty difficulty, int lastProposalTurn = NeverProposed)
        {
            PlayerId = playerId;
            Profile = AiProfile.For(difficulty);
            LastProposalTurn = lastProposalTurn;
        }

        public int PlayerId { get; }

        /// <summary>The AI's only memory: the turn it last offered a trade (at most one offer per turn). Saved with the game.</summary>
        public int LastProposalTurn { get; private set; }
        public AiProfile Profile { get; }
        public AiDifficulty Difficulty => Profile.Difficulty;

        /// <summary>
        /// The command this player makes now, or null when the game is not waiting on it
        /// (for example while the other side considers its trade offer).
        /// </summary>
        public GameCommand Decide(BoardDefinition board, RulesConfig rules, GameState state, IRandomSource random)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (random == null) throw new ArgumentNullException(nameof(random));

            PlayerState me = state.FindPlayer(PlayerId);
            if (me == null || me.IsBankrupt || state.Phase == GamePhase.GameOver) return null;

            var view = new View(board, rules, state, me, Profile, random);
            switch (state.Phase)
            {
                case GamePhase.AwaitingTradeResponse:
                    return state.PendingTrade.ToPlayerId == PlayerId ? DecideTradeResponse(view) : null;
            }

            if (state.CurrentPlayer.Id != PlayerId) return null;
            switch (state.Phase)
            {
                case GamePhase.WaitingForRoll:
                    return Manage(view) ?? ProposeTrade(view) ?? new RollDiceCommand(PlayerId);
                case GamePhase.TurnEnd:
                    return Manage(view) ?? new EndTurnCommand(PlayerId);
                case GamePhase.AwaitingBuyDecision:
                    return DecidePurchase(view);
                case GamePhase.AwaitingDebtPayment:
                    return RaiseMoney(view);
                case GamePhase.AwaitingTravelDecision:
                    return DecideTravel(view);
                case GamePhase.AwaitingDiceChoice:
                    return ChooseDice(view);
                case GamePhase.AwaitingJailDecision:
                    return DecideJail(view);
                case GamePhase.AwaitingCupChoice:
                    return DecideCup(view);
                default:
                    return null;
            }
        }

        private GameCommand DecidePurchase(View view)
        {
            var buy = new BuyPropertyCommand(PlayerId);
            int square = view.Me.Position;
            int price = view.Board[square].Price;
            if (!view.Legal(buy) || view.MakesMistake()) return new DeclinePurchaseCommand(PlayerId);

            // A square that completes a group is worth dipping into the reserve for.
            int reserve = view.CompletesGroup(square, PlayerId) ? view.Reserve / 3 : view.Reserve;
            if (view.Me.Money - price < reserve) return new DeclinePurchaseCommand(PlayerId);

            // The houses it would put up afterwards go up with the purchase, in one step and one payment, like a person's
            // pick on the city card.
            int cost = view.Board[square].BuildingCost;
            int spare = view.Me.Money - price - view.Reserve * Profile.BuildReserveMultiplier;
            int levels = cost > 0 && spare > 0 ? Math.Min(GameRules.BuildableAfterPurchase(view.Board, view.Me, square), spare / cost) : 0;
            var withHouses = new BuyPropertyCommand(PlayerId, levels);
            return levels > 0 && view.Legal(withHouses) ? withHouses : buy;
        }

        /// <summary>Takes a ride to a free property it can afford (one completing a group first) or to a rich pot.</summary>
        private GameCommand DecideTravel(View view)
        {
            var stay = new DeclineTravelCommand(PlayerId);
            int fee = view.Board[view.Me.Position].Fee;
            int best = -1;
            int bestScore = 0;
            for (int square = 0; square < view.Board.Count; square++)
            {
                SquareDefinition definition = view.Board[square];
                int score = 0;
                if (definition.Type == SquareType.Property && view.State.GetOwner(square) == GameState.NoOwner &&
                    view.Me.Money - fee - definition.Price >= view.Reserve)
                {
                    score = definition.Price + (view.CompletesGroup(square, PlayerId) ? CompletesGroupBonus : 0);
                }
                else if (definition.Type == SquareType.Pot && view.State.Pot > fee * 2)
                {
                    score = view.State.Pot;
                }

                if (score <= bestScore || !view.Legal(new TravelCommand(PlayerId, square))) continue;
                best = square;
                bestScore = score;
            }

            if (best < 0 || view.MakesMistake()) return stay;
            return new TravelCommand(PlayerId, best);
        }

        /// <summary>
        /// Brings the cup to its town with the highest rent (which may already hold it: the raise grows) when the fee leaves its
        /// reserve whole; otherwise passes.
        /// </summary>
        private GameCommand DecideCup(View view)
        {
            var pass = new DeclineCupCommand(PlayerId);
            int fee = view.Board[view.Me.Position].Fee;
            if (view.Me.Money - fee < view.Reserve || view.MakesMistake()) return pass;
            int best = -1;
            int bestRent = 0;
            for (int square = 0; square < view.Board.Count; square++)
            {
                if (view.State.GetOwner(square) != PlayerId || !GameRules.CanHoldCup(view.Board, square)) continue;
                int rent = GameRules.RentWithoutCup(view.Board, view.Rules, view.State, square);
                if (best >= 0 && rent <= bestRent) continue;
                best = square;
                bestRent = rent;
            }

            var host = new HostCupCommand(PlayerId, best);
            return best >= 0 && view.Legal(host) ? host : (GameCommand)pass;
        }

        /// <summary>Pays its way out of jail when the fee leaves its reserve whole; otherwise rolls for a double.</summary>
        private GameCommand DecideJail(View view)
        {
            var pay = new PayJailFeeCommand(PlayerId);
            bool affordable = view.Legal(pay) && view.Me.Money - GameRules.JailFee(view.Board) >= view.Reserve;
            return affordable && !view.MakesMistake() ? pay : (GameCommand)new RollInJailCommand(PlayerId);
        }

        /// <summary>Of the two rolls a lookout offers, keeps the one whose landing looks better.</summary>
        private GameCommand ChooseDice(View view)
        {
            int[] first = GameRules.DiceOption(view.State, 0);
            int[] second = GameRules.DiceOption(view.State, 1);
            int firstScore = view.LandingScore(first[0] + first[1]);
            int secondScore = view.LandingScore(second[0] + second[1]);
            return new ChooseDiceCommand(PlayerId, secondScore > firstScore ? 1 : 0);
        }

        private GameCommand DecideTradeResponse(View view)
        {
            var reject = new RejectTradeCommand(PlayerId);
            var accept = new AcceptTradeCommand(PlayerId);
            if (!view.Legal(accept)) return reject;

            TradeOffer offer = view.State.PendingTrade;
            int received = offer.GiveMoney;
            int given = offer.TakeMoney;
            foreach (int square in offer.GiveSquares) received += view.ValueIfGained(square, PlayerId, offer.GiveSquares);
            foreach (int square in offer.TakeSquares) given += view.ValueIfLost(square, PlayerId, offer.FromPlayerId, offer.TakeSquares);

            // Never accept a deal that leaves too little cash to survive the next rent.
            int moneyAfter = view.Me.Money + offer.GiveMoney - offer.TakeMoney;
            if (moneyAfter < view.Reserve / 2) return reject;

            bool good = (long)received * 100 >= (long)given * (100 + Profile.TradeAcceptMarginPercent) && received > 0;
            if (view.MakesMistake()) good = !good;
            return good ? accept : (GameCommand)reject;
        }

        /// <summary>Spends spare money between rolls on the town the player stands on.</summary>
        private GameCommand Manage(View view)
        {
            int spare = view.Me.Money - view.Reserve * Profile.BuildReserveMultiplier;
            return Build(view, spare);
        }

        /// <summary>
        /// Building only happens on the town the player stands on, so there is one candidate: as many levels as the visit
        /// allows and the spare money pays for, put up at once in one payment like a person's pick on the card. A player
        /// prone to mistakes sometimes leaves the chance unused and moves on.
        /// </summary>
        private GameCommand Build(View view, int spare)
        {
            int square = view.Me.Position;
            int cost = view.Board[square].BuildingCost;
            int levels = cost > 0 ? Math.Min(GameRules.BuildableNow(view.Board, view.State, view.Me, square), spare / cost) : 0;
            if (levels < 1) return null;
            var build = new BuildCommand(PlayerId, square, levels);
            if (!view.Legal(build)) return null;
            return view.MakesMistake() ? null : build;
        }

        /// <summary>Rounds after which offers grow each round, so a table where nobody sells never plays on forever.</summary>
        /// <summary>How much a ride's target that completes a colour group outscores any price (prices run to half a million).</summary>
        public const int CompletesGroupBonus = 100000000;

        public const int StalemateRound = 12;
        public const int StalemateBonusPercent = 15;

        private int TradeOfferPercent(int round) => Profile.TradeOfferPercent + StalemateBonusPercent * Math.Max(0, round - StalemateRound);

        /// <summary>Offers money for the one property that would complete a group, at most once per turn.</summary>
        private GameCommand ProposeTrade(View view)
        {
            if (!Profile.ProposesTrades || LastProposalTurn == view.State.TurnNumber) return null;

            for (int square = 0; square < view.Board.Count; square++)
            {
                SquareDefinition definition = view.Board[square];
                int owner = view.State.GetOwner(square);
                if (definition.Type != SquareType.Property || owner == GameState.NoOwner || owner == PlayerId) continue;
                if (!view.CompletesGroup(square, PlayerId)) continue;

                int money = Math.Min(definition.Price * TradeOfferPercent(view.State.Round) / 100, view.Me.Money - view.Reserve);
                if (money < definition.Price) continue;

                var offer = new TradeOffer(PlayerId, owner, money, 0, Array.Empty<int>(), new[] { square });
                var command = new ProposeTradeCommand(offer);
                if (!view.Legal(command)) continue;

                LastProposalTurn = view.State.TurnNumber;
                return command;
            }

            return null;
        }

        /// <summary>
        /// Pays when it can; otherwise sells whole towns, buildings and all, the way a person's sale and the debt screen's
        /// automatic sale do (the owner's wish, Faz 27: never house by house), one town per decision; gives up when even
        /// selling everything would not cover the debt.
        /// </summary>
        private GameCommand RaiseMoney(View view)
        {
            var pay = new PayDebtCommand(PlayerId);
            if (view.Legal(pay)) return pay;

            IReadOnlyList<GameCommand> plan = DebtSettlement.Plan(view.Board, view.Rules, view.State, PlayerId);
            if (plan.Count > 0 && view.Legal(plan[0])) return plan[0];
            return new DeclareBankruptcyCommand(PlayerId);
        }

        /// <summary>Read-only helpers over the state, from this player's point of view.</summary>
        private sealed class View
        {
            private readonly IRandomSource _random;

            public View(BoardDefinition board, RulesConfig rules, GameState state, PlayerState me, AiProfile profile, IRandomSource random)
            {
                Board = board;
                Rules = rules;
                State = state;
                Me = me;
                Profile = profile;
                Reserve = profile.CashReserve(rules.StartingMoney);
                _random = random;
            }

            public BoardDefinition Board { get; }
            public RulesConfig Rules { get; }
            public GameState State { get; }
            public PlayerState Me { get; }
            public AiProfile Profile { get; }
            public int Reserve { get; }

            public bool Legal(GameCommand command) => GameRules.Validate(Board, Rules, State, command) == RejectionReason.None;

            public bool MakesMistake() => Profile.MistakePercent > 0 && _random.NextInt(0, 100) < Profile.MistakePercent;

            /// <summary>Whether owning this square would give the player its whole group (other squares already theirs).</summary>
            public bool CompletesGroup(int square, int playerId)
            {
                int group = Board[square].GroupId;
                if (group == 0) return false;
                foreach (int other in GameRules.GroupSquares(Board, group))
                {
                    if (other != square && State.GetOwner(other) != playerId) return false;
                }

                return true;
            }

            /// <summary>Rough value of moving <paramref name="steps"/> squares: good squares and the start bonus count up, rent and taxes down.</summary>
            public int LandingScore(int steps)
            {
                int total = Me.Position + steps;
                int square = total % Board.Count;
                int score = total >= Board.Count ? Rules.PassStartBonus : 0;
                SquareDefinition definition = Board[square];
                switch (definition.Type)
                {
                    case SquareType.Property:
                        int owner = State.GetOwner(square);
                        if (owner == GameState.NoOwner)
                        {
                            if (Me.Money - definition.Price >= Reserve) score += definition.Price / 2 + (CompletesGroup(square, Me.Id) ? definition.Price : 0);
                        }
                        else if (owner != Me.Id)
                        {
                            score -= GameRules.CalculateRent(Board, Rules, State, square);
                        }

                        break;
                    case SquareType.Tax:
                        score -= GameRules.TaxDue(Board, State, square, Me.Id);
                        break;
                    case SquareType.Pot:
                        score += State.Pot;
                        break;
                    case SquareType.Jail:
                        // Turns lost in jail, or the fee to leave: worse than most rents.
                        score -= definition.Fee * (Rules.JailTurns + 1);
                        break;
                }

                return score;
            }

            /// <summary>What a square is worth to a player who receives it along with <paramref name="bundle"/>.</summary>
            public int ValueIfGained(int square, int playerId, IReadOnlyList<int> bundle)
            {
                int value = Worth(square);
                if (CompletesGroupWith(square, playerId, bundle)) value *= 2;
                return value;
            }

            /// <summary>What a player loses by handing a square (and <paramref name="bundle"/>) to <paramref name="receiverId"/>.</summary>
            public int ValueIfLost(int square, int playerId, int receiverId, IReadOnlyList<int> bundle)
            {
                int value = Worth(square);
                int group = Board[square].GroupId;
                if (group != 0 && GameRules.OwnsWholeGroup(Board, State, playerId, group)) value *= 2;

                // Handing someone their missing piece makes them dangerous; a strong player charges for it.
                if (Profile.Difficulty == AiDifficulty.Hard && CompletesGroupWith(square, receiverId, bundle)) value += Board[square].Price;
                return value;
            }

            private int Worth(int square) => Board[square].Price;

            private bool CompletesGroupWith(int square, int playerId, IReadOnlyList<int> bundle)
            {
                int group = Board[square].GroupId;
                if (group == 0) return false;
                foreach (int other in GameRules.GroupSquares(Board, group))
                {
                    if (other == square || State.GetOwner(other) == playerId) continue;
                    bool comesAlong = false;
                    foreach (int extra in bundle)
                    {
                        if (extra == other) comesAlong = true;
                    }

                    if (!comesAlong) return false;
                }

                return true;
            }
        }
    }
}
