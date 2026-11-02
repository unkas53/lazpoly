using System;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.State;
using NUnit.Framework;

namespace Lazpoly.Core.Tests
{
    public class SnapshotAndChecksumTests
    {
        private static GameEngine PlayedEngine()
        {
            var engine = new GameEngine(TestData.TestBoard(), TestData.TestRules(), TestData.TestSeats(3), new Pcg32Random(77UL));
            for (int i = 0; i < 40 && engine.State.Phase != GamePhase.GameOver; i++)
            {
                int player = engine.State.CurrentPlayer.Id;
                GameCommand command;
                switch (engine.State.Phase)
                {
                    case GamePhase.WaitingForRoll: command = new RollDiceCommand(player); break;
                    case GamePhase.AwaitingBuyDecision: command = new BuyPropertyCommand(player); break;
                    default: command = new EndTurnCommand(player); break;
                }

                Assert.IsTrue(engine.Execute(command).IsAccepted);
            }

            return engine;
        }

        [Test]
        public void RestoredSnapshotHasTheSameHashAndNames()
        {
            GameState original = PlayedEngine().State;

            GameState copy = GameStateSnapshot.Capture(original).Restore();

            Assert.AreEqual(original.ComputeHash(), copy.ComputeHash());
            Assert.AreEqual(original.Players.Count, copy.Players.Count);
            for (int i = 0; i < original.Players.Count; i++)
            {
                Assert.AreEqual(original.Players[i].Name, copy.Players[i].Name);
            }
        }

        [Test]
        public void QueuedBankAuctionsTravelWithTheSnapshotAndCountInTheHash()
        {
            GameState state = PlayedEngine().State;
            ulong before = state.ComputeHash();
            GameStateReducer.Apply(state, new BankAuctionsQueuedEvent(new[] { 1, 2 }));
            Assert.AreNotEqual(before, state.ComputeHash(), "The queue is part of the state.");

            GameState restored = GameStateSnapshot.Capture(state).Restore();
            CollectionAssert.AreEqual(new[] { 1, 2 }, restored.PendingAuctions);
            Assert.AreEqual(state.ComputeHash(), restored.ComputeHash());

            GameStateReducer.Apply(restored, new AuctionStartedEvent(1));
            CollectionAssert.AreEqual(new[] { 2 }, restored.PendingAuctions, "Starting a queued auction takes it off the list.");

            GameStateSnapshot broken = GameStateSnapshot.Capture(state);
            broken.PendingAuctions = new[] { 99 };
            Assert.Throws<System.ArgumentException>(() => broken.Restore());
        }

        [Test]
        public void SnapshotIsACopyNotAView()
        {
            GameState original = PlayedEngine().State;
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(original);
            ulong before = original.ComputeHash();

            snapshot.Players[0].Money += 999;
            snapshot.Owners[1] = TestData.Bob;

            Assert.AreEqual(before, original.ComputeHash());
        }

        [Test]
        public void InvalidSnapshotsAreRefused()
        {
            GameStateSnapshot Fresh() => GameStateSnapshot.Capture(PlayedEngine().State);

            GameStateSnapshot unknownOwner = Fresh();
            unknownOwner.Owners[1] = 12345;
            Assert.Throws<ArgumentException>(() => unknownOwner.Restore());

            GameStateSnapshot offBoard = Fresh();
            offBoard.Players[0].Position = offBoard.Owners.Length;
            Assert.Throws<ArgumentException>(() => offBoard.Restore());

            GameStateSnapshot duplicate = Fresh();
            duplicate.Players[1].Id = duplicate.Players[0].Id;
            Assert.Throws<ArgumentException>(() => duplicate.Restore());

            GameStateSnapshot badTurn = Fresh();
            badTurn.CurrentPlayerIndex = badTurn.Players.Count;
            Assert.Throws<ArgumentException>(() => badTurn.Restore());

            GameStateSnapshot onePlayer = Fresh();
            onePlayer.Players.RemoveRange(1, onePlayer.Players.Count - 1);
            Assert.Throws<ArgumentException>(() => onePlayer.Restore());
        }

        [Test]
        public void ChecksumIsStableAndSensitiveToData()
        {
            ulong reference = BoardChecksum.Compute(TestData.TestBoard(), TestData.TestRules());

            Assert.AreEqual(reference, BoardChecksum.Compute(TestData.TestBoard(), TestData.TestRules()));
            Assert.AreNotEqual(reference, BoardChecksum.Compute(TestData.TestBoard(), TestData.TestRules(startingMoney: 1001)));

            var squares = new SquareDefinition[TestData.TestBoard().Count];
            for (int i = 0; i < squares.Length; i++) squares[i] = TestData.TestBoard()[i];
            squares[1] = SquareDefinition.Property("A1", 61, 10, 1);
            Assert.AreNotEqual(reference, BoardChecksum.Compute(new BoardDefinition(squares), TestData.TestRules()));
        }
    }
}
