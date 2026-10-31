using System;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;

namespace Lazpoly.Net.Messages
{
    /// <summary>
    /// Turns messages into bytes and back. Decoding never trusts the sender: malformed or
    /// unknown data throws <see cref="FormatException"/>, which sessions treat as a dropped message.
    /// </summary>
    public static class MessageCodec
    {
        public const int ProtocolVersion = 2;

        private enum CommandTag : byte
        {
            Roll = 1, Buy = 2, Decline = 3, EndTurn = 4,
            Build = 5, SellBuilding = 6, Mortgage = 7, Redeem = 8,
            Bid = 9, PassBid = 10,
            ProposeTrade = 11, AcceptTrade = 12, RejectTrade = 13, CancelTrade = 14,
            PayDebt = 15, Bankruptcy = 16,
        }

        private enum EventTag : byte
        {
            TurnStarted = 1, DiceRolled = 2, PlayerMoved = 3, MoneyChanged = 4,
            OwnerChanged = 5, Bankrupted = 6, PhaseChanged = 7, GameEnded = 8,
            BuildingLevel = 9, Mortgage = 10, AuctionStarted = 11, BidPlaced = 12, BidPassed = 13, AuctionEnded = 14,
            TradeProposed = 15, TradeResolved = 16, DebtIncurred = 17, DebtCleared = 18,
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
                case MortgageCommand _: w.WriteByte((byte)CommandTag.Mortgage); break;
                case RedeemCommand _: w.WriteByte((byte)CommandTag.Redeem); break;
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
                default: throw new ArgumentException($"Cannot encode command {command?.GetType().Name}.");
            }

            w.WriteInt(command.PlayerId);
            switch (command)
            {
                case SquareCommand c: w.WriteInt(c.SquareIndex); break;
                case PlaceBidCommand c: w.WriteInt(c.Amount); break;
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
                case CommandTag.Mortgage: return new MortgageCommand(playerId, r.ReadInt());
                case CommandTag.Redeem: return new RedeemCommand(playerId, r.ReadInt());
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
                    w.WriteInt(e.ToSquare); w.WriteBool(e.PassedStart);
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
                    w.WriteByte((byte)EventTag.GameEnded); w.WriteInt(e.WinnerId);
                    break;
                case BuildingLevelChangedEvent e:
                    w.WriteByte((byte)EventTag.BuildingLevel); w.WriteInt(e.SquareIndex); w.WriteInt(e.Level);
                    break;
                case MortgageChangedEvent e:
                    w.WriteByte((byte)EventTag.Mortgage); w.WriteInt(e.SquareIndex); w.WriteBool(e.IsMortgaged);
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
                case EventTag.PlayerMoved: return new PlayerMovedEvent(r.ReadInt(), r.ReadInt(), r.ReadInt(), r.ReadBool());
                case EventTag.MoneyChanged: return new MoneyChangedEvent(r.ReadInt(), r.ReadInt(), ReadEnum<MoneyReason>(r));
                case EventTag.OwnerChanged: return new PropertyOwnerChangedEvent(r.ReadInt(), r.ReadInt(), r.ReadInt());
                case EventTag.Bankrupted: return new PlayerBankruptedEvent(r.ReadInt(), r.ReadInt());
                case EventTag.PhaseChanged: return new PhaseChangedEvent(ReadEnum<GamePhase>(r));
                case EventTag.GameEnded: return new GameEndedEvent(r.ReadInt());
                case EventTag.BuildingLevel: return new BuildingLevelChangedEvent(r.ReadInt(), r.ReadInt());
                case EventTag.Mortgage: return new MortgageChangedEvent(r.ReadInt(), r.ReadBool());
                case EventTag.AuctionStarted: return new AuctionStartedEvent(r.ReadInt());
                case EventTag.BidPlaced: return new BidPlacedEvent(r.ReadInt(), r.ReadInt());
                case EventTag.BidPassed: return new BidPassedEvent(r.ReadInt());
                case EventTag.AuctionEnded: return new AuctionEndedEvent(r.ReadInt(), r.ReadInt(), r.ReadInt());
                case EventTag.TradeProposed: return new TradeProposedEvent(ReadOffer(r));
                case EventTag.TradeResolved: return new TradeResolvedEvent(ReadEnum<TradeOutcome>(r));
                case EventTag.DebtIncurred: return new DebtIncurredEvent(r.ReadInt(), r.ReadInt(), r.ReadInt(), ReadEnum<MoneyReason>(r));
                case EventTag.DebtCleared: return new DebtClearedEvent(r.ReadInt());
                default: throw new FormatException($"Unknown event tag {(byte)tag}.");
            }
        }

        private static void WriteSnapshot(NetWriter w, GameStateSnapshot s)
        {
            if (s == null) throw new ArgumentException("GameStart needs a snapshot.");
            w.WriteInt((int)s.Phase);
            w.WriteInt(s.CurrentPlayerIndex);
            w.WriteInt(s.TurnNumber);
            w.WriteInt(s.LastDie1);
            w.WriteInt(s.LastDie2);
            w.WriteInt(s.ConsecutiveDoubles);
            w.WriteInt(s.WinnerId);
            w.WriteInt(s.Players.Count);
            foreach (GameStateSnapshot.PlayerEntry p in s.Players)
            {
                w.WriteInt(p.Id);
                w.WriteString(p.Name);
                w.WriteInt(p.Money);
                w.WriteInt(p.Position);
                w.WriteBool(p.IsBankrupt);
            }

            w.WriteInt(s.Owners.Length);
            for (int i = 0; i < s.Owners.Length; i++)
            {
                w.WriteInt(s.Owners[i]);
                w.WriteInt(s.BuildingLevels[i]);
                w.WriteBool(s.Mortgaged[i]);
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

        private static GameStateSnapshot ReadSnapshot(NetReader r)
        {
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
                });
            }

            int squares = r.ReadCount();
            s.Owners = new int[squares];
            s.BuildingLevels = new int[squares];
            s.Mortgaged = new bool[squares];
            for (int i = 0; i < squares; i++)
            {
                s.Owners[i] = r.ReadInt();
                s.BuildingLevels[i] = r.ReadInt();
                s.Mortgaged[i] = r.ReadBool();
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

        /// <summary>Reads an int-backed enum and refuses values the enum does not define.</summary>
        private static T ReadEnum<T>(NetReader r) where T : struct, Enum
        {
            int value = r.ReadInt();
            if (!Enum.IsDefined(typeof(T), value)) throw new FormatException($"Invalid {typeof(T).Name} value {value}.");
            return (T)(object)value;
        }
    }
}
