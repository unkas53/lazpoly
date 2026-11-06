using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using NUnit.Framework;
using static Lazpoly.Core.Tests.TestData;

namespace Lazpoly.Core.Tests
{
    public class GameEngineTests
    {
        private static GameEngine NewGame(ScriptedDice dice, int players = 2, RulesConfig rules = null, BoardDefinition board = null)
        {
            return new GameEngine(board ?? TestBoard(), rules ?? TestRules(), TestSeats(players), dice);
        }

        private static CommandResult Accept(GameEngine engine, GameCommand command)
        {
            CommandResult result = engine.Execute(command);
            Assert.IsTrue(result.IsAccepted, $"{command.GetType().Name} by {command.PlayerId} was rejected: {result.Reason}");
            return result;
        }

        private static PlayerState P(GameEngine engine, int id) => engine.State.FindPlayer(id);

        [Test]
        public void LandingOnTaxPaysTheBankAndEndsTheMove()
        {
            var engine = NewGame(new ScriptedDice(1, 2));

            CommandResult result = Accept(engine, new RollDiceCommand(Alice));

            Assert.AreEqual(3, P(engine, Alice).Position);
            Assert.AreEqual(900, P(engine, Alice).Money);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
            CollectionAssert.AreEqual(
                new[] { typeof(DiceRolledEvent), typeof(PlayerMovedEvent), typeof(MoneyChangedEvent), typeof(PhaseChangedEvent) },
                result.Events.Select(e => e.GetType()).ToArray());
            var tax = (MoneyChangedEvent)result.Events[2];
            Assert.AreEqual(-100, tax.Delta);
            Assert.AreEqual(MoneyReason.Tax, tax.Reason);
        }

        [Test]
        public void BuyingAPropertyTransfersOwnershipAndMoney()
        {
            var engine = NewGame(new ScriptedDice(2, 3));

            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.AwaitingBuyDecision, engine.State.Phase);

            Accept(engine, new BuyPropertyCommand(Alice));

            Assert.AreEqual(Alice, engine.State.GetOwner(5));
            Assert.AreEqual(880, P(engine, Alice).Money);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
        }

        [Test]
        public void ADeclinedSquareStaysWithTheBankAndTheMoveGoesOn()
        {
            var engine = NewGame(new ScriptedDice(2, 3));

            Accept(engine, new RollDiceCommand(Alice));
            CommandResult declined = Accept(engine, new DeclinePurchaseCommand(Alice));

            Assert.IsFalse(declined.Events.OfType<PropertyOwnerChangedEvent>().Any(), "Nobody gets the square.");
            Assert.IsFalse(declined.Events.OfType<MoneyChangedEvent>().Any(), "Nothing is paid.");
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(5));
            Assert.AreEqual(1000, P(engine, Alice).Money);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
        }

        [Test]
        public void UnaffordablePropertyIsOfferedButCannotBeBoughtWithoutMoney()
        {
            var engine = NewGame(new ScriptedDice(2, 3), rules: TestRules(startingMoney: 100));

            Accept(engine, new RollDiceCommand(Alice));

            Assert.AreEqual(GamePhase.AwaitingBuyDecision, engine.State.Phase);
            Assert.AreEqual(RejectionReason.InsufficientFunds, engine.Execute(new BuyPropertyCommand(Alice)).Reason);
        }

        [Test]
        public void LandingOnAnotherPlayersPropertyPaysRent()
        {
            var engine = NewGame(new ScriptedDice(2, 3, 2, 3));

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new BuyPropertyCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));
            Accept(engine, new RollDiceCommand(Bob));

            Assert.AreEqual(980, P(engine, Bob).Money);
            Assert.AreEqual(900, P(engine, Alice).Money);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
        }

        [Test]
        public void OwningTheWholeGroupMultipliesRent()
        {
            // Alice: 1+1 to A2 (buy, double), 5+6 around the board to A1 (buy). Bob: 1+1 to A2.
            var engine = NewGame(new ScriptedDice(1, 1, 5, 6, 1, 1));

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new BuyPropertyCommand(Alice));
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "A double earns another roll.");
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(1, P(engine, Alice).Position);
            Accept(engine, new BuyPropertyCommand(Alice));
            Assert.AreEqual(1000 - 80 + 200 - 60, P(engine, Alice).Money);
            Accept(engine, new EndTurnCommand(Alice));

            Assert.AreEqual(24, engine.CalculateRent(2));
            Accept(engine, new RollDiceCommand(Bob));

            Assert.AreEqual(1000 - 24, P(engine, Bob).Money);
            Assert.AreEqual(1060 + 24, P(engine, Alice).Money);
        }

        [Test]
        public void LandingOnOwnPropertyCostsNothing()
        {
            // Alice: 1+1 to A2 (buy), then 6+6 around to A2 again, then third roll.
            var engine = NewGame(new ScriptedDice(1, 1, 6, 6, 1, 2));

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new BuyPropertyCommand(Alice));
            Accept(engine, new RollDiceCommand(Alice));

            Assert.AreEqual(2, P(engine, Alice).Position);
            Assert.AreEqual(1000 - 80 + 200, P(engine, Alice).Money);
        }

        [Test]
        public void ThirdDoubleForfeitsTheMove()
        {
            var engine = NewGame(new ScriptedDice(1, 1, 2, 2, 3, 3));

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new DeclinePurchaseCommand(Alice));
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(6, P(engine, Alice).Position);
            Accept(engine, new DeclinePurchaseCommand(Alice));
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase);

            CommandResult third = Accept(engine, new RollDiceCommand(Alice));

            Assert.AreEqual(6, P(engine, Alice).Position, "The third double does not move the pawn.");
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
            Assert.IsFalse(third.Events.OfType<PlayerMovedEvent>().Any());
        }

        [Test]
        public void LandingExactlyOnStartPaysTheBonus()
        {
            var engine = NewGame(new ScriptedDice(1, 2, 1, 2, 4, 5));

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));
            Accept(engine, new RollDiceCommand(Bob));
            Accept(engine, new EndTurnCommand(Bob));
            CommandResult result = Accept(engine, new RollDiceCommand(Alice));

            Assert.AreEqual(0, P(engine, Alice).Position);
            Assert.AreEqual(900 + 200, P(engine, Alice).Money);
            Assert.IsTrue(result.Events.OfType<PlayerMovedEvent>().Single().PassedStart);
        }

        [Test]
        public void EndTurnPassesToTheNextPlayer()
        {
            var engine = NewGame(new ScriptedDice(1, 2), players: 3);

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));

            Assert.AreEqual(Bob, engine.State.CurrentPlayer.Id);
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase);
            Assert.AreEqual(2, engine.State.TurnNumber);
            Assert.AreEqual(0, engine.State.LastDie1);
        }

        [Test]
        public void IllegalCommandsAreRejectedWithoutChangingState()
        {
            var engine = NewGame(new ScriptedDice());
            ulong before = engine.State.ComputeHash();

            Assert.AreEqual(RejectionReason.NotYourTurn, engine.Execute(new RollDiceCommand(Bob)).Reason);
            Assert.AreEqual(RejectionReason.UnknownPlayer, engine.Execute(new RollDiceCommand(99)).Reason);
            Assert.AreEqual(RejectionReason.WrongPhase, engine.Execute(new EndTurnCommand(Alice)).Reason);
            Assert.AreEqual(RejectionReason.WrongPhase, engine.Execute(new BuyPropertyCommand(Alice)).Reason);
            Assert.AreEqual(RejectionReason.WrongPhase, engine.Execute(new DeclinePurchaseCommand(Alice)).Reason);

            CommandResult rejected = engine.Execute(new RollDiceCommand(Bob));
            Assert.IsEmpty(rejected.Events);
            Assert.AreEqual(before, engine.State.ComputeHash());
        }

        [Test]
        public void ARentNothingCouldCoverBankruptsAtOnceAndEndsTwoPlayerGame()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Rest("Rest"),
                SquareDefinition.Property("Pricey", 50, 500, 0),
                SquareDefinition.Rest("Rest"),
            });
            var rules = new RulesConfig(100, 0, 6, 3, 2, 50);
            // Alice 1+1 buys Pricey, rolls again 1+2. Bob 1+1 lands on Pricey and cannot pay.
            var engine = NewGame(new ScriptedDice(1, 1, 1, 2, 1, 1), rules: rules, board: board);

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new BuyPropertyCommand(Alice));
            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));
            // Bob holds 100 and nothing to sell: the rent of 500 ends his game on the roll, with no debt screen.
            CommandResult result = Accept(engine, new RollDiceCommand(Bob));
            DebtIncurredEvent debt = result.Events.OfType<DebtIncurredEvent>().Single();
            Assert.AreEqual(500, debt.Amount);
            Assert.AreEqual(MoneyReason.Rent, debt.Reason);
            Assert.IsFalse(result.Events.OfType<PhaseChangedEvent>().Any(e => e.Phase == GamePhase.AwaitingDebtPayment));

            Assert.IsTrue(P(engine, Bob).IsBankrupt);
            Assert.IsNull(engine.State.Debt);
            Assert.AreEqual(0, P(engine, Bob).Money);
            Assert.AreEqual(50 + 100, P(engine, Alice).Money);
            Assert.AreEqual(GamePhase.GameOver, engine.State.Phase);
            Assert.AreEqual(Alice, engine.State.WinnerId);
            Assert.AreEqual(Alice, result.Events.OfType<GameEndedEvent>().Single().WinnerId);
            Assert.AreEqual(RejectionReason.GameOver, engine.Execute(new EndTurnCommand(Bob)).Reason);
        }

        [Test]
        public void BankruptcyInALargerGameSellsThePropertiesAndContinues()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Rest("Rest 1"),
                SquareDefinition.Property("Pricey", 50, 500, 0),
                SquareDefinition.Rest("Rest 2"),
                SquareDefinition.Property("Cheap", 40, 10, 0),
                SquareDefinition.Rest("Rest 3"),
            });
            var rules = new RulesConfig(100, 0, 6, 3, 2, 50);
            var engine = NewGame(new ScriptedDice(1, 1, 1, 2, 1, 3, 1, 2, 2, 4, 1, 3), players: 3, rules: rules, board: board);

            Accept(engine, new RollDiceCommand(Alice));      // 1+1 -> Pricey
            Accept(engine, new BuyPropertyCommand(Alice));
            Accept(engine, new RollDiceCommand(Alice));      // 1+2 -> Rest 3
            Accept(engine, new EndTurnCommand(Alice));
            Accept(engine, new RollDiceCommand(Bob));        // 1+3 -> Cheap
            Accept(engine, new BuyPropertyCommand(Bob));
            Accept(engine, new EndTurnCommand(Bob));
            Accept(engine, new RollDiceCommand(Cem));        // 1+2 -> Rest 2
            Accept(engine, new EndTurnCommand(Cem));
            Accept(engine, new RollDiceCommand(Alice));      // 2+4 -> back to Rest 3
            Accept(engine, new EndTurnCommand(Alice));
            Accept(engine, new RollDiceCommand(Bob));        // 1+3 -> Pricey, rent 500: 60 and Cheap (20) cannot cover it

            Assert.IsTrue(P(engine, Bob).IsBankrupt);
            Assert.AreEqual(50 + 60 + 20, P(engine, Alice).Money, "Bob's 60 and the 20 the bank paid for Cheap, short of the rent.");
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(4), "Bob's property is sold to the bank, not handed to his creditor.");
            Assert.AreEqual(Cem, engine.State.CurrentPlayer.Id, "Play continues with the next solvent player.");
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase);
            Assert.AreEqual(2, engine.State.ActivePlayerCount());
        }

        [Test]
        public void BankruptcyToTheBankReturnsPropertiesToTheBank()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Rest("Rest"),
                SquareDefinition.Property("Cheap", 40, 10, 0),
                SquareDefinition.Rest("Rest"),
                SquareDefinition.Tax("Ruin", 500),
                SquareDefinition.Rest("Rest"),
            });
            var rules = new RulesConfig(100, 0, 6, 3, 2, 50);
            // Alice 1+1 buys Cheap, then 1+1 again onto Ruin.
            var engine = NewGame(new ScriptedDice(1, 1, 1, 1), players: 3, rules: rules, board: board);

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new BuyPropertyCommand(Alice));
            Accept(engine, new RollDiceCommand(Alice));      // Ruin takes 500; 60 and Cheap (20) cannot cover it

            Assert.IsTrue(P(engine, Alice).IsBankrupt);
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(2));
            Assert.AreEqual(Bob, engine.State.CurrentPlayer.Id);
            Assert.AreEqual(100, P(engine, Bob).Money, "Money paid to the bank goes to nobody.");
        }
    }
}
