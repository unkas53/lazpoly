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
            GameState state = GameState.CreateInitial(Board, new RulesConfig(money, 100, 6, 3, 2, 10, 50),
                new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });
            return state;
        }

        private static void Own(GameState state, int square, int owner) =>
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(square, GameState.NoOwner, owner));

        [Test]
        public void NamesTheSquaresStillMissingFromTheGroup()
        {
            GameState state = State();
            Own(state, 1, 1);
            Own(state, 2, 2);
            Assert.AreEqual("Ev için bu rengin tamamı gerekli; eksik: Kuru Yolu.", BuildHint.For(Board, state, 1, 1));
        }

        [Test]
        public void AsksToRedeemAMortgagedSquareFirst()
        {
            GameState state = State();
            Own(state, 1, 1);
            Own(state, 2, 1);
            GameStateReducer.Apply(state, new MortgageChangedEvent(2, true));
            Assert.AreEqual("Bu renkte ipotekli kare var; önce onu kurtar.", BuildHint.For(Board, state, 1, 1));
        }

        [Test]
        public void AsksToBuildEvenly()
        {
            GameState state = State();
            Own(state, 1, 1);
            Own(state, 2, 1);
            GameStateReducer.Apply(state, new BuildingLevelChangedEvent(1, 1));
            StringAssert.StartsWith("Evler eşit kurulur", BuildHint.For(Board, state, 1, 1));
            Assert.IsNull(BuildHint.For(Board, state, 1, 2), "The lower square can be built on.");
        }

        [Test]
        public void SaysWhenMoneyIsShort()
        {
            GameState state = State(money: 30);
            Own(state, 1, 1);
            Own(state, 2, 1);
            Assert.AreEqual("Ev için 50 gerekli, paran yetmiyor.", BuildHint.For(Board, state, 1, 1));
        }

        [Test]
        public void SaysWhenItIsNotThePlayersTurn()
        {
            GameState state = State();
            Own(state, 1, 2);
            Own(state, 2, 2);
            StringAssert.StartsWith("Ev, kendi sıranda", BuildHint.For(Board, state, 2, 1));
        }

        [Test]
        public void NothingToSayForPlainPropertiesOrWhenBuildingIsAllowed()
        {
            GameState state = State();
            Own(state, 1, 1);
            Own(state, 2, 1);
            Own(state, 3, 1);
            Assert.IsNull(BuildHint.For(Board, state, 1, 3));
            Assert.IsNull(BuildHint.For(Board, state, 1, 1));
        }
    }
}
