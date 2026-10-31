using System;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Save;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using NUnit.Framework;

namespace Lazpoly.App.Tests
{
    public class LocalGameAndSaveTests
    {
        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Start"),
            SquareDefinition.Property("A1", 60, 6, 1, 50, new[] { 30, 90, 240, 330, 420 }),
            SquareDefinition.Property("A2", 80, 8, 1, 50, new[] { 40, 120, 320, 440, 560 }),
            SquareDefinition.Tax("Tax", 100),
            SquareDefinition.Property("B1", 120, 12, 2, 100, new[] { 60, 180, 480, 660, 840 }),
            SquareDefinition.Property("B2", 140, 14, 2, 100, new[] { 70, 210, 560, 770, 980 }),
            SquareDefinition.Rest("Rest"),
            SquareDefinition.Property("C1", 200, 20, 3, 150, new[] { 100, 300, 800, 1100, 1400 }),
            SquareDefinition.Property("C2", 220, 22, 3, 150, new[] { 110, 330, 880, 1210, 1540 }),
            SquareDefinition.Property("Solo", 150, 15, 0),
        });

        private static readonly RulesConfig Rules = new RulesConfig(800, 100, 6, 3, 2, 10, 50);
        private static readonly ulong Checksum = BoardChecksum.Compute(Board, Rules);

        private static PlayerSeat[] Seats(int count)
        {
            var seats = new PlayerSeat[count];
            for (int i = 0; i < count; i++) seats[i] = new PlayerSeat(i + 1, $"P{i + 1}");
            return seats;
        }

        private static LocalGame AllAi(int players, ulong seed)
        {
            var ai = new Dictionary<int, AiDifficulty>();
            for (int i = 1; i <= players; i++) ai.Add(i, (AiDifficulty)(1 + i % 3));
            return LocalGame.Create(Board, Rules, Seats(players), ai, seed);
        }

        private static void Steps(LocalGame game, int count)
        {
            for (int i = 0; i < count && game.Session.State.Phase != GamePhase.GameOver; i++)
            {
                Assert.IsTrue(game.Session.StepAi(), "With only computer players the AI must always have a move.");
            }
        }

        [Test]
        public void PeopleCannotMoveForComputerPlayers()
        {
            LocalGame game = LocalGame.Create(Board, Rules, Seats(2), new Dictionary<int, AiDifficulty> { { 2, AiDifficulty.Easy } }, 4UL);
            LocalGameSession session = game.Session;
            var rejections = new List<RejectionReason>();
            session.CommandRejected += rejections.Add;

            Assert.IsTrue(session.CanControl(1));
            Assert.IsFalse(session.CanControl(2));
            Assert.IsTrue(session.IsAiPlaying(2));
            Assert.IsFalse(session.StepAi(), "Player 1, a person, is on turn.");

            session.Submit(new RollDiceCommand(1));
            while (session.State.CurrentPlayer.Id == 1 && session.State.Phase != GamePhase.GameOver)
            {
                GameState state = session.State;
                if (state.Phase == GamePhase.AwaitingBuyDecision) session.Submit(new DeclinePurchaseCommand(1));
                else if (state.Phase == GamePhase.AuctionInProgress && !session.StepAi()) session.Submit(new PassBidCommand(1));
                else if (state.Phase == GamePhase.TurnEnd) session.Submit(new EndTurnCommand(1));
                else if (state.Phase == GamePhase.WaitingForRoll) session.Submit(new RollDiceCommand(1));
                else if (state.Phase == GamePhase.AwaitingDebtPayment) session.Submit(new DeclareBankruptcyCommand(1));
            }

            session.Submit(new RollDiceCommand(2));
            CollectionAssert.Contains(rejections, RejectionReason.NotYourPlayer);
            Assert.IsTrue(session.StepAi(), "Now the computer is on turn.");
        }

        [Test]
        public void ComputerOnlyGamesFinish()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                LocalGame game = AllAi(2 + (int)(seed % 3), seed);
                Steps(game, 20000);
                Assert.AreEqual(GamePhase.GameOver, game.Session.State.Phase, $"Seed {seed} did not finish.");
            }
        }

        [Test]
        public void SavedGameResumesExactly()
        {
            for (ulong seed = 1; seed <= 10; seed++)
            {
                LocalGame original = AllAi(4, seed);
                Steps(original, 150);

                byte[] bytes = SavedGameCodec.Encode(original.Save(Checksum));
                LocalGame resumed = LocalGame.Resume(Board, Rules, Checksum, SavedGameCodec.Decode(bytes));
                Assert.AreEqual(original.Session.State.ComputeHash(), resumed.Session.State.ComputeHash());

                for (int i = 0; i < 400 && original.Session.State.Phase != GamePhase.GameOver; i++)
                {
                    original.Session.StepAi();
                    resumed.Session.StepAi();
                    Assert.AreEqual(original.Session.State.ComputeHash(), resumed.Session.State.ComputeHash(),
                        $"Seed {seed}: the resumed game went another way at step {i}.");
                }
            }
        }

        [Test]
        public void SaveKeepsPeopleAndComputerSeats()
        {
            var ai = new Dictionary<int, AiDifficulty> { { 2, AiDifficulty.Hard }, { 3, AiDifficulty.Easy } };
            LocalGame game = LocalGame.Create(Board, Rules, Seats(3), ai, 8UL);

            SavedGame save = SavedGameCodec.Decode(SavedGameCodec.Encode(game.Save(Checksum)));
            LocalGame resumed = LocalGame.Resume(Board, Rules, Checksum, save);

            Assert.IsTrue(resumed.Session.CanControl(1));
            Assert.AreEqual(AiDifficulty.Hard, save.AiPlayers[2].Difficulty);
            Assert.IsTrue(resumed.Session.IsAiPlaying(3));
            Assert.AreEqual("P2", resumed.Session.State.FindPlayer(2).Name);
        }

        [Test]
        public void SaveFromOtherBoardDataIsRefused()
        {
            SavedGame save = AllAi(2, 1UL).Save(Checksum);
            Assert.Throws<ArgumentException>(() => LocalGame.Resume(Board, Rules, Checksum + 1, save));

            var otherBoard = new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.Rest("R") });
            Assert.Throws<ArgumentException>(() => LocalGame.Resume(otherBoard, Rules, Checksum, save));
        }

        [Test]
        public void BrokenSaveFilesAreRejected()
        {
            byte[] good = SavedGameCodec.Encode(AllAi(3, 2UL).Save(Checksum));

            byte[] wrongMagic = (byte[])good.Clone();
            wrongMagic[0] ^= 0xFF;
            Assert.Throws<FormatException>(() => SavedGameCodec.Decode(wrongMagic));

            byte[] wrongVersion = (byte[])good.Clone();
            wrongVersion[8] = 99;
            Assert.Throws<FormatException>(() => SavedGameCodec.Decode(wrongVersion));

            byte[] truncated = new byte[good.Length - 3];
            Array.Copy(good, truncated, truncated.Length);
            Assert.Throws<FormatException>(() => SavedGameCodec.Decode(truncated));

            byte[] trailing = new byte[good.Length + 1];
            Array.Copy(good, trailing, good.Length);
            Assert.Throws<FormatException>(() => SavedGameCodec.Decode(trailing));
        }
    }
}
