using System;
using System.Collections.Generic;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.State;
using Lazpoly.Net.Messages;
using NUnit.Framework;

namespace Lazpoly.App.Tests
{
    public class MessageCodecTests
    {
        private static GameStateSnapshot SampleSnapshot()
        {
            var engine = new GameEngine(NetTestData.Board(), NetTestData.Rules(),
                new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Çağrı Öztürk") }, new Pcg32Random(3UL));
            engine.Execute(new RollDiceCommand(1));
            return GameStateSnapshot.Capture(engine.State);
        }

        private static IEnumerable<NetMessage> AllMessages()
        {
            yield return new HelloMessage { ProtocolVersion = MessageCodec.ProtocolVersion, BoardChecksum = ulong.MaxValue };
            yield return new GameStartMessage
            {
                ProtocolVersion = MessageCodec.ProtocolVersion, BoardChecksum = 12345UL, YourPlayerId = 2, Sequence = 17,
                Snapshot = SampleSnapshot(),
            };
            GameStateSnapshot busy = SampleSnapshot();
            busy.BuildingLevels[1] = 3;
            busy.Mortgaged[4] = true;
            busy.Auction = new AuctionState(7, 40, 2, new[] { 1 });
            busy.PendingTrade = new TradeOffer(1, 2, 10, 20, new[] { 1 }, new[] { 2, 4 });
            busy.TradeReturnPhase = GamePhase.TurnEnd;
            busy.Debt = new DebtState(1, 2, 75, MoneyReason.Rent);
            yield return new GameStartMessage
            {
                ProtocolVersion = MessageCodec.ProtocolVersion, BoardChecksum = 1UL, YourPlayerId = 1, Sequence = 3, Snapshot = busy,
            };
            yield return new RefusedMessage { Reason = RefusalReason.BoardMismatch };
            yield return new CommandMessage { Command = new RollDiceCommand(1) };
            yield return new CommandMessage { Command = new BuyPropertyCommand(2) };
            yield return new CommandMessage { Command = new DeclinePurchaseCommand(3) };
            yield return new CommandMessage { Command = new EndTurnCommand(4) };
            yield return new CommandMessage { Command = new BuildCommand(1, 5) };
            yield return new CommandMessage { Command = new SellBuildingCommand(1, 5) };
            yield return new CommandMessage { Command = new MortgageCommand(2, 7) };
            yield return new CommandMessage { Command = new RedeemCommand(2, 7) };
            yield return new CommandMessage { Command = new PlaceBidCommand(3, 140) };
            yield return new CommandMessage { Command = new PassBidCommand(3) };
            yield return new CommandMessage { Command = new ProposeTradeCommand(new TradeOffer(1, 2, 30, 0, new[] { 1, 2 }, new[] { 4 })) };
            yield return new CommandMessage { Command = new AcceptTradeCommand(2) };
            yield return new CommandMessage { Command = new RejectTradeCommand(2) };
            yield return new CommandMessage { Command = new CancelTradeCommand(1) };
            yield return new CommandMessage { Command = new PayDebtCommand(1) };
            yield return new CommandMessage { Command = new DeclareBankruptcyCommand(1) };
            var events = new EventsMessage { Sequence = 9, StateHash = 0xDEADBEEFUL };
            events.Events.AddRange(new GameEvent[]
            {
                new TurnStartedEvent(1),
                new DiceRolledEvent(1, 3, 4),
                new PlayerMovedEvent(1, 8, 2, true),
                new MoneyChangedEvent(1, -120, MoneyReason.PropertyPurchase),
                new PropertyOwnerChangedEvent(4, GameState.NoOwner, 1),
                new PlayerBankruptedEvent(2, 1),
                new PhaseChangedEvent(GamePhase.TurnEnd),
                new GameEndedEvent(1),
                new BuildingLevelChangedEvent(4, 5),
                new MortgageChangedEvent(7, true),
                new AuctionStartedEvent(4),
                new BidPlacedEvent(2, 60),
                new BidPassedEvent(1),
                new AuctionEndedEvent(4, GameState.NoOwner, 0),
                new TradeProposedEvent(new TradeOffer(1, 2, 0, 15, null, new[] { 8 })),
                new TradeResolvedEvent(TradeOutcome.Cancelled),
                new DebtIncurredEvent(1, GameState.NoOwner, 100, MoneyReason.Tax),
                new DebtClearedEvent(1),
            });
            yield return events;
            yield return new RejectedMessage { Reason = RejectionReason.NotYourPlayer };
            yield return new SnapshotRequestMessage();
        }

        [Test]
        public void EveryMessageSurvivesARoundTrip()
        {
            foreach (NetMessage message in AllMessages())
            {
                byte[] encoded = MessageCodec.Encode(message);
                NetMessage decoded = MessageCodec.Decode(encoded);

                Assert.AreEqual(message.GetType(), decoded.GetType());
                CollectionAssert.AreEqual(encoded, MessageCodec.Encode(decoded), $"{message.Type} changed in transit.");
            }
        }

        [Test]
        public void DecodedFieldsMatch()
        {
            var start = (GameStartMessage)MessageCodec.Decode(MessageCodec.Encode(new GameStartMessage
            {
                ProtocolVersion = MessageCodec.ProtocolVersion, BoardChecksum = 77UL, YourPlayerId = 2, Sequence = 5,
                Snapshot = SampleSnapshot(),
            }));

            Assert.AreEqual(77UL, start.BoardChecksum);
            Assert.AreEqual(2, start.YourPlayerId);
            Assert.AreEqual(5, start.Sequence);
            Assert.AreEqual("Çağrı Öztürk", start.Snapshot.Players[1].Name);
            Assert.AreEqual(SampleSnapshot().Restore().ComputeHash(), start.Snapshot.Restore().ComputeHash());

            var command = (CommandMessage)MessageCodec.Decode(MessageCodec.Encode(new CommandMessage { Command = new BuyPropertyCommand(6) }));
            Assert.IsInstanceOf<BuyPropertyCommand>(command.Command);
            Assert.AreEqual(6, command.Command.PlayerId);

            var moved = (EventsMessage)MessageCodec.Decode(MessageCodec.Encode(new EventsMessage
            {
                Sequence = 1, Events = { new PlayerMovedEvent(3, 9, 1, true) },
            }));
            var move = (PlayerMovedEvent)moved.Events[0];
            Assert.AreEqual(3, move.PlayerId);
            Assert.AreEqual(9, move.FromSquare);
            Assert.AreEqual(1, move.ToSquare);
            Assert.IsTrue(move.PassedStart);
        }

        [Test]
        public void TradeOfferFieldsSurvive()
        {
            var offer = new TradeOffer(1, 2, 30, 5, new[] { 1, 2 }, new[] { 4 });
            var decoded = (ProposeTradeCommand)((CommandMessage)MessageCodec.Decode(
                MessageCodec.Encode(new CommandMessage { Command = new ProposeTradeCommand(offer) }))).Command;

            Assert.AreEqual(1, decoded.PlayerId);
            Assert.AreEqual(2, decoded.Offer.ToPlayerId);
            Assert.AreEqual(30, decoded.Offer.GiveMoney);
            Assert.AreEqual(5, decoded.Offer.TakeMoney);
            CollectionAssert.AreEqual(new[] { 1, 2 }, decoded.Offer.GiveSquares);
            CollectionAssert.AreEqual(new[] { 4 }, decoded.Offer.TakeSquares);
        }

        [Test]
        public void TradeOfferFromSomeoneElseIsRejected()
        {
            var writer = new NetWriter();
            writer.WriteByte((byte)MessageType.Command);
            writer.WriteByte(11); // ProposeTrade
            writer.WriteInt(2);   // Commanding player...
            writer.WriteInt(1);   // ...but the offer claims to come from player 1.
            writer.WriteInt(3);
            writer.WriteInt(10);
            writer.WriteInt(0);
            writer.WriteInt(0);
            writer.WriteInt(0);
            Assert.Throws<FormatException>(() => MessageCodec.Decode(writer.ToArray()));
        }

        [Test]
        public void TruncatedMessagesAreRejected()
        {
            foreach (NetMessage message in AllMessages())
            {
                byte[] encoded = MessageCodec.Encode(message);
                for (int length = 0; length < encoded.Length; length++)
                {
                    var cut = new byte[length];
                    Array.Copy(encoded, cut, length);
                    Assert.Throws<FormatException>(() => MessageCodec.Decode(cut), $"{message.Type} cut to {length} bytes was accepted.");
                }
            }
        }

        [Test]
        public void TrailingBytesAreRejected()
        {
            byte[] encoded = MessageCodec.Encode(new SnapshotRequestMessage());
            Assert.Throws<FormatException>(() => MessageCodec.Decode(new[] { encoded[0], (byte)0 }));
        }

        [Test]
        public void UnknownTypesTagsAndEnumValuesAreRejected()
        {
            Assert.Throws<FormatException>(() => MessageCodec.Decode(new byte[] { 0 }));
            Assert.Throws<FormatException>(() => MessageCodec.Decode(new byte[] { 200 }));

            var unknownCommand = new NetWriter();
            unknownCommand.WriteByte((byte)MessageType.Command);
            unknownCommand.WriteByte(99);
            unknownCommand.WriteInt(1);
            Assert.Throws<FormatException>(() => MessageCodec.Decode(unknownCommand.ToArray()));

            var badPhase = new NetWriter();
            badPhase.WriteByte((byte)MessageType.Events);
            badPhase.WriteInt(1);
            badPhase.WriteULong(0);
            badPhase.WriteInt(1);
            badPhase.WriteByte(7); // PhaseChanged
            badPhase.WriteInt(99);
            Assert.Throws<FormatException>(() => MessageCodec.Decode(badPhase.ToArray()));

            var badRefusal = new NetWriter();
            badRefusal.WriteByte((byte)MessageType.Refused);
            badRefusal.WriteByte(250);
            Assert.Throws<FormatException>(() => MessageCodec.Decode(badRefusal.ToArray()));

            var badRejection = new NetWriter();
            badRejection.WriteByte((byte)MessageType.Rejected);
            badRejection.WriteInt(-5);
            Assert.Throws<FormatException>(() => MessageCodec.Decode(badRejection.ToArray()));
        }

        [Test]
        public void HugeCountsAreRejectedBeforeAllocating()
        {
            var writer = new NetWriter();
            writer.WriteByte((byte)MessageType.Events);
            writer.WriteInt(1);
            writer.WriteULong(0);
            writer.WriteInt(int.MaxValue);
            Assert.Throws<FormatException>(() => MessageCodec.Decode(writer.ToArray()));

            var negative = new NetWriter();
            negative.WriteByte((byte)MessageType.Events);
            negative.WriteInt(1);
            negative.WriteULong(0);
            negative.WriteInt(-1);
            Assert.Throws<FormatException>(() => MessageCodec.Decode(negative.ToArray()));
        }

        [Test]
        public void EncodingAGameStartWithoutSnapshotFails()
        {
            Assert.Throws<ArgumentException>(() => MessageCodec.Encode(new GameStartMessage()));
        }
    }
}
