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
                case GamePhase.AuctionInProgress:
                    return view.Legal(new PassBidCommand(PlayerId)) ? DecideBid(view) : null;
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
            return view.Me.Money - price >= reserve ? buy : (GameCommand)new DeclinePurchaseCommand(PlayerId);
        }

        private GameCommand DecideBid(View view)
        {
            var pass = new PassBidCommand(PlayerId);
            AuctionState auction = view.State.Auction;
            SquareDefinition square = view.Board[auction.SquareIndex];

            int bidPercent = Profile.BidPercent;
            if (view.CompletesGroup(auction.SquareIndex, PlayerId)) bidPercent += Profile.GroupBidBonusPercent;
            else if (Profile.BlockBidBonusPercent > 0 && view.CompletesAnyoneElsesGroup(auction.SquareIndex)) bidPercent += Profile.BlockBidBonusPercent;

            int ceiling = Math.Min(square.Price * bidPercent / 100, view.Me.Money - view.Reserve);
            int step = Math.Max(10, square.Price / 100 * 10);
            int bid = auction.HighestBid + step;
            if (bid > ceiling || view.MakesMistake()) return pass;

            var command = new PlaceBidCommand(PlayerId, bid);
            return view.Legal(command) ? command : (GameCommand)pass;
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

        /// <summary>Spends spare money between rolls: buys back mortgages, then builds evenly.</summary>
        private GameCommand Manage(View view)
        {
            int spare = view.Me.Money - view.Reserve * Profile.BuildReserveMultiplier;

            foreach (int square in view.OwnSquaresByValue())
            {
                var redeem = new RedeemCommand(PlayerId, square);
                if (view.Legal(redeem) && view.Me.Money - view.Rules.RedeemCost(view.Board[square].MortgageValue) >= view.Reserve * 2) return redeem;
            }

            foreach (int square in view.OwnSquaresByValue())
            {
                var build = new BuildCommand(PlayerId, square);
                if (view.Legal(build) && spare - view.Board[square].BuildingCost >= 0) return build;
            }

            return null;
        }

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

                int money = Math.Min(definition.Price * Profile.TradeOfferPercent / 100, view.Me.Money - view.Reserve);
                if (money < definition.Price) continue;

                var offer = new TradeOffer(PlayerId, owner, money, 0, Array.Empty<int>(), new[] { square });
                var command = new ProposeTradeCommand(offer);
                if (!view.Legal(command)) continue;

                LastProposalTurn = view.State.TurnNumber;
                return command;
            }

            return null;
        }

        /// <summary>Pays when it can; otherwise sells houses, then mortgages the least valuable property, then gives up.</summary>
        private GameCommand RaiseMoney(View view)
        {
            var pay = new PayDebtCommand(PlayerId);
            if (view.Legal(pay)) return pay;

            int bestSale = -1;
            for (int square = 0; square < view.Board.Count; square++)
            {
                if (!view.Legal(new SellBuildingCommand(PlayerId, square))) continue;
                if (bestSale < 0 || view.State.GetBuildingLevel(square) > view.State.GetBuildingLevel(bestSale)) bestSale = square;
            }

            if (bestSale >= 0) return new SellBuildingCommand(PlayerId, bestSale);

            IReadOnlyList<int> squares = view.OwnSquaresByValue();
            for (int i = squares.Count - 1; i >= 0; i--)
            {
                var mortgage = new MortgageCommand(PlayerId, squares[i]);
                if (view.Legal(mortgage)) return mortgage;
            }

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

            public bool CompletesAnyoneElsesGroup(int square)
            {
                foreach (PlayerState player in State.Players)
                {
                    if (player.Id != Me.Id && !player.IsBankrupt && CompletesGroup(square, player.Id)) return true;
                }

                return false;
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

            /// <summary>The player's properties, most valuable first (ties in board order).</summary>
            public IReadOnlyList<int> OwnSquaresByValue()
            {
                var squares = new List<int>();
                for (int i = 0; i < Board.Count; i++)
                {
                    if (State.GetOwner(i) == Me.Id) squares.Add(i);
                }

                squares.Sort((a, b) =>
                {
                    int byPrice = Board[b].Price.CompareTo(Board[a].Price);
                    return byPrice != 0 ? byPrice : a.CompareTo(b);
                });
                return squares;
            }

            private int Worth(int square)
            {
                SquareDefinition definition = Board[square];
                int value = definition.Price;
                if (State.IsMortgaged(square)) value -= Rules.RedeemCost(definition.MortgageValue);
                return Math.Max(value, 0);
            }

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
