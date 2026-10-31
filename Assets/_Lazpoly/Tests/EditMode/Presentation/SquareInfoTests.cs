using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    public class SquareInfoTests
    {
        private static readonly int[] Rents = { 50, 150, 400, 550, 700 };

        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Başlangıç"),
            SquareDefinition.Property("Sur Yolu", 60, 4, 1, 50, Rents),
            SquareDefinition.Property("Kuru Yolu", 60, 4, 1, 50, Rents),
            SquareDefinition.Tax("Emlak Vergisi", 100),
            SquareDefinition.Rest("Park"),
        });

        private static readonly RulesConfig Rules = new RulesConfig(1000, 200, 6, 3, 2, 10, 50);

        private static GameState State() =>
            GameState.CreateInitial(Board, Rules, new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });

        private static string ValueOf(SquareInfo info, string label) => info.Rows.Single(row => row.Label == label).Value;

        [Test]
        public void UnownedPropertyShowsTheWholeRentTableAndNoCurrentRent()
        {
            SquareInfo info = SquareInfo.Describe(Board, Rules, State(), 1, "Sokaklar");

            Assert.AreEqual("Sur Yolu", info.Title);
            Assert.AreEqual("Mülk · Sokaklar", info.Kind);
            Assert.AreEqual("60", ValueOf(info, "Fiyat"));
            Assert.AreEqual("4", ValueOf(info, "Kira"));
            Assert.AreEqual("8", ValueOf(info, "Kira, rengin tamamı"));
            Assert.AreEqual("50", ValueOf(info, "Kira, 1 ev"));
            Assert.AreEqual("700", ValueOf(info, "Kira, otel"));
            Assert.AreEqual("50", ValueOf(info, "Ev maliyeti"));
            Assert.AreEqual("30", ValueOf(info, "İpotek değeri"));
            Assert.IsFalse(info.Rows.Any(row => row.Current));
            CollectionAssert.Contains(info.Notes, "Bu renk: 2 kare; sahipsiz 2.");
        }

        [Test]
        public void MarksTheRentThatAppliesNow()
        {
            GameState state = State();
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(1, GameState.NoOwner, 1));
            Assert.AreEqual("Kira", SquareInfo.Describe(Board, Rules, state, 1, null).Rows.Single(row => row.Current).Label);

            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(2, GameState.NoOwner, 1));
            Assert.AreEqual("Kira, rengin tamamı", SquareInfo.Describe(Board, Rules, state, 1, null).Rows.Single(row => row.Current).Label);

            GameStateReducer.Apply(state, new BuildingLevelChangedEvent(1, 2));
            SquareInfo built = SquareInfo.Describe(Board, Rules, state, 1, null);
            Assert.AreEqual("Kira, 2 ev", built.Rows.Single(row => row.Current).Label);
            CollectionAssert.Contains(built.Notes, "Sahibi Ayşe.");
            CollectionAssert.Contains(built.Notes, "Bu renk: 2 kare; Ayşe 2.");
        }

        [Test]
        public void MortgagedPropertyHasNoCurrentRent()
        {
            GameState state = State();
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(1, GameState.NoOwner, 2));
            GameStateReducer.Apply(state, new MortgageChangedEvent(1, true));
            SquareInfo info = SquareInfo.Describe(Board, Rules, state, 1, null);
            Assert.IsFalse(info.Rows.Any(row => row.Current));
            CollectionAssert.Contains(info.Notes, "Sahibi Can (ipotekli, kira alınmaz).");
        }

        [Test]
        public void OtherSquaresExplainThemselves()
        {
            GameState state = State();
            CollectionAssert.Contains(SquareInfo.Describe(Board, Rules, state, 0, null).Notes, "Buradan her geçişte 200 alırsın.");
            Assert.AreEqual("100", ValueOf(SquareInfo.Describe(Board, Rules, state, 3, null), "Ödenecek"));
            Assert.AreEqual("Dinlenme", SquareInfo.Describe(Board, Rules, state, 4, null).Kind);
        }

        [Test]
        public void LogTonesFollowMoney()
        {
            Assert.AreEqual(LogTone.Gain, EventDescriber.ToneOf(new MoneyChangedEvent(1, 200, MoneyReason.PassedStart)));
            Assert.AreEqual(LogTone.Loss, EventDescriber.ToneOf(new MoneyChangedEvent(1, -60, MoneyReason.Rent)));
            Assert.AreEqual(LogTone.Neutral, EventDescriber.ToneOf(new DiceRolledEvent(1, 2, 3)));
        }

        [Test]
        public void CameraRigTurnsAroundTheBoardAndZooms()
        {
            Vector3 center = Vector3.zero, aim = new Vector3(0f, 0f, -1f), offset = new Vector3(0f, 10f, -8f);
            Assert.AreEqual(aim + offset, BoardCameraRig.Place(center, aim, offset, 0f, 1f));

            Vector3 half = BoardCameraRig.Place(center, aim, offset, 0f, 0.5f);
            Assert.AreEqual((aim + offset * 0.5f).y, half.y, 1e-4f);

            Vector3 turned = BoardCameraRig.Place(center, aim, offset, 180f, 1f);
            Assert.AreEqual(9f, turned.z, 1e-4f, "Half a turn puts the camera on the far side.");
            Assert.AreEqual(10f, turned.y, 1e-4f);
        }
    }
}
