using System.Collections.Generic;
using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class TipsTests
    {
        private static readonly int[] Rents = { 50, 150, 400, 550, 700 };

        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Başlangıç"),
            SquareDefinition.Property("Sur Yolu", 60, 4, 1, 50, Rents),
            SquareDefinition.Property("Kuru Yolu", 60, 4, 1, 50, Rents),
            SquareDefinition.Tax("Emlak Vergisi", 100),
            SquareDefinition.Pot("Çay Molası"),
            SquareDefinition.Travel("Otogar", 50),
            SquareDefinition.Lookout("Seyir Terası"),
            SquareDefinition.Card("Sokak Haberi"),
        }, new[] { CardDefinition.Collect("Piyango", 50) });

        private static readonly RulesConfig Rules = new RulesConfig(1000, 200, 6, 3, 2, 50);

        private static GameState State() =>
            GameState.CreateInitial(Board, Rules, new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });

        private static TipId? Next(GameState state, int player, RulesConfig rules = null, params TipId[] seen) =>
            Tips.Next(Board, rules ?? Rules, state, player, id => seen.Contains(id))?.Id;

        private sealed class MemoryStorage : ISettingsStorage
        {
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public int Saves;
            public bool HasKey(string key) => Ints.ContainsKey(key);
            public int GetInt(string key) => Ints[key];
            public float GetFloat(string key) => 0f;
            public void SetInt(string key, int value) => Ints[key] = value;
            public void SetFloat(string key, float value) { }
            public string GetString(string key) => string.Empty;
            public void SetString(string key, string value) { }
            public void Save() => Saves++;
        }

        [Test]
        public void FirstTurnExplainsTheRollOnlyToThePlayerOnTurn()
        {
            GameState state = State();
            Assert.AreEqual(TipId.FirstRoll, Next(state, 1));
            Assert.IsNull(Next(state, 2));
            Assert.IsNull(Next(state, 1, null, TipId.FirstRoll));
        }

        [Test]
        public void DecisionsComeBeforeGeneralHints()
        {
            GameState state = State();
            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.AwaitingBuyDecision));
            Assert.AreEqual(TipId.BuyOrDecline, Next(state, 1, Rules.WithTable(TableRules.Default.WithRoundLimit(20))));

            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.AwaitingDiceChoice));
            Assert.AreEqual(TipId.DiceChoice, Next(state, 1));
        }

        [Test]
        public void BuyHintSaysADeclinedSquareStaysWithTheBank()
        {
            GameState state = State();
            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.AwaitingBuyDecision));
            string hint = Tips.Next(Board, Rules, state, 1, _ => false).Value.Text;
            StringAssert.Contains("bankada kalır", hint);
            StringAssert.DoesNotContain("artırma", hint);
        }

        [Test]
        public void RideHintNamesTheFare()
        {
            GameState state = State();
            GameStateReducer.Apply(state, new PlayerMovedEvent(1, 0, 5, false));
            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.AwaitingTravelDecision));
            Tip tip = Tips.Next(Board, Rules, state, 1, _ => false).Value;
            Assert.AreEqual(TipId.Travel, tip.Id);
            StringAssert.Contains("50", tip.Text);
        }

        [Test]
        public void JailHintAndRulesNameTheTurnsAndTheFee()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Başlangıç"), SquareDefinition.Property("Sur Yolu", 60, 4, 1, 50, Rents),
                SquareDefinition.Jail("Hapis", 50), SquareDefinition.Property("Kuru Yolu", 60, 4, 1, 50, Rents),
            });
            var rules = new RulesConfig(1000, 200, 6, 3, 2, 50, jailTurns: 3);
            GameState state = GameState.CreateInitial(board, rules, new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });
            GameStateReducer.Apply(state, new PlayerMovedEvent(1, 0, 2, false));
            GameStateReducer.Apply(state, new JailChangedEvent(1, 2));
            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.AwaitingJailDecision));
            Tip tip = Tips.Next(board, rules, state, 1, _ => false).Value;
            Assert.AreEqual(TipId.Jail, tip.Id);
            StringAssert.Contains("2 tur", tip.Text);
            StringAssert.Contains("50", tip.Text);

            IReadOnlyList<KeyValuePair<string, string>> sections = HowToPlay.Sections(board, rules);
            StringAssert.Contains("hapse düşersin", sections.Single(s => s.Key == "Sıra").Value);
            string jail = sections.Single(s => s.Key == "Hapis").Value;
            StringAssert.Contains("3 kez", jail);
            StringAssert.Contains("50 ödeyip", jail);
            StringAssert.Contains("hamlen yanar", HowToPlay.Sections(Board, Rules).Single(s => s.Key == "Sıra").Value, "without a jail three doubles only end the move");
        }

        [Test]
        public void ArrivingOnYourOwnTownSuggestsBuilding()
        {
            GameState state = State();
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(1, GameState.NoOwner, 1));
            Assert.AreNotEqual(TipId.CanBuild, Next(state, 1, null, TipId.FirstRoll), "Away from the town there is nothing to build.");

            GameStateReducer.Apply(state, new PlayerMovedEvent(1, 0, 1, false));
            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.TurnEnd));
            Assert.AreEqual(TipId.CanBuild, Next(state, 1, null, TipId.FirstRoll));
        }

        [Test]
        public void AFullPotIsPointedOut()
        {
            GameState state = State();
            GameStateReducer.Apply(state, new PotChangedEvent(150));
            Assert.AreEqual(TipId.Pot, Next(state, 2));
        }

        [Test]
        public void NoHintsAfterTheGameIsOver()
        {
            GameState state = State();
            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.GameOver));
            Assert.IsNull(Next(state, 1));
        }

        [Test]
        public void MemoryKeepsSeenHintsAndTheOffSwitch()
        {
            var storage = new MemoryStorage();
            var memory = new TipMemory(storage);
            Assert.IsTrue(memory.Enabled);
            Assert.IsFalse(memory.Seen(TipId.Trade));

            memory.MarkSeen(TipId.Trade);
            memory.MarkSeen(TipId.Trade);
            memory.TurnOff();

            var again = new TipMemory(storage);
            Assert.IsTrue(again.Seen(TipId.Trade));
            Assert.IsFalse(again.Seen(TipId.FirstRoll));
            Assert.IsFalse(again.Enabled);
            Assert.AreEqual(2, storage.Saves, "Marking a seen hint again should not write.");

            again.Reset();
            var reset = new TipMemory(storage);
            Assert.IsTrue(reset.Enabled);
            Assert.IsFalse(reset.Seen(TipId.Trade));
        }

        [Test]
        public void HowToPlayFollowsTheBoardAndTableRules()
        {
            List<string> titles = HowToPlay.Sections(Board, Rules).Select(s => s.Key).ToList();
            CollectionAssert.IsSubsetOf(new[] { "Amaç", "Sokak Haberi", "Çay Molası", "Otogar", "Seyir Terası" }, titles);
            StringAssert.Contains("50 bilet", HowToPlay.Sections(Board, Rules).Single(s => s.Key == "Otogar").Value);

            string goal = HowToPlay.Sections(Board, Rules.WithTable(TableRules.Default.WithRoundLimit(15))).Single(s => s.Key == "Amaç").Value;
            StringAssert.Contains("15 tur", goal);

            var plain = new BoardDefinition(new[] { SquareDefinition.Start("Başlangıç"), SquareDefinition.Property("Sur Yolu", 60, 4, 1), SquareDefinition.Rest("Park") });
            Assert.IsFalse(HowToPlay.Sections(plain, Rules).Any(s => s.Key == "Otogar" || s.Key == "Çay Molası"));
        }

        [Test]
        public void HowToPlayExplainsTakeoversAndTheRidesLimit()
        {
            string takeover = HowToPlay.Sections(Board, Rules).Single(s => s.Key == "Şehir devralma").Value;
            StringAssert.Contains("%150", takeover);
            StringAssert.Contains("Otelli", takeover);
            StringAssert.Contains("Şampiyonlar Kupası", HowToPlay.Sections(Board, Rules).Single(s => s.Key == "Otogar").Value);
        }

        [Test]
        public void HowToPlayNamesBothKindsOfCardSquareOnce()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Başlangıç"), SquareDefinition.Card("Şans"), SquareDefinition.Card("Sokak Haberi"), SquareDefinition.Card("Şans"),
            }, new[] { CardDefinition.Collect("Piyango", 50) });
            List<string> titles = HowToPlay.Sections(board, Rules).Select(s => s.Key).ToList();
            CollectionAssert.Contains(titles, "Şans ve Sokak Haberi");
            Assert.IsFalse(titles.Contains("Sokak Haberi"), "the squares share one section");
        }
    }
}
