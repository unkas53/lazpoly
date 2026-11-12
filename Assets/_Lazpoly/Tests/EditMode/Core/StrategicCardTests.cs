using System;
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
    /// <summary>
    /// Faz 30, the owner's plan: the strategic cards are kept in hand (at most four) and played one a turn before the roll;
    /// attacks hit a rival's town unless a shield stands there, which stops the first one and comes down.
    /// </summary>
    public class StrategicCardTests
    {
        // 0 Start, 1 A1, 2 A2 (group 1), 3 Card, 4 Rest, 5 Jail (fee 50), 6 B1, 7 Tax 100, 8 Cup (50, 50% +50%, up to 500%),
        // 9 Resort, 10 B2 (group 2), 11-23 Rest.
        private const int A1 = 1;
        private const int A2 = 2;
        private const int CardSquare = 3;
        private const int JailSquare = 5;
        private const int B1 = 6;
        private const int TaxSquare = 7;
        private const int CupSquare = 8;
        private const int ResortSquare = 9;
        private const int B2 = 10;
        private const int Far = 11;

        // The deck, one card of each kind.
        private const int Prize = 0;
        private const int Lawyer = 1;
        private const int RentShield = 2;
        private const int Chauffeur = 3;
        private const int CupTransfer = 4;
        private const int Spyglass = 5;
        private const int LandDeal = 6;
        private const int Renewal = 7;
        private const int Fund = 8;
        private const int Shield = 9;
        private const int Strike = 10;
        private const int Audit = 11;
        private const int Demolish = 12;
        private const int IllegalFloor = 13;
        private const int ForceSale = 14;

        private static BoardDefinition Board()
        {
            SquareDefinition Dev(string name, int price, int rent, int group) =>
                SquareDefinition.Property(name, price, rent, group, 50, new[] { rent * 5, rent * 15, rent * 40, rent * 55, rent * 70 });

            var squares = new SquareDefinition[24];
            for (int i = 0; i < squares.Length; i++) squares[i] = SquareDefinition.Rest($"Rest {i}");
            squares[0] = SquareDefinition.Start("Start");
            squares[A1] = Dev("A1", 100, 10, 1);
            squares[A2] = Dev("A2", 100, 10, 1);
            squares[CardSquare] = SquareDefinition.Card("Chance");
            squares[JailSquare] = SquareDefinition.Jail("Jail", 50);
            squares[B1] = Dev("B1", 200, 20, 2);
            squares[TaxSquare] = SquareDefinition.Tax("Tax", 100);
            squares[CupSquare] = SquareDefinition.Cup("Cup", 50, 50, 50, 500);
            squares[ResortSquare] = SquareDefinition.Resort("Resort", 200, 25, 0);
            squares[B2] = Dev("B2", 200, 20, 2);

            var cards = new[]
            {
                CardDefinition.Collect("Prize", 10),
                CardDefinition.Strategic("Lawyer", CardKind.Lawyer),
                CardDefinition.Strategic("Rent shield", CardKind.RentShield),
                CardDefinition.Strategic("Chauffeur", CardKind.Chauffeur),
                CardDefinition.Strategic("Cup transfer", CardKind.CupTransfer),
                CardDefinition.Lookout("Spyglass"),
                CardDefinition.Strategic("Land deal", CardKind.LandDeal, 25),
                CardDefinition.Strategic("Renewal", CardKind.Renewal, 50),
                CardDefinition.Fund("Fund", 30, 40, 100),
                CardDefinition.Strategic("Shield", CardKind.Shield),
                CardDefinition.Strategic("Strike", CardKind.Strike, 3),
                CardDefinition.Strategic("Audit", CardKind.Audit, 5),
                CardDefinition.Strategic("Demolish", CardKind.Demolish),
                CardDefinition.Strategic("Illegal floor", CardKind.IllegalFloor),
                CardDefinition.Strategic("Force sale", CardKind.ForceSale),
            };
            return new BoardDefinition(squares, cards);
        }

        private static readonly RulesConfig Rules = TestRules();

        /// <summary>A two-player game set up through a snapshot, then played on with scripted dice; Alice is on turn.</summary>
        private static GameEngine Game(Action<GameStateSnapshot> setup, params int[] dice)
        {
            BoardDefinition board = Board();
            var fresh = new GameEngine(board, Rules, TestSeats(2), new ScriptedDice());
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(fresh.State);
            foreach (GameStateSnapshot.PlayerEntry player in snapshot.Players) player.Position = Far;
            setup(snapshot);
            return GameEngine.Restore(board, Rules, snapshot, new ScriptedDice(dice));
        }

        private static GameStateSnapshot.PlayerEntry Seat(GameStateSnapshot s, int id) => s.Players.Single(p => p.Id == id);

        private static GameEngine AliceHolds(params int[] cards) => Game(s => Seat(s, Alice).Hand.AddRange(cards));

        private static CommandResult Accept(GameEngine engine, GameCommand command)
        {
            CommandResult result = engine.Execute(command);
            Assert.IsTrue(result.IsAccepted, $"{command.GetType().Name} by {command.PlayerId} was rejected: {result.Reason}");
            return result;
        }

        private static RejectionReason Reason(GameEngine engine, GameCommand command)
        {
            ulong before = engine.State.ComputeHash();
            CommandResult result = engine.Execute(command);
            Assert.IsFalse(result.IsAccepted, $"{command.GetType().Name} was accepted.");
            Assert.AreEqual(before, engine.State.ComputeHash(), "A rejected command changed the state.");
            return result.Reason;
        }

        private static PlayerState Player(GameEngine engine, int id) => engine.State.FindPlayer(id);

        private static int Rent(GameEngine engine, int square) => GameRules.CalculateRent(engine.Board, engine.Rules, engine.State, square);

        // ---- The hand ----

        [Test]
        public void AKeptCardGoesIntoTheHandAndOutOfTheDeck()
        {
            GameEngine engine = Game(s => Seat(s, Alice).Position = 0, 1, 2, Shield);
            CommandResult result = Accept(engine, new RollDiceCommand(Alice));

            Assert.IsTrue(result.Events.OfType<CardDrawnEvent>().Single().Kept);
            CollectionAssert.AreEqual(new[] { Shield }, Player(engine, Alice).Hand);
            Assert.AreEqual(GamePhase.AwaitingCardUse, engine.State.Phase, "the player decides on the spot: use it or keep it");
            Assert.AreEqual(Shield, GameRules.DrawnCard(engine.State));
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new KeepCardCommand(Bob)));
            Accept(engine, new KeepCardCommand(Alice));
            CollectionAssert.AreEqual(new[] { Shield }, Player(engine, Alice).Hand);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "kept: nothing happens now");
            Assert.AreEqual(-1, GameRules.DrawnCard(engine.State));
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new KeepCardCommand(Alice)));
            CollectionAssert.DoesNotContain(GameRules.DrawableCards(engine.Board, engine.State), Shield, "out of the deck while kept");
            Assert.AreEqual(engine.Board.Cards.Count - 1, GameRules.DrawableCards(engine.Board, engine.State).Count);
        }

        [Test]
        public void AFifthCardMeansLettingOneGo()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Position = 0;
                Seat(s, Alice).Hand.AddRange(new[] { Lawyer, Chauffeur, Fund, Audit });
            }, 1, 2, 6);

            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.AwaitingCardUse, engine.State.Phase);
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new DiscardCardCommand(Alice, Chauffeur)), "first the drawn card: use or keep");
            Accept(engine, new KeepCardCommand(Alice));
            Assert.AreEqual(GamePhase.AwaitingCardDiscard, engine.State.Phase);
            Assert.AreEqual(GameRules.HandLimit + 1, Player(engine, Alice).Hand.Count);
            Assert.AreEqual(Shield, Player(engine, Alice).Hand.Last(), "the drawable deck skips the four held cards");

            Assert.AreEqual(RejectionReason.CardNotHeld, Reason(engine, new DiscardCardCommand(Alice, Strike)));
            Accept(engine, new DiscardCardCommand(Alice, Chauffeur));
            CollectionAssert.AreEqual(new[] { Lawyer, Fund, Audit, Shield }, Player(engine, Alice).Hand);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
            CollectionAssert.Contains(GameRules.DrawableCards(engine.Board, engine.State), Chauffeur, "back in the deck");
        }

        // ---- Use it or keep it, as it is drawn (the owner's wish, 2026-11-12) ----

        [Test]
        public void ADrawnCardCanBeUsedOnTheSpot()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Position = 0;
                s.Owners[B1] = Bob;
            }, 1, 2, ForceSale);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.AwaitingCardUse, engine.State.Phase);
            CollectionAssert.AreEqual(new[] { B1 }, GameRules.CardTargets(engine.Board, engine.State, Player(engine, Alice), ForceSale));
            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, ForceSale, A1)), "nobody's town");

            int before = Player(engine, Bob).Money;
            CommandResult used = Accept(engine, new PlayCardCommand(Alice, ForceSale, B1));
            Assert.AreEqual(ForceSale, used.Events.OfType<CardPlayedEvent>().Single().CardIndex);
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(B1));
            Assert.Greater(Player(engine, Bob).Money, before, "the town's worth went to its owner");
            Assert.AreEqual(0, Player(engine, Alice).Hand.Count, "used, so it never stayed in the hand");
            Assert.IsTrue(engine.State.CardPlayedThisTurn, "it is the turn's one card");
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "the move it was drawn on is over");
            CollectionAssert.Contains(GameRules.DrawableCards(engine.Board, engine.State), ForceSale, "back in the deck");
        }

        [Test]
        public void OnlyTheDrawnCardAndOnlyOneCardATurn()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Position = 0;
                Seat(s, Alice).Hand.Add(Audit);
            }, 1, 2, Fund);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(Fund, GameRules.DrawnCard(engine.State), "the drawable deck skips the held audit");
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new PlayCardCommand(Alice, Audit, Bob)), "an older card waits for the start of a turn");
            Accept(engine, new PlayCardCommand(Alice, Fund));
            Assert.AreEqual(1030, Player(engine, Alice).Money, "the fund's cash");
            CollectionAssert.AreEqual(new[] { Audit }, Player(engine, Alice).Hand);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);

            // A card played before the roll was the turn's card: one drawn after it can only be kept.
            GameEngine played = Game(s =>
            {
                Seat(s, Alice).Position = 0;
                Seat(s, Alice).Hand.Add(Fund);
                s.Owners[A1] = Alice;
            }, 1, 2, Shield);
            Accept(played, new PlayCardCommand(Alice, Fund));
            Accept(played, new RollDiceCommand(Alice));
            Assert.AreEqual(Shield, GameRules.DrawnCard(played.State));
            Assert.AreEqual(RejectionReason.CardAlreadyPlayed, Reason(played, new PlayCardCommand(Alice, Shield, A1)));
            Accept(played, new KeepCardCommand(Alice));
            CollectionAssert.AreEqual(new[] { Shield }, Player(played, Alice).Hand);
        }

        [Test]
        public void CardsWithAMomentOfTheirOwnCanOnlyBeKept()
        {
            foreach (int card in new[] { Lawyer, RentShield, Chauffeur })
            {
                GameEngine engine = Game(s => Seat(s, Alice).Position = 0, 1, 2, card);
                Accept(engine, new RollDiceCommand(Alice));
                Assert.AreEqual(card, GameRules.DrawnCard(engine.State));
                Assert.IsFalse(GameRules.UsedAtDraw(engine.Board.Cards[card].Kind));
                Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new PlayCardCommand(Alice, card, card == Chauffeur ? A1 : -1)));
                Assert.AreEqual(0, GameRules.CardTargets(engine.Board, engine.State, Player(engine, Alice), card).Count);
                Accept(engine, new KeepCardCommand(Alice));
                CollectionAssert.AreEqual(new[] { card }, Player(engine, Alice).Hand);
            }

            GameEngine spy = Game(s => Seat(s, Alice).Position = 0, 1, 2, Spyglass);
            Accept(spy, new RollDiceCommand(Alice));
            Assert.IsTrue(GameRules.UsedAtDraw(CardKind.Lookout));
            Accept(spy, new PlayCardCommand(Alice, Spyglass));
            Assert.IsTrue(Player(spy, Alice).HasLookout, "the lookout waits for the next roll");
            Assert.AreEqual(GamePhase.TurnEnd, spy.State.Phase);
        }

        [Test]
        public void ADoubleRollsAgainWhetherTheCardIsUsedOrKept()
        {
            foreach (bool use in new[] { true, false })
            {
                GameEngine engine = Game(s => Seat(s, Alice).Position = A1, 1, 1, Fund);
                Accept(engine, new RollDiceCommand(Alice));
                Assert.AreEqual(GamePhase.AwaitingCardUse, engine.State.Phase);
                Accept(engine, use ? (GameCommand)new PlayCardCommand(Alice, Fund, -1, 1) : new KeepCardCommand(Alice));
                Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "the double's second roll");
                Assert.AreEqual(use ? 100 : 0, Player(engine, Alice).FundPayout);
                Assert.AreEqual(use ? 960 : 1000, Player(engine, Alice).Money);
            }
        }

        [Test]
        public void AFifthCardUsedOnTheSpotLetsNothingGo()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Position = 0;
                Seat(s, Alice).Hand.AddRange(new[] { Lawyer, Chauffeur, Fund, Audit });
                s.Owners[A1] = Alice;
            }, 1, 2, 6);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(Shield, GameRules.DrawnCard(engine.State));
            Accept(engine, new PlayCardCommand(Alice, Shield, A1));
            Assert.IsTrue(engine.State.HasShield(A1));
            CollectionAssert.AreEqual(new[] { Lawyer, Chauffeur, Fund, Audit }, Player(engine, Alice).Hand);
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "the hand is at its limit again: nothing to let go");
        }

        [Test]
        public void OneCardATurnAndOnlyBeforeTheRoll()
        {
            GameEngine engine = Game(s => Seat(s, Alice).Hand.AddRange(new[] { Fund, Audit }), 1, 2);

            Accept(engine, new PlayCardCommand(Alice, Fund));
            Assert.AreEqual(1030, Player(engine, Alice).Money, "the fund's cash");
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "the turn goes on with the roll");
            Assert.AreEqual(RejectionReason.CardAlreadyPlayed, Reason(engine, new PlayCardCommand(Alice, Audit, Bob)));
            Assert.AreEqual(RejectionReason.NotYourTurn, Reason(engine, new PlayCardCommand(Bob, Audit, Alice)));
            Assert.AreEqual(RejectionReason.CardNotHeld, Reason(engine, new PlayCardCommand(Alice, Fund)), "it left the hand");

            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));
            Assert.IsFalse(engine.State.CardPlayedThisTurn, "a new turn, a new card");
        }

        [Test]
        public void NoCardAfterTheRoll()
        {
            GameEngine engine = Game(s => Seat(s, Alice).Hand.Add(Audit), 1, 1, 1, 2);
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase, "a double rolls again");
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new PlayCardCommand(Alice, Audit, Bob)));
        }

        [Test]
        public void ABankruptPlayersCardsGoBackToTheDeck()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Money = 5;
                Seat(s, Alice).Position = 22;
                Seat(s, Alice).Hand.Add(Shield);
                s.Owners[A1] = Bob;
                s.BuildingLevels[A1] = 5;
            }, 1, 2);

            Accept(engine, new RollDiceCommand(Alice));
            Assert.IsTrue(Player(engine, Alice).IsBankrupt);
            Assert.AreEqual(0, Player(engine, Alice).Hand.Count);
            CollectionAssert.Contains(GameRules.DrawableCards(engine.Board, engine.State), Shield);
        }

        // ---- Cards for oneself ----

        [Test]
        public void TheLawyerOpensTheJail()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Lawyer);
                Seat(s, Alice).Position = JailSquare;
                Seat(s, Alice).JailTurns = 3;
                s.Phase = GamePhase.AwaitingJailDecision;
            });

            Accept(engine, new PlayCardCommand(Alice, Lawyer));
            Assert.IsFalse(Player(engine, Alice).InJail);
            Assert.AreEqual(1000, Player(engine, Alice).Money, "free");
            Assert.AreEqual(GamePhase.WaitingForRoll, engine.State.Phase);

            GameEngine outside = AliceHolds(Lawyer);
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(outside, new PlayCardCommand(Alice, Lawyer)), "only in jail");
        }

        [Test]
        public void TheRentShieldIsAskedWhenRentFallsDue()
        {
            Action<GameStateSnapshot> setup = s =>
            {
                Seat(s, Alice).Position = 22;
                Seat(s, Alice).Hand.Add(RentShield);
                s.Owners[A1] = Bob;
            };

            GameEngine shielded = Game(setup, 1, 2);
            Assert.AreEqual(RejectionReason.WrongPhase, Reason(shielded, new PlayCardCommand(Alice, RentShield)), "no rent yet");
            Accept(shielded, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.AwaitingRentShield, shielded.State.Phase);
            Assert.AreEqual(10, shielded.State.RentDue.Amount);
            Accept(shielded, new PlayCardCommand(Alice, RentShield));
            Assert.AreEqual(1200, Player(shielded, Alice).Money, "only the Start bonus");
            Assert.AreEqual(1000, Player(shielded, Bob).Money);
            Assert.IsNull(shielded.State.RentDue);
            Assert.AreEqual(GamePhase.TurnEnd, shielded.State.Phase);

            GameEngine paid = Game(setup, 1, 2);
            Accept(paid, new RollDiceCommand(Alice));
            Accept(paid, new PayRentCommand(Alice));
            Assert.AreEqual(1190, Player(paid, Alice).Money);
            Assert.AreEqual(1010, Player(paid, Bob).Money);
            CollectionAssert.AreEqual(new[] { RentShield }, Player(paid, Alice).Hand, "kept for later");
        }

        [Test]
        public void NoRentShieldOnceTheTurnsCardIsPlayed()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Position = 22;
                Seat(s, Alice).Hand.AddRange(new[] { RentShield, Fund });
                s.Owners[A1] = Bob;
            }, 1, 2);

            Accept(engine, new PlayCardCommand(Alice, Fund));
            Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(GamePhase.AwaitingTakeoverDecision, engine.State.Phase, "the rent was simply paid; the town may be taken over (Faz 31)");
            Assert.AreEqual(1030 + 200 - 10, Player(engine, Alice).Money);
            Accept(engine, new DeclineTakeoverCommand(Alice));
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase);
        }

        [Test]
        public void TheChauffeurIsTheTurnsMove()
        {
            GameEngine engine = Game(s => Seat(s, Alice).Hand.Add(Chauffeur));
            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, Chauffeur, JailSquare)));
            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, Chauffeur, TaxSquare)));
            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, Chauffeur, Far)), "already there");

            CommandResult ride = Accept(engine, new PlayCardCommand(Alice, Chauffeur, A2));
            Assert.AreEqual(A2, Player(engine, Alice).Position);
            Assert.IsTrue(ride.Events.OfType<PlayerMovedEvent>().Single().PassedStart);
            Assert.AreEqual(1200, Player(engine, Alice).Money, "passing Start pays");
            Assert.AreEqual(GamePhase.AwaitingBuyDecision, engine.State.Phase);
            Accept(engine, new DeclinePurchaseCommand(Alice));
            Assert.AreEqual(GamePhase.TurnEnd, engine.State.Phase, "no roll after the ride");
        }

        [Test]
        public void TheCupTransferMovesTheCupAtItsRaise()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(CupTransfer);
                s.Owners[A1] = Alice;
                s.Owners[B1] = Bob;
                s.CupSquare = B1;
                s.CupPercent = 100;
                s.CupPicks = 2;
            });

            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, CupTransfer, B1)), "not a rival's town");
            Accept(engine, new PlayCardCommand(Alice, CupTransfer, A1));
            Assert.AreEqual(A1, engine.State.CupSquare);
            Assert.AreEqual(100, engine.State.CupPercent);
            Assert.AreEqual(2, engine.State.CupPicks, "a move, not a pick");
            Assert.AreEqual(1000, Player(engine, Alice).Money, "no fee");

            GameEngine first = Game(s =>
            {
                Seat(s, Alice).Hand.Add(CupTransfer);
                s.Owners[A1] = Alice;
            });
            Accept(first, new PlayCardCommand(Alice, CupTransfer, A1));
            Assert.AreEqual(50, first.State.CupPercent, "no cup yet: it starts at the first raise");
            Assert.AreEqual(1, first.State.CupPicks);
        }

        [Test]
        public void TheSpyglassIsPlayedForTheComingRoll()
        {
            GameEngine engine = AliceHolds(Spyglass);
            Accept(engine, new PlayCardCommand(Alice, Spyglass));
            Assert.IsTrue(Player(engine, Alice).HasLookout);
        }

        [Test]
        public void TheLandDealBuysAFreeTownAQuarterCheaper()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(LandDeal);
                s.Owners[A1] = Bob;
            });

            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, LandDeal, A1)), "owned");
            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, LandDeal, CardSquare)));
            Accept(engine, new PlayCardCommand(Alice, LandDeal, ResortSquare));
            Assert.AreEqual(Alice, engine.State.GetOwner(ResortSquare), "a resort may be bought too");
            Assert.AreEqual(1000 - 150, Player(engine, Alice).Money);
        }

        [Test]
        public void TheRenewalBuildsAtHalfPriceWithinTheUsualLimits()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Renewal);
                s.Owners[A1] = Alice;
            });

            Accept(engine, new PlayCardCommand(Alice, Renewal, A1));
            Assert.AreEqual(1, engine.State.GetBuildingLevel(A1));
            Assert.AreEqual(1000 - 25, Player(engine, Alice).Money);

            GameEngine firstLap = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Renewal);
                s.Owners[A1] = Alice;
                s.BuildingLevels[A1] = 1;
            });
            Assert.AreEqual(RejectionReason.CannotBuild, Reason(firstLap, new PlayCardCommand(Alice, Renewal, A1)), "one house before the first lap");

            GameEngine hotel = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Renewal);
                Seat(s, Alice).Laps = 1;
                s.Owners[A1] = Alice;
                s.BuildingLevels[A1] = 4;
            });
            Accept(hotel, new PlayCardCommand(Alice, Renewal, A1));
            Assert.AreEqual(SquareDefinition.MaxBuildingLevel, hotel.State.GetBuildingLevel(A1), "four houses take the hotel");
            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(AliceHolds(Renewal), new PlayCardCommand(Alice, Renewal, ResortSquare)));
        }

        [Test]
        public void TheFundPaysOutAtStart()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Fund);
                Seat(s, Alice).Position = 22;
            }, 1, 2);

            Accept(engine, new PlayCardCommand(Alice, Fund, -1, 1));
            Assert.AreEqual(960, Player(engine, Alice).Money);
            Assert.AreEqual(100, Player(engine, Alice).FundPayout);

            CommandResult roll = Accept(engine, new RollDiceCommand(Alice));
            Assert.AreEqual(960 + 200 + 100, Player(engine, Alice).Money);
            Assert.IsTrue(roll.Events.OfType<MoneyChangedEvent>().Any(e => e.Reason == MoneyReason.Fund && e.Delta == 100));
            Assert.AreEqual(0, Player(engine, Alice).FundPayout);

            GameEngine poor = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Fund);
                Seat(s, Alice).Money = 30;
            });
            Assert.AreEqual(RejectionReason.InsufficientFunds, Reason(poor, new PlayCardCommand(Alice, Fund, -1, 1)));
            Assert.AreEqual(RejectionReason.InvalidChoice, Reason(poor, new PlayCardCommand(Alice, Fund, -1, 2)));
        }

        // ---- Attacks and the shield ----

        [Test]
        public void TheShieldStopsTheFirstAttackAndComesDown()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Shield);
                Seat(s, Bob).Hand.AddRange(new[] { Demolish, IllegalFloor });
                s.Owners[A1] = Alice;
                s.BuildingLevels[A1] = 2;
            }, 1, 2, 1, 2, 1, 2);

            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, Shield, B1)), "only on one's own town");
            Accept(engine, new PlayCardCommand(Alice, Shield, A1));
            Assert.IsTrue(engine.State.HasShield(A1));
            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));

            CommandResult stopped = Accept(engine, new PlayCardCommand(Bob, Demolish, A1));
            Assert.IsTrue(stopped.Events.OfType<ShieldChangedEvent>().Single().Blocked);
            Assert.IsFalse(engine.State.HasShield(A1), "it comes down after the attack");
            Assert.AreEqual(Alice, engine.State.GetOwner(A1));
            Assert.AreEqual(2, engine.State.GetBuildingLevel(A1));
            CollectionAssert.DoesNotContain(Player(engine, Bob).Hand, Demolish, "the attacker's card is spent");
            Accept(engine, new RollDiceCommand(Bob));
            Accept(engine, new EndTurnCommand(Bob));
            Accept(engine, new RollDiceCommand(Alice));
            Accept(engine, new EndTurnCommand(Alice));

            Accept(engine, new PlayCardCommand(Bob, IllegalFloor, A1));
            Assert.AreEqual(1, engine.State.GetBuildingLevel(A1), "the next attack hits");
        }

        [Test]
        public void AShieldAndAStrikeEndWithANewOwner()
        {
            GameEngine engine = Game(s =>
            {
                s.Owners[A1] = Alice;
                s.Shields = new bool[24];
                s.Shields[A1] = true;
                s.Strikers = Enumerable.Repeat(GameState.NoOwner, 24).ToArray();
                s.StrikeTurns = new int[24];
                s.Strikers[A1] = Bob;
                s.StrikeTurns[A1] = 2;
                s.Phase = GamePhase.AwaitingDebtPayment;
                s.Debt = new DebtState(Alice, Bob, 50, MoneyReason.Rent);
                Seat(s, Alice).Money = 0;
            });

            Accept(engine, new SellPropertyCommand(Alice, A1));
            Assert.IsFalse(engine.State.HasShield(A1));
            Assert.IsFalse(engine.State.IsStruck(A1));
        }

        [Test]
        public void TheStrikeLastsUntilTheStrikersThirdTurnAfterEnds()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Strike);
                s.Owners[B1] = Bob;
            }, Enumerable.Repeat(new[] { 1, 2 }, 8).SelectMany(d => d).ToArray());

            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, Strike, A1)), "nobody's town");
            Accept(engine, new PlayCardCommand(Alice, Strike, B1));
            Assert.AreEqual(0, Rent(engine, B1));

            for (int aliceTurn = 0; aliceTurn <= 3; aliceTurn++)
            {
                Assert.IsTrue(engine.State.IsStruck(B1), $"still on in Alice's turn {aliceTurn}");
                Accept(engine, new RollDiceCommand(Alice));
                Accept(engine, new EndTurnCommand(Alice));
                Assert.AreEqual(aliceTurn < 3, engine.State.IsStruck(B1), $"after Alice's turn {aliceTurn}");
                Accept(engine, new RollDiceCommand(Bob));
                Accept(engine, new EndTurnCommand(Bob));
            }

            Assert.AreEqual(20, Rent(engine, B1));
        }

        [Test]
        public void TheAuditTakesFivePerCentOfTheRivalsWorthAtMostTheirCash()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Audit);
                s.Owners[B1] = Bob;
            });

            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, Audit, Alice)));
            Accept(engine, new PlayCardCommand(Alice, Audit, Bob));
            Assert.AreEqual(1000 - 60, Player(engine, Bob).Money, "5% of 1000 cash and a 200 town");
            Assert.AreEqual(1000, Player(engine, Alice).Money, "to the bank");

            GameEngine poor = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Audit);
                Seat(s, Bob).Money = 10;
                s.Owners[B1] = Bob;
                s.Owners[B2] = Bob;
                s.BuildingLevels[B1] = 5;
            });
            Accept(poor, new PlayCardCommand(Alice, Audit, Bob));
            Assert.AreEqual(0, Player(poor, Bob).Money, "never more than the cash");
        }

        [Test]
        public void TheIllegalBuildingTearsDownATownWithoutAHotel()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(Demolish);
                s.Owners[B1] = Bob;
                s.Owners[B2] = Bob;
                s.BuildingLevels[B1] = 3;
                s.BuildingLevels[B2] = 5;
            });

            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, Demolish, B2)), "hotels are spared");
            Accept(engine, new PlayCardCommand(Alice, Demolish, B1));
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(B1));
            Assert.AreEqual(0, engine.State.GetBuildingLevel(B1));
            Assert.AreEqual(1000, Player(engine, Bob).Money, "nothing paid back");
        }

        [Test]
        public void TheIllegalFloorTakesOneLevelOffATownWithHouses()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(IllegalFloor);
                s.Owners[A1] = Bob;
                s.Owners[B1] = Bob;
                s.Owners[B2] = Bob;
                s.BuildingLevels[B1] = 3;
                s.BuildingLevels[B2] = 5;
            });

            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, IllegalFloor, A1)), "no houses");
            Assert.AreEqual(RejectionReason.InvalidTarget, Reason(engine, new PlayCardCommand(Alice, IllegalFloor, B2)), "hotels are spared");
            Accept(engine, new PlayCardCommand(Alice, IllegalFloor, B1));
            Assert.AreEqual(2, engine.State.GetBuildingLevel(B1));
            Assert.AreEqual(Bob, engine.State.GetOwner(B1));
            Assert.AreEqual(1000, Player(engine, Bob).Money);
        }

        [Test]
        public void TheForcedSaleSellsARivalTownAndPaysTheOwner()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.Add(ForceSale);
                s.Owners[B1] = Bob;
                s.BuildingLevels[B1] = 5;
            });

            int proceeds = GameRules.PropertySaleProceeds(engine.Board, engine.Rules, engine.State, B1);
            Accept(engine, new PlayCardCommand(Alice, ForceSale, B1));
            Assert.AreEqual(GameState.NoOwner, engine.State.GetOwner(B1));
            Assert.AreEqual(0, engine.State.GetBuildingLevel(B1));
            Assert.AreEqual(1000 + proceeds, Player(engine, Bob).Money, "a hotel town too, paid in full");
        }

        [Test]
        public void ACardMayBePlayedInJailButNotToMove()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.AddRange(new[] { Audit, Chauffeur });
                Seat(s, Alice).Position = JailSquare;
                Seat(s, Alice).JailTurns = 3;
                s.Phase = GamePhase.AwaitingJailDecision;
            });

            Assert.AreEqual(RejectionReason.WrongPhase, Reason(engine, new PlayCardCommand(Alice, Chauffeur, A1)));
            Accept(engine, new PlayCardCommand(Alice, Audit, Bob));
            Assert.AreEqual(GamePhase.AwaitingJailDecision, engine.State.Phase);
        }

        [Test]
        public void TheCardsSurviveASnapshot()
        {
            GameEngine engine = Game(s =>
            {
                Seat(s, Alice).Hand.AddRange(new[] { Shield, Strike });
                Seat(s, Bob).Hand.Add(Fund);
                s.Owners[A1] = Alice;
                s.Owners[B1] = Bob;
            });
            Accept(engine, new PlayCardCommand(Alice, Shield, A1));

            GameState copy = GameStateSnapshot.Capture(engine.State).Restore();
            Assert.AreEqual(engine.State.ComputeHash(), copy.ComputeHash());
            CollectionAssert.AreEqual(new[] { Strike }, copy.FindPlayer(Alice).Hand);
            Assert.IsTrue(copy.HasShield(A1));
            Assert.IsTrue(copy.CardPlayedThisTurn);
        }
    }
}
