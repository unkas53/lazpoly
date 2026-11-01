using System;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;

namespace Lazpoly.Net.Messages
{
    /// <summary>
    /// Turns messages into bytes and back. Decoding never trusts the sender: malformed or
    /// unknown data throws <see cref="FormatException"/>, which sessions treat as a dropped message.
    /// </summary>
    public static class MessageCodec
    {
        public const int ProtocolVersion = 6;

        private enum CommandTag : byte
        {
            Roll = 1, Buy = 2, Decline = 3, EndTurn = 4,
            Build = 5, SellBuilding = 6, SellProperty = 7,
            Bid = 9, PassBid = 10,
            ProposeTrade = 11, AcceptTrade = 12, RejectTrade = 13, CancelTrade = 14,
            PayDebt = 15, Bankruptcy = 16,
            Travel = 17, DeclineTravel = 18, ChooseDice = 19,
        }

        private enum EventTag : byte
        {
            TurnStarted = 1, DiceRolled = 2, PlayerMoved = 3, MoneyChanged = 4,
            OwnerChanged = 5, Bankrupted = 6, PhaseChanged = 7, GameEnded = 8,
            BuildingLevel = 9, AuctionStarted = 11, BidPlaced = 12, BidPassed = 13, AuctionEnded = 14,
            TradeProposed = 15, TradeResolved = 16, DebtIncurred = 17, DebtCleared = 18,
            PotChanged = 19, CardDrawn = 20, Lookout = 21, DiceOptions = 22, TimedOut = 23,
        }

        public static byte[] Encode(NetMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var w = new NetWriter();
            w.WriteByte((byte)message.Type);
            switch (message)
            {
                case HelloMessage m:
                    w.WriteInt(m.ProtocolVersion);
                    w.WriteULong(m.BoardChecksum);
                    break;
                case GameStartMessage m:
                    w.WriteInt(m.ProtocolVersion);
                    w.WriteULong(m.BoardChecksum);
                    w.WriteInt(m.YourPlayerId);
                    w.WriteInt(m.Sequence);
                    WriteTable(w, m.Table ?? TableRules.Default);
                    WriteSnapshot(w, m.Snapshot);
                    break;
                case RefusedMessage m:
                    w.WriteByte((byte)m.Reason);
                    break;
                case CommandMessage m:
                    WriteCommand(w, m.Command);
                    break;
                case EventsMessage m:
                    w.WriteInt(m.Sequence);
                    w.WriteULong(m.StateHash);
                    w.WriteInt(m.Events.Count);
                    foreach (GameEvent gameEvent in m.Events) WriteEvent(w, gameEvent);
                    break;
                case RejectedMessage m:
                    w.WriteInt((int)m.Reason);
                    break;
                case SnapshotRequestMessage _:
                    break;
                default:
                    throw new ArgumentException($"Cannot encode {message.GetType().Name}.", nameof(message));
            }

            return w.ToArray();
        }

        public static NetMessage Decode(byte[] data)
        {
            var r = new NetReader(data);
            NetMessage message;
            var type = (MessageType)r.ReadByte();
            switch (type)
            {
                case MessageType.Hello:
                    message = new HelloMessage { ProtocolVersion = r.ReadInt(), BoardChecksum = r.ReadULong() };
                    break;
                case MessageType.GameStart:
                    message = new GameStartMessage
                    {
                        ProtocolVersion = r.ReadInt(),
                        BoardChecksum = r.ReadULong(),
                        YourPlayerId = r.ReadInt(),
                        Sequence = r.ReadInt(),
                        Table = ReadTable(r),
                        Snapshot = ReadSnapshot(r),
                    };
                    break;
                case MessageType.Refused:
                    byte refusal = r.ReadByte();
                    if (!Enum.IsDefined(typeof(RefusalReason), refusal)) throw new FormatException($"Invalid refusal reason {refusal}.");
                    message = new RefusedMessage { Reason = (RefusalReason)refusal };
                    break;
                case MessageType.Command:
                    message = new CommandMessage { Command = ReadCommand(r) };
                    break;
                case MessageType.Events:
                    var events = new EventsMessage { Sequence = r.ReadInt(), StateHash = r.ReadULong() };
                    int count = r.ReadCount();
                    for (int i = 0; i < count; i++) events.Events.Add(ReadEvent(r));
                    message = events;
                    break;
                case MessageType.Rejected:
                    message = new RejectedMessage { Reason = ReadEnum<RejectionReason>(r) };
                    break;
                case MessageType.SnapshotRequest:
                    message = new SnapshotRequestMessage();
                    break;
                default:
                    throw new FormatException($"Unknown message type {(byte)type}.");
            }

            if (!r.AtEnd) throw new FormatException("Message has trailing bytes.");
            return message;
        }

        private static void WriteCommand(NetWriter w, GameCommand command)
        {
            switch (command)
            {
                case RollDiceCommand _: w.WriteByte((byte)CommandTag.Roll); break;
                case BuyPropertyCommand _: w.WriteByte((byte)CommandTag.Buy); break;
                case DeclinePurchaseCommand _: w.WriteByte((byte)CommandTag.Decline); break;
                case EndTurnCommand _: w.WriteByte((byte)CommandTag.EndTurn); break;
                case BuildCommand _: w.WriteByte((byte)CommandTag.Build); break;
                case SellBuildingCommand _: w.WriteByte((byte)CommandTag.SellBuilding); break;
                case SellPropertyCommand _: w.WriteByte((byte)CommandTag.SellProperty); break;
                case PlaceBidCommand _: w.WriteByte((byte)CommandTag.Bid); break;
                case PassBidCommand _: w.WriteByte((byte)CommandTag.PassBid); break;
                case ProposeTradeCommand c:
                    if (c.Offer == null) throw new ArgumentException("A trade command needs an offer.");
                    w.WriteByte((byte)CommandTag.ProposeTrade);
                    break;
                case AcceptTradeCommand _: w.WriteByte((byte)CommandTag.AcceptTrade); break;
                case RejectTradeCommand _: w.WriteByte((byte)CommandTag.RejectTrade); break;
                case CancelTradeCommand _: w.WriteByte((byte)CommandTag.CancelTrade); break;
                case PayDebtCommand _: w.WriteByte((byte)CommandTag.PayDebt); break;
                case DeclareBankruptcyCommand _: w.WriteByte((byte)CommandTag.Bankruptcy); break;
                case TravelCommand _: w.WriteByte((byte)CommandTag.Travel); break;
                case DeclineTravelCommand _: w.WriteByte((byte)CommandTag.DeclineTravel); break;
                case ChooseDiceCommand _: w.WriteByte((byte)CommandTag.ChooseDice); break;
                default: throw new ArgumentException($"Cannot encode command {command?.GetType().Name}.");
            }

            w.WriteInt(command.PlayerId);
            switch (command)
            {
                case SquareCommand c: w.WriteInt(c.SquareIndex); break;
                case PlaceBidCommand c: w.WriteInt(c.Amount); break;
                case ChooseDiceCommand c: w.WriteInt(c.Option); break;
                case ProposeTradeCommand c: WriteOffer(w, c.Offer); break;
            }
        }

        private static void WriteOffer(NetWriter w, TradeOffer offer)
        {
            w.WriteInt(offer.FromPlayerId);
            w.WriteInt(offer.ToPlayerId);
            w.WriteInt(offer.GiveMoney);
            w.WriteInt(offer.TakeMoney);
            w.WriteInt(offer.GiveSquares.Count);
            foreach (int square in offer.GiveSquares) w.WriteInt(square);
            w.WriteInt(offer.TakeSquares.Count);
            foreach (int square in offer.TakeSquares) w.WriteInt(square);
        }

        private static TradeOffer ReadOffer(NetReader r)
        {
            int from = r.ReadInt();
            int to = r.ReadInt();
            int give = r.ReadInt();
            int take = r.ReadInt();
            int[] giveSquares = ReadInts(r);
            int[] takeSquares = ReadInts(r);
            return new TradeOffer(from, to, give, take, giveSquares, takeSquares);
        }

        private static int[] ReadInts(NetReader r)
        {
            var values = new int[r.ReadCount()];
            for (int i = 0; i < values.Length; i++) values[i] = r.ReadInt();
            return values;
        }

        private static GameCommand ReadCommand(NetReader r)
        {
            var tag = (CommandTag)r.ReadByte();
            int playerId = r.ReadInt();
            switch (tag)
            {
                case CommandTag.Roll: return new RollDiceCommand(playerId);
                case CommandTag.Buy: return new BuyPropertyCommand(playerId);
                case CommandTag.Decline: return new DeclinePurchaseCommand(playerId);
                case CommandTag.EndTurn: return new EndTurnCommand(playerId);
                case CommandTag.Build: return new BuildCommand(playerId, r.ReadInt());
                case CommandTag.SellBuilding: return new SellBuildingCommand(playerId, r.ReadInt());
                case CommandTag.SellProperty: return new SellPropertyCommand(playerId, r.ReadInt());
                case CommandTag.Bid: return new PlaceBidCommand(playerId, r.ReadInt());
                case CommandTag.PassBid: return new PassBidCommand(playerId);
                case CommandTag.ProposeTrade:
                    TradeOffer offer = ReadOffer(r);
                    if (offer.FromPlayerId != playerId) throw new FormatException("Trade offer is not from the commanding player.");
                    return new ProposeTradeCommand(offer);
                case CommandTag.AcceptTrade: return new AcceptTradeCommand(playerId);
                case CommandTag.RejectTrade: return new RejectTradeCommand(playerId);
                case CommandTag.CancelTrade: return new CancelTradeCommand(playerId);
                case CommandTag.PayDebt: return new PayDebtCommand(playerId);
                case CommandTag.Bankruptcy: return new DeclareBankruptcyCommand(playerId);
                case CommandTag.Travel: return new TravelCommand(playerId, r.ReadInt());
                case CommandTag.DeclineTravel: return new DeclineTravelCommand(playerId);
                case CommandTag.ChooseDice: return new ChooseDiceCommand(playerId, r.ReadInt());
                default: throw new FormatException($"Unknown command tag {(byte)tag}.");
            }
        }

        private static void WriteEvent(NetWriter w, GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case TurnStartedEvent e:
                    w.WriteByte((byte)EventTag.TurnStarted); w.WriteInt(e.PlayerId);
                    break;
                case DiceRolledEvent e:
                    w.WriteByte((byte)EventTag.DiceRolled); w.WriteInt(e.PlayerId); w.WriteInt(e.Die1); w.WriteInt(e.Die2);
                    break;
                case PlayerMovedEvent e:
                    w.WriteByte((byte)EventTag.PlayerMoved); w.WriteInt(e.PlayerId); w.WriteInt(e.FromSquare);
                    w.WriteInt(e.ToSquare); w.WriteBool(e.PassedStart); w.WriteInt((int)e.Kind);
                    break;
                case MoneyChangedEvent e:
                    w.WriteByte((byte)EventTag.MoneyChanged); w.WriteInt(e.PlayerId); w.WriteInt(e.Delta); w.WriteInt((int)e.Reason);
                    break;
                case PropertyOwnerChangedEvent e:
                    w.WriteByte((byte)EventTag.OwnerChanged); w.WriteInt(e.SquareIndex); w.WriteInt(e.PreviousOwnerId);
                    w.WriteInt(e.NewOwnerId);
                    break;
                case PlayerBankruptedEvent e:
                    w.WriteByte((byte)EventTag.Bankrupted); w.WriteInt(e.PlayerId); w.WriteInt(e.CreditorId);
                    break;
                case PhaseChangedEvent e:
                    w.WriteByte((byte)EventTag.PhaseChanged); w.WriteInt((int)e.Phase);
                    break;
                case GameEndedEvent e:
                    w.WriteByte((byte)EventTag.GameEnded); w.WriteInt(e.WinnerId); w.WriteInt((int)e.Reason);
                    break;
                case BuildingLevelChangedEvent e:
                    w.WriteByte((byte)EventTag.BuildingLevel); w.WriteInt(e.SquareIndex); w.WriteInt(e.Level);
                    break;
                case AuctionStartedEvent e:
                    w.WriteByte((byte)EventTag.AuctionStarted); w.WriteInt(e.SquareIndex);
                    break;
                case BidPlacedEvent e:
                    w.WriteByte((byte)EventTag.BidPlaced); w.WriteInt(e.PlayerId); w.WriteInt(e.Amount);
                    break;
                case BidPassedEvent e:
                    w.WriteByte((byte)EventTag.BidPassed); w.WriteInt(e.PlayerId);
                    break;
                case AuctionEndedEvent e:
                    w.WriteByte((byte)EventTag.AuctionEnded); w.WriteInt(e.SquareIndex); w.WriteInt(e.WinnerId); w.WriteInt(e.Price);
                    break;
                case TradeProposedEvent e:
                    w.WriteByte((byte)EventTag.TradeProposed); WriteOffer(w, e.Offer);
                    break;
                case TradeResolvedEvent e:
                    w.WriteByte((byte)EventTag.TradeResolved); w.WriteInt((int)e.Outcome);
                    break;
                case DebtIncurredEvent e:
                    w.WriteByte((byte)EventTag.DebtIncurred); w.WriteInt(e.PlayerId); w.WriteInt(e.CreditorId);
                    w.WriteInt(e.Amount); w.WriteInt((int)e.Reason);
                    break;
                case DebtClearedEvent e:
                    w.WriteByte((byte)EventTag.DebtCleared); w.WriteInt(e.PlayerId);
                    break;
                case PotChangedEvent e:
                    w.WriteByte((byte)EventTag.PotChanged); w.WriteInt(e.Delta);
                    break;
                case CardDrawnEvent e:
                    w.WriteByte((byte)EventTag.CardDrawn); w.WriteInt(e.PlayerId); w.WriteInt(e.CardIndex);
                    break;
                case LookoutChangedEvent e:
                    w.WriteByte((byte)EventTag.Lookout); w.WriteInt(e.PlayerId); w.WriteBool(e.HasLookout);
                    break;
                case DiceOptionsEvent e:
                    w.WriteByte((byte)EventTag.DiceOptions); w.WriteInt(e.PlayerId);
                    w.WriteInt(e.FirstDie1); w.WriteInt(e.FirstDie2); w.WriteInt(e.SecondDie1); w.WriteInt(e.SecondDie2);
                    break;
                case PlayerTimedOutEvent e:
                    w.WriteByte((byte)EventTag.TimedOut); w.WriteInt(e.PlayerId); w.WriteBool(e.StandInTookOver);
                    break;
                default:
                    throw new ArgumentException($"Cannot encode event {gameEvent?.GetType().Name}.");
            }
        }

        private static GameEvent ReadEvent(NetReader r)
        {
            var tag = (EventTag)r.ReadByte();
            switch (tag)
            {
                case EventTag.TurnStarted: return new TurnStartedEvent(r.ReadInt());
                case EventTag.DiceRolled: return new DiceRolledEvent(r.ReadInt(), r.ReadInt(), r.ReadInt());
                case EventTag.PlayerMoved: return new PlayerMovedEvent(r.ReadInt(), r.ReadInt(), r.ReadInt(), r.ReadBool(), ReadEnum<MoveKind>(r));
                case EventTag.MoneyChanged: return new MoneyChangedEvent(r.ReadInt(), r.ReadInt(), ReadEnum<MoneyReason>(r));
                case EventTag.OwnerChanged: return new PropertyOwnerChangedEvent(r.ReadInt(), r.ReadInt(), r.ReadInt());
                case EventTag.Bankrupted: return new PlayerBankruptedEvent(r.ReadInt(), r.ReadInt());
                case EventTag.PhaseChanged: return new PhaseChangedEvent(ReadEnum<GamePhase>(r));
                case EventTag.GameEnded: return new GameEndedEvent(r.ReadInt(), ReadEnum<GameEndReason>(r));
                case EventTag.BuildingLevel: return new BuildingLevelChangedEvent(r.ReadInt(), r.ReadInt());
                case EventTag.AuctionStarted: return new AuctionStartedEvent(r.ReadInt());
                case EventTag.BidPlaced: return new BidPlacedEvent(r.ReadInt(), r.ReadInt());
                case EventTag.BidPassed: return new BidPassedEvent(r.ReadInt());
                case EventTag.AuctionEnded: return new AuctionEndedEvent(r.ReadInt(), r.ReadInt(), r.ReadInt());
                case EventTag.TradeProposed: return new TradeProposedEvent(ReadOffer(r));
                case EventTag.TradeResolved: return new TradeResolvedEvent(ReadEnum<TradeOutcome>(r));
                case EventTag.DebtIncurred: return new DebtIncurredEvent(r.ReadInt(), r.ReadInt(), r.ReadInt(), ReadEnum<MoneyReason>(r));
                case EventTag.DebtCleared: return new DebtClearedEvent(r.ReadInt());
                case EventTag.PotChanged: return new PotChangedEvent(r.ReadInt());
                case EventTag.CardDrawn: return new CardDrawnEvent(r.ReadInt(), r.ReadInt());
                case EventTag.Lookout: return new LookoutChangedEvent(r.ReadInt(), r.ReadBool());
                case EventTag.DiceOptions: return new DiceOptionsEvent(r.ReadInt(), r.ReadInt(), r.ReadInt(), r.ReadInt(), r.ReadInt());
                case EventTag.TimedOut: return new PlayerTimedOutEvent(r.ReadInt(), r.ReadBool());
                default: throw new FormatException($"Unknown event tag {(byte)tag}.");
            }
        }

        /// <summary>Writes a full game state; also used for save files.</summary>
        public static void WriteSnapshot(NetWriter w, GameStateSnapshot s)
        {
            if (s == null) throw new ArgumentException("GameStart needs a snapshot.");
            w.WriteInt((int)s.Phase);
            w.WriteInt(s.CurrentPlayerIndex);
            w.WriteInt(s.TurnNumber);
            w.WriteInt(s.LastDie1);
            w.WriteInt(s.LastDie2);
            w.WriteInt(s.ConsecutiveDoubles);
            w.WriteInt(s.WinnerId);
            w.WriteInt(s.Round);
            w.WriteInt(s.Pot);
            w.WriteInt(s.BuiltThisVisit);
            w.WriteBool(s.DiceOptions != null);
            if (s.DiceOptions != null)
            {
                if (s.DiceOptions.Length != 4) throw new ArgumentException("Dice options need four dice.");
                foreach (int die in s.DiceOptions) w.WriteInt(die);
            }

            w.WriteInt(s.Players.Count);
            foreach (GameStateSnapshot.PlayerEntry p in s.Players)
            {
                w.WriteInt(p.Id);
                w.WriteString(p.Name);
                w.WriteInt(p.Money);
                w.WriteInt(p.Position);
                w.WriteBool(p.IsBankrupt);
                w.WriteBool(p.HasLookout);
                w.WriteInt(p.Laps);
            }

            w.WriteInt(s.Owners.Length);
            for (int i = 0; i < s.Owners.Length; i++)
            {
                w.WriteInt(s.Owners[i]);
                w.WriteInt(s.BuildingLevels[i]);
            }

            w.WriteBool(s.Auction != null);
            if (s.Auction != null)
            {
                w.WriteInt(s.Auction.SquareIndex);
                w.WriteInt(s.Auction.HighestBid);
                w.WriteInt(s.Auction.HighestBidderId);
                w.WriteInt(s.Auction.PassedPlayerIds.Count);
                foreach (int id in s.Auction.PassedPlayerIds) w.WriteInt(id);
            }

            w.WriteBool(s.PendingTrade != null);
            if (s.PendingTrade != null)
            {
                WriteOffer(w, s.PendingTrade);
                w.WriteInt((int)s.TradeReturnPhase);
            }

            w.WriteBool(s.Debt != null);
            if (s.Debt != null)
            {
                w.WriteInt(s.Debt.PlayerId);
                w.WriteInt(s.Debt.CreditorId);
                w.WriteInt(s.Debt.Amount);
                w.WriteInt((int)s.Debt.Reason);
            }
        }

        /// <summary>The snapshot layout written today (Faz 8: adds the buildings-this-visit count).</summary>
        public const int SnapshotVersion = 5;

        /// <summary>Layout 4 counted laps but still carried mortgage flags.</summary>
        public const int LapSnapshotVersion = 4;

        /// <summary>Layout 3 knew the building visit but not the players' laps.</summary>
        public const int VisitSnapshotVersion = 3;

        /// <summary>The Faz 7c layout: round, pot, dice options and lookouts, no build count.</summary>
        public const int TableSnapshotVersion = 2;

        /// <summary>The layout before the Faz 7c squares: no round, pot, dice options or lookouts.</summary>
        public const int LegacySnapshotVersion = 1;

        /// <summary>Reads what <see cref="WriteSnapshot"/> wrote. Throws <see cref="FormatException"/> on bad data.</summary>
        public static GameStateSnapshot ReadSnapshot(NetReader r) => ReadSnapshot(r, SnapshotVersion);

        /// <summary>
        /// Reads a snapshot in the given layout version, so old save files still open. A legacy
        /// snapshot gets its round estimated from the turn number, an empty pot and no lookouts.
        /// </summary>
        public static GameStateSnapshot ReadSnapshot(NetReader r, int version)
        {
            if (version < LegacySnapshotVersion || version > SnapshotVersion) throw new FormatException($"Snapshot layout {version} is not supported.");
            bool legacy = version == LegacySnapshotVersion;

            var s = new GameStateSnapshot
            {
                Phase = ReadEnum<GamePhase>(r),
                CurrentPlayerIndex = r.ReadInt(),
                TurnNumber = r.ReadInt(),
                LastDie1 = r.ReadInt(),
                LastDie2 = r.ReadInt(),
                ConsecutiveDoubles = r.ReadInt(),
                WinnerId = r.ReadInt(),
            };

            if (!legacy)
            {
                s.Round = r.ReadInt();
                s.Pot = r.ReadInt();
                s.BuiltThisVisit = version >= VisitSnapshotVersion ? r.ReadInt() : GameState.NotArrived;
                if (r.ReadBool()) s.DiceOptions = new[] { r.ReadInt(), r.ReadInt(), r.ReadInt(), r.ReadInt() };
            }

            int players = r.ReadCount();
            for (int i = 0; i < players; i++)
            {
                s.Players.Add(new GameStateSnapshot.PlayerEntry
                {
                    Id = r.ReadInt(),
                    Name = r.ReadString(),
                    Money = r.ReadInt(),
                    Position = r.ReadInt(),
                    IsBankrupt = r.ReadBool(),
                    HasLookout = !legacy && r.ReadBool(),
                    Laps = version >= LapSnapshotVersion ? r.ReadInt() : -1,
                });
            }

            if (legacy) s.Round = players > 0 ? s.TurnNumber / players + 1 : 1;
            // Older layouts did not count laps; a player has gone round about once per completed round.
            foreach (GameStateSnapshot.PlayerEntry p in s.Players) if (p.Laps < 0) p.Laps = Math.Max(0, s.Round - 1);

            int squares = r.ReadCount();
            s.Owners = new int[squares];
            s.BuildingLevels = new int[squares];
            for (int i = 0; i < squares; i++)
            {
                s.Owners[i] = r.ReadInt();
                s.BuildingLevels[i] = r.ReadInt();
                // Layouts before 5 carried a mortgage flag; a mortgaged town simply comes back as owned.
                if (version < SnapshotVersion) r.ReadBool();
            }

            if (r.ReadBool())
            {
                int square = r.ReadInt();
                int bid = r.ReadInt();
                int bidder = r.ReadInt();
                if (bid < 0) throw new FormatException("Negative auction bid.");
                s.Auction = new AuctionState(square, bid, bidder, ReadInts(r));
            }

            if (r.ReadBool())
            {
                s.PendingTrade = ReadOffer(r);
                s.TradeReturnPhase = ReadEnum<GamePhase>(r);
            }

            if (r.ReadBool())
            {
                s.Debt = new DebtState(r.ReadInt(), r.ReadInt(), r.ReadInt(), ReadEnum<MoneyReason>(r));
            }

            return s;
        }

        /// <summary>Writes the table's house rules; also used for save files.</summary>
        public static void WriteTable(NetWriter w, TableRules table)
        {
            w.WriteInt(table.StartingMoney);
            w.WriteBool(table.AuctionsEnabled);
            w.WriteInt(table.RoundLimit);
        }

        public static TableRules ReadTable(NetReader r)
        {
            int money = r.ReadInt();
            bool auctions = r.ReadBool();
            int rounds = r.ReadInt();
            if (money < 0 || rounds < 0) throw new FormatException("Invalid table rules.");
            return new TableRules(money, auctions, rounds);
        }

        /// <summary>Reads an int-backed enum and refuses values the enum does not define.</summary>
        private static T ReadEnum<T>(NetReader r) where T : struct, Enum
        {
            int value = r.ReadInt();
            if (!Enum.IsDefined(typeof(T), value)) throw new FormatException($"Invalid {typeof(T).Name} value {value}.");
            return (T)(object)value;
        }
    }
}
