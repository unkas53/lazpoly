using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class DebtSaleTests
    {
        private const int Cheap = 1;
        private const int Dear = 2;
        private const int Solo = 4;

        /// <summary>The player on turn holds 10 and three towns, and owes the other player <paramref name="amount"/> in rent.</summary>
        private static GameEngine Owing(int amount, out int me)
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Start"),
                SquareDefinition.Property("Cheap", 60, 6, 1, 50, new[] { 30, 90, 240, 330, 420 }),
                SquareDefinition.Property("Dear", 200, 20, 1, 50, new[] { 100, 300, 800, 1100, 1400 }),
                SquareDefinition.Rest("Rest"),
                SquareDefinition.Property("Solo", 100, 10, 0),
            });
            var engine = new GameEngine(board, new RulesConfig(1000, 0, 6, 3, 2, 50),
                new[] { new PlayerSeat(1, "Ada"), new PlayerSeat(2, "Can") }, new Pcg32Random(1UL));
            me = engine.State.CurrentPlayer.Id;
            int other = me == 1 ? 2 : 1;
            foreach (int town in new[] { Cheap, Dear, Solo })
            {
                GameStateReducer.Apply(engine.State, new PropertyOwnerChangedEvent(town, GameState.NoOwner, me));
            }

            GameStateReducer.Apply(engine.State, new MoneyChangedEvent(me, 10 - engine.State.FindPlayer(me).Money, MoneyReason.Tax));
            GameStateReducer.Apply(engine.State, new DebtIncurredEvent(me, other, amount, MoneyReason.Rent));
            GameStateReducer.Apply(engine.State, new PhaseChangedEvent(GamePhase.AwaitingDebtPayment));
            return engine;
        }

        [Test]
        public void TicksComeAndGoAndEachChangeIsCounted()
        {
            var sale = new DebtSale();
            sale.Toggle(Cheap);
            sale.Toggle(Solo);
            CollectionAssert.AreEqual(new[] { Cheap, Solo }, sale.Picked);
            Assert.AreEqual(2, sale.Version);

            sale.Toggle(Cheap);
            Assert.IsFalse(sale.IsPicked(Cheap), "a second tick takes it off");
            sale.Set(Solo, true);
            Assert.AreEqual(3, sale.Version, "ticking a ticked town changes nothing");

            sale.Keep(square => square != Solo);
            Assert.AreEqual(0, sale.Picked.Count, "a town that is no longer a choice loses its tick");
            sale.Toggle(Dear);
            sale.Clear();
            Assert.AreEqual(0, sale.Picked.Count);
        }

        [Test]
        public void SellingTheTickedTownsPaysTheDebtOnceTheyCoverIt()
        {
            GameEngine engine = Owing(100, out int me);
            var sale = new DebtSale();
            sale.Toggle(Cheap);
            int cheap = GameRules.PropertySaleProceeds(engine.Board, engine.Rules, engine.State, Cheap);
            Assert.AreEqual(cheap, sale.Proceeds(engine.Board, engine.Rules, engine.State, me));
            IReadOnlyList<GameCommand> commands = sale.Commands(engine.Board, engine.Rules, engine.State, me);
            Assert.AreEqual(1, commands.Count, "30 more is not enough for 100: only the sale, no payment");
            Assert.IsInstanceOf<SellPropertyCommand>(commands[0]);

            sale.Toggle(Dear);
            commands = sale.Commands(engine.Board, engine.Rules, engine.State, me);
            Assert.AreEqual(3, commands.Count);
            Assert.IsInstanceOf<PayDebtCommand>(commands[2], "the two sales cover the debt, so it is paid too");
            foreach (GameCommand command in commands)
            {
                CommandResult result = engine.Execute(command);
                Assert.IsTrue(result.IsAccepted, $"{command.GetType().Name} was rejected: {result.Reason}");
            }

            Assert.IsNull(engine.State.Debt);
            Assert.AreEqual(me, engine.State.GetOwner(Solo), "the town left unticked is kept");
        }

        [Test]
        public void NothingIsSentForTownsThatAreNotTheirsOrWithoutADebt()
        {
            GameEngine engine = Owing(100, out int me);
            var sale = new DebtSale();
            sale.Toggle(0);
            sale.Toggle(3);
            Assert.AreEqual(0, sale.Commands(engine.Board, engine.Rules, engine.State, me).Count, "the start and a rest square are not towns");
            sale.Toggle(Solo);
            Assert.AreEqual(0, sale.Commands(engine.Board, engine.Rules, engine.State, me == 1 ? 2 : 1).Count, "the other player owes nothing");
        }

        [Test]
        public void TheWaitingPlayersPiecesFadeAndThePlayerOnTurnsStandsSolid()
        {
            Material fade = ModelFade.Copy(Resources.Load<Material>(ModelParts.MaterialResource), 0.4f);
            Assert.IsNotNull(fade, "the fade material is in Resources");
            Assert.GreaterOrEqual(fade.renderQueue, 3000, "drawn see-through, after the solid scene");

            var holder = new GameObject("Pawn Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                PawnView pawn = PawnView.Create(holder.transform, board, 0, Color.red, new PawnCrowd(4), 1);
                Renderer[] parts = pawn.transform.Find(PawnView.ModelName).GetComponentsInChildren<Renderer>(true);
                Assert.Greater(parts.Length, 0);
                var solid = new List<Material>();
                foreach (Renderer part in parts) solid.Add(part.sharedMaterial);

                pawn.SetFaded(true);
                Assert.IsTrue(pawn.Faded);
                foreach (Renderer part in parts)
                {
                    Assert.GreaterOrEqual(part.sharedMaterial.renderQueue, 3000, $"{part.name} is see-through");
                    Assert.That(part.sharedMaterial.color.a, Is.EqualTo(PawnView.FadeAlpha).Within(1e-4f));
                    Material[] drawn = part.sharedMaterials;
                    Assert.AreEqual(2, drawn.Length, $"{part.name} carries the depth mask");
                    Assert.AreEqual(ModelFade.DepthShaderName, drawn[1].shader.name);
                    Assert.Less(drawn[1].renderQueue, drawn[0].renderQueue, $"{part.name}'s depth is drawn before its colour");
                }

                pawn.SetFaded(false);
                for (int i = 0; i < parts.Length; i++) Assert.AreSame(solid[i], parts[i].sharedMaterial, $"{parts[i].name} is solid again");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
