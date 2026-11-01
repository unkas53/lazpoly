using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class BuildHintTests
    {
        private static readonly int[] Rents = { 50, 150, 400, 550, 700 };

        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Başlangıç"),
            SquareDefinition.Property("Sur Yolu", 60, 4, 1, 50, Rents),
            SquareDefinition.Property("Kuru Yolu", 60, 4, 1, 50, Rents),
            SquareDefinition.Property("Liman", 100, 10, 2),
        });

        private static GameState State(int money = 1000)
        {
            GameState state = GameState.CreateInitial(Board, new RulesConfig(money, 100, 6, 3, 2, 50),
                new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });
            return state;
        }

        private static void Own(GameState state, int square, int owner) =>
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(square, GameState.NoOwner, owner));

        private static void Arrive(GameState state, int player, int square, bool passedStart = false)
        {
            GameStateReducer.Apply(state, new PlayerMovedEvent(player, state.FindPlayer(player).Position, square, passedStart));
            GameStateReducer.Apply(state, new PhaseChangedEvent(GamePhase.TurnEnd));
        }

        [Test]
        public void AsksToComeToTheTownFirst()
        {
            GameState state = State();
            Own(state, 1, 1);
            Assert.AreEqual("Ev ya da otel, kendi sıranda bu şehre gelince kurulur.", BuildHint.For(Board, state, 1, 1));
        }

        [Test]
        public void ExplainsTheFirstLapLimitAndTheHotelVisit()
        {
            GameState state = State();
            Own(state, 1, 1);
            Arrive(state, 1, 1);
            GameStateReducer.Apply(state, new BuildingLevelChangedEvent(1, 1));
            StringAssert.StartsWith("Başlangıçtan geçene kadar her şehre tek ev", BuildHint.For(Board, state, 1, 1));

            Arrive(state, 1, 1, passedStart: true);
            for (int level = 2; level <= 4; level++) GameStateReducer.Apply(state, new BuildingLevelChangedEvent(1, level));
            Assert.AreEqual("Otel, dört evli şehre bir sonraki gelişinde kurulur.", BuildHint.For(Board, state, 1, 1));

            Arrive(state, 1, 1);
            Assert.IsNull(BuildHint.For(Board, state, 1, 1), "A fresh visit allows the hotel.");
            GameStateReducer.Apply(state, new BuildingLevelChangedEvent(1, 5));
            Assert.AreEqual("En yüksek seviyede (otel).", BuildHint.For(Board, state, 1, 1));
        }

        [Test]
        public void SaysWhenMoneyIsShort()
        {
            GameState state = State(money: 30);
            Own(state, 1, 1);
            Arrive(state, 1, 1);
            Assert.AreEqual("Ev için 50 gerekli, paran yetmiyor.", BuildHint.For(Board, state, 1, 1));
        }

        [Test]
        public void SaysWhenItIsNotThePlayersTurn()
        {
            GameState state = State();
            Own(state, 1, 2);
            GameStateReducer.Apply(state, new PlayerMovedEvent(2, 0, 1, false));
            StringAssert.StartsWith("Ev ya da otel, kendi sıranda", BuildHint.For(Board, state, 2, 1));
        }

        [Test]
        public void NothingToSayForPlainPropertiesOrWhenBuildingIsAllowed()
        {
            GameState state = State();
            Own(state, 1, 1);
            Own(state, 3, 1);
            Arrive(state, 1, 1);
            Assert.IsNull(BuildHint.For(Board, state, 1, 3));
            Assert.IsNull(BuildHint.For(Board, state, 1, 1));
        }
    }
}
