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

        private static readonly RulesConfig Rules = new RulesConfig(1000, 200, 6, 3, 2, 50);

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
            Assert.AreEqual("4", ValueOf(info, "Arsa"));
            Assert.AreEqual("8", ValueOf(info, "Rengin tamamı"));
            Assert.AreEqual("50", ValueOf(info, "1 ev"));
            Assert.AreEqual("700", ValueOf(info, "Otel"));
            Assert.AreEqual(7, info.Rows.Count(row => row.Rent), "land, whole colour, four houses and the hotel make the rent table");
            Assert.IsFalse(info.Rows.Single(row => row.Label == "Fiyat").Rent);
            Assert.AreEqual("50", ValueOf(info, "Ev maliyeti"));
            Assert.AreEqual("60", ValueOf(info, "Satış değeri"), "The bank pays the whole price (Faz 27).");
            Assert.IsFalse(info.Rows.Any(row => row.Current));
            CollectionAssert.Contains(info.Notes, "Bu renk: 2 kare; sahipsiz 2.");
        }

        [Test]
        public void MarksTheRentThatAppliesNow()
        {
            GameState state = State();
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(1, GameState.NoOwner, 1));
            Assert.AreEqual("Arsa", SquareInfo.Describe(Board, Rules, state, 1, null).Rows.Single(row => row.Current).Label);

            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(2, GameState.NoOwner, 1));
            Assert.AreEqual("Rengin tamamı", SquareInfo.Describe(Board, Rules, state, 1, null).Rows.Single(row => row.Current).Label);

            GameStateReducer.Apply(state, new BuildingLevelChangedEvent(1, 2));
            SquareInfo built = SquareInfo.Describe(Board, Rules, state, 1, null);
            Assert.AreEqual("2 ev", built.Rows.Single(row => row.Current).Label);
            Assert.AreEqual("300", built.Rows.Single(row => row.Current).Value, "the whole colour doubles the houses' rent too");
            Assert.AreEqual("1,4K", ValueOf(built, "Otel"));
            CollectionAssert.Contains(built.Notes, "Sahibi Ayşe.");
            CollectionAssert.Contains(built.Notes, "Bu renk: 2 kare; Ayşe 2.");
            // The card draws the owner's row and the group's chips itself, so it leaves those two notes out and keeps the rest.
            CollectionAssert.DoesNotContain(built.CardNotes.ToList(), "Sahibi Ayşe.");
            CollectionAssert.DoesNotContain(built.CardNotes.ToList(), "Bu renk: 2 kare; Ayşe 2.");
            Assert.AreEqual(built.Notes.Count - 2, built.CardNotes.Count());
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
        public void CameraRigLooksDownFromACornerAndZoomsByItsSize()
        {
            var aim = new Vector3(0f, -0.3f, 0f);
            Vector3 camera = BoardCameraRig.Place(aim, BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, BoardCameraRig.Distance);
            Assert.AreEqual(BoardCameraRig.Distance, Vector3.Distance(camera, aim), 1e-3f);
            Assert.Greater(camera.y, aim.y, "the camera looks down");
            Assert.Greater(camera.x, 0f, "it stands off the start square's corner (+x, -z)");
            Assert.Less(camera.z, 0f);
            Vector3 towards = Quaternion.Euler(BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, 0f) * Vector3.forward;
            Assert.That(Vector3.Distance(camera + towards * BoardCameraRig.Distance, aim), Is.LessThan(1e-3f), "looking along its forward reaches the aim");

            Vector3 opposite = BoardCameraRig.Place(aim, BoardCameraRig.Pitch, BoardCameraRig.BaseYaw + 180f, BoardCameraRig.Distance);
            Assert.AreEqual(-camera.x, opposite.x, 1e-3f, "half a turn puts the camera at the far corner");
            Assert.AreEqual(-camera.z, opposite.z, 1e-3f);
            Assert.AreEqual(camera.y, opposite.y, 1e-3f);

            Assert.AreEqual(10f, BoardCameraRig.SizeAt(10f, 1f));
            Assert.AreEqual(5f, BoardCameraRig.SizeAt(10f, 0.5f), 1e-4f, "zooming in shows less");
            Assert.AreEqual(10f * BoardCameraRig.MinZoom, BoardCameraRig.SizeAt(10f, 0.01f), 1e-4f, "the zoom is clamped");
            Assert.AreEqual(10f * BoardCameraRig.MaxZoom, BoardCameraRig.SizeAt(10f, 9f), 1e-4f);
        }
    
        [Test]
        public void SpecialSquaresExplainThemselves()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Başlangıç"), SquareDefinition.Card("Sokak Haberi"), SquareDefinition.Pot("Çay Molası"),
                SquareDefinition.Travel("Otogar", 50), SquareDefinition.Lookout("Seyir Terası"), SquareDefinition.Tax("Vergi", 100),
            }, new[] { CardDefinition.Collect("Ödül", 10) });
            GameState state = GameState.CreateInitial(board, Rules, new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });

            Assert.AreEqual("Sokak Haberi", SquareInfo.Describe(board, Rules, state, 1, null).Kind);
            Assert.AreEqual("Çay kumbarası", SquareInfo.Describe(board, Rules, state, 2, null).Kind);
            SquareInfo ride = SquareInfo.Describe(board, Rules, state, 3, null);
            Assert.AreEqual("Bilet", ride.Rows[0].Label);
            Assert.AreEqual("50", ride.Rows[0].Value);
            Assert.AreEqual("Seyir", SquareInfo.Describe(board, Rules, state, 4, null).Kind);
            CollectionAssert.Contains(SquareInfo.Describe(board, Rules, state, 5, null).Notes, "Buraya gelen öder; para çay kumbarasına girer.");
        }

        [Test]
        public void AResortShowsTheRentForOneToFourAndMarksWhatItsOwnerHolds()
        {
            var board = new BoardDefinition(new[]
            {
                SquareDefinition.Start("Başlangıç"),
                SquareDefinition.Resort("Manchester", 200, 50, 9),
                SquareDefinition.Resort("Şenzen", 200, 50, 9),
                SquareDefinition.Resort("Pittsburgh", 200, 50, 9),
                SquareDefinition.Resort("Detroit", 200, 50, 9),
            });
            GameState state = GameState.CreateInitial(board, Rules, new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });

            SquareInfo info = SquareInfo.Describe(board, Rules, state, 2, "Fabrika Şehirleri");
            Assert.AreEqual("Fabrika şehri", info.Kind);
            Assert.AreEqual("200", ValueOf(info, "Fiyat"));
            Assert.AreEqual("50", ValueOf(info, "1 fabrika şehri"));
            Assert.AreEqual("100", ValueOf(info, "2 fabrika şehri"));
            Assert.AreEqual("200", ValueOf(info, "3 fabrika şehri"), "each further resort doubles the rent (the owner's wish, Faz 27)");
            Assert.AreEqual("400", ValueOf(info, "4 fabrika şehri"));
            Assert.AreEqual(4, info.Rows.Count(row => row.Rent));
            Assert.IsFalse(info.Rows.Any(row => row.Label == "Ev maliyeti"), "a resort takes no buildings");
            Assert.IsFalse(info.Rows.Any(row => row.Current));

            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(1, GameState.NoOwner, 1));
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(2, GameState.NoOwner, 1));
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(4, GameState.NoOwner, 1));
            info = SquareInfo.Describe(board, Rules, state, 2, "Fabrika Şehirleri");
            Assert.AreEqual("3 fabrika şehri", info.Rows.Single(row => row.Current).Label);
            CollectionAssert.Contains(info.Notes, "Sahibi Ayşe.");
            Assert.AreEqual(4, SquareInfo.ResortCount(board));
        }
}
}
