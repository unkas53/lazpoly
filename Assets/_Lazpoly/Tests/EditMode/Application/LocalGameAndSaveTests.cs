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
using Lazpoly.Net.Messages;
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

        private static readonly RulesConfig Rules = new RulesConfig(800, 100, 6, 3, 2, 50);
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
        public void SaveKeepsTheTableRules()
        {
            var table = new TableRules(1200, 18, 120);
            LocalGame game = LocalGame.Create(Board, Rules.WithTable(table), Seats(2), new Dictionary<int, AiDifficulty>(), 4UL);

            SavedGame save = SavedGameCodec.Decode(SavedGameCodec.Encode(game.Save(Checksum)));
            Assert.IsTrue(save.Table.SameAs(table));

            // Resuming takes the board data's rules and puts the saved table on top.
            LocalGame resumed = LocalGame.Resume(Board, Rules, Checksum, save);
            Assert.AreEqual(1200, resumed.Session.Rules.StartingMoney);
            Assert.AreEqual(18, resumed.Session.Rules.RoundLimit);
            Assert.AreEqual(120, resumed.Session.Rules.TurnSeconds);
        }

        [Test]
        public void SavesFromTheAuctionVersionsStillOpenAndResume()
        {
            LocalGame game = AllAi(3, 12UL);
            Steps(game, 30);
            SavedGame original = game.Save(Checksum);

            foreach (int version in new[] { 5, 6 })
            {
                SavedGame read = SavedGameCodec.Decode(AuctionEraSave(original, version));

                Assert.AreEqual(version == 5 ? TableRules.DefaultTurnSeconds : 45, read.Table.TurnSeconds, $"Format {version}: turn time");
                Assert.AreEqual(original.Table.RoundLimit, read.Table.RoundLimit);
                Assert.AreEqual(original.Snapshot.TurnNumber, read.Snapshot.TurnNumber);
                Assert.AreEqual(original.Snapshot.Players[2].Money, read.Snapshot.Players[2].Money);
                Assert.AreEqual(original.Snapshot.Players[0].Laps, read.Snapshot.Players[0].Laps);
                Assert.AreEqual(original.Snapshot.BuiltThisVisit, read.Snapshot.BuiltThisVisit);
                Assert.AreEqual(original.Snapshot.Restore().ComputeHash(), read.Snapshot.Restore().ComputeHash());

                LocalGame resumed = LocalGame.Resume(Board, Rules, Checksum, read);
                Steps(resumed, 5);
            }
        }

        /// <summary>
        /// A save from before Faz 13b, when tables still had auctions: format 5 (Faz 9) wrote a three-value table,
        /// format 6 (Faz 10c-2) added the turn time, the bank rule and the queue of bank auctions to the snapshot.
        /// </summary>
        private static byte[] AuctionEraSave(SavedGame save, int version)
        {
            var w = new NetWriter();
            w.WriteULong(0x4C415A504F4C5953UL);
            w.WriteInt(version);
            w.WriteULong(save.BoardChecksum);
            w.WriteULong(save.DiceState);
            w.WriteULong(save.DiceIncrement);
            w.WriteULong(save.AiState);
            w.WriteULong(save.AiIncrement);
            w.WriteInt(save.AiPlayers.Count);
            foreach (KeyValuePair<int, SavedAiPlayer> ai in save.AiPlayers)
            {
                w.WriteInt(ai.Key);
                w.WriteByte((byte)ai.Value.Difficulty);
                w.WriteInt(ai.Value.LastProposalTurn);
            }

            w.WriteInt(save.Table.StartingMoney);
            w.WriteBool(true); // auctions on
            w.WriteInt(save.Table.RoundLimit);
            if (version >= 6)
            {
                w.WriteInt(45);
                w.WriteByte(1); // bankrupt players' squares auctioned
            }

            GameStateSnapshot s = save.Snapshot;
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
            if (s.DiceOptions != null) foreach (int die in s.DiceOptions) w.WriteInt(die);
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

            w.WriteBool(false); // no auction running
            if (version >= 6) w.WriteInt(0); // no bank auctions queued

            w.WriteBool(s.PendingTrade != null);
            if (s.PendingTrade != null)
            {
                w.WriteInt(s.PendingTrade.FromPlayerId);
                w.WriteInt(s.PendingTrade.ToPlayerId);
                w.WriteInt(s.PendingTrade.GiveMoney);
                w.WriteInt(s.PendingTrade.TakeMoney);
                w.WriteInt(s.PendingTrade.GiveSquares.Count);
                foreach (int square in s.PendingTrade.GiveSquares) w.WriteInt(square);
                w.WriteInt(s.PendingTrade.TakeSquares.Count);
                foreach (int square in s.PendingTrade.TakeSquares) w.WriteInt(square);
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

            return w.ToArray();
        }

        [Test]
        public void SaveFromOtherBoardDataIsRefused()
        {
            SavedGame save = AllAi(2, 1UL).Save(Checksum);
            Assert.Throws<ArgumentException>(() => LocalGame.Resume(Board, Rules, Checksum + 1, save));

            var otherBoard = new BoardDefinition(new[] { SquareDefinition.Start("S"), SquareDefinition.Rest("R") });
            Assert.Throws<ArgumentException>(() => LocalGame.Resume(otherBoard, Rules, Checksum, save));
        }

        /// <summary>Writes a save the way version 1 did: no table, snapshot without round, pot, dice options or lookouts.</summary>
        private static byte[] LegacySave(SavedGame save)
        {
            var w = new NetWriter();
            w.WriteULong(0x4C415A504F4C5953UL);
            w.WriteInt(1);
            w.WriteULong(save.BoardChecksum);
            w.WriteULong(save.DiceState);
            w.WriteULong(save.DiceIncrement);
            w.WriteULong(save.AiState);
            w.WriteULong(save.AiIncrement);
            w.WriteInt(save.AiPlayers.Count);
            foreach (KeyValuePair<int, SavedAiPlayer> ai in save.AiPlayers)
            {
                w.WriteInt(ai.Key);
                w.WriteByte((byte)ai.Value.Difficulty);
                w.WriteInt(ai.Value.LastProposalTurn);
            }

            GameStateSnapshot s = save.Snapshot;
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
                w.WriteBool(false); // mortgage flag of the old layout
            }

            w.WriteBool(false); // auction
            w.WriteBool(false); // trade
            w.WriteBool(false); // debt
            return w.ToArray();
        }

        [Test]
        public void ASaveFromTheVersionBeforeTableRulesStillOpensAndResumes()
        {
            LocalGame game = AllAi(3, 11UL);
            Steps(game, 40);
            SavedGame original = game.Save(Checksum);

            SavedGame read = SavedGameCodec.Decode(LegacySave(original));

            Assert.IsTrue(read.Table.SameAs(TableRules.Default), "An old save plays with the default table.");
            Assert.AreEqual(original.Snapshot.TurnNumber, read.Snapshot.TurnNumber);
            Assert.AreEqual(original.Snapshot.Players.Count, read.Snapshot.Players.Count);
            Assert.AreEqual(original.Snapshot.Players[1].Money, read.Snapshot.Players[1].Money);
            Assert.AreEqual(0, read.Snapshot.Pot);
            Assert.AreEqual(GameState.NotArrived, read.Snapshot.BuiltThisVisit, "An old save does not know about the visit, so no building until the next move.");
            Assert.AreEqual(read.Snapshot.Round - 1, read.Snapshot.Players[0].Laps, "An old save counts a lap per finished round.");
            Assert.IsNull(read.Snapshot.DiceOptions);
            Assert.AreEqual(original.Snapshot.TurnNumber / 3 + 1, read.Snapshot.Round);
            Assert.AreEqual(original.AiPlayers.Count, read.AiPlayers.Count);

            LocalGame resumed = LocalGame.Resume(Board, Rules, Checksum, read);
            Steps(resumed, 5);
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
