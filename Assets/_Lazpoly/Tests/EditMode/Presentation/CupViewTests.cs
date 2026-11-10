using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>Faz 24: the champions' cup on the shipped board, on the board view and in the texts.</summary>
    public class CupViewTests
    {
        private BoardData _data;

        [SetUp]
        public void SetUp()
        {
            Lang.Set(Lazpoly.Infra.Settings.Language.Turkish);
            _data = BoardDataLoader.LoadFromResources();
        }

        private int CupCorner => Enumerable.Range(0, _data.Board.Count).Single(i => _data.Board[i].Type == SquareType.Cup);

        private int Town(string name) => Enumerable.Range(0, _data.Board.Count).Single(i => _data.Board[i].Name == name);

        /// <summary>A two-player state on the shipped board, changed through its snapshot.</summary>
        private GameState State(System.Action<GameStateSnapshot> setup)
        {
            GameState fresh = GameState.CreateInitial(_data.Board, _data.Rules, new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(fresh);
            setup(snapshot);
            return snapshot.Restore();
        }

        [Test]
        public void TheCupTakesTheStreetNewsCorner()
        {
            Assert.AreEqual(16, CupCorner, "the corner after the jail's side (the owner's wish, Faz 24)");
            SquareDefinition cup = _data.Board[CupCorner];
            Assert.AreEqual("Şampiyonlar Kupası", cup.Name);
            Assert.AreEqual(50000, cup.Fee, "50K (the owner's wish, Faz 27)");
            Assert.AreEqual(50, cup.BoostPercent);
            Assert.AreEqual(50, cup.BoostStep, "50, 100, 150... (the owner's rule, Faz 29)");
            Assert.AreEqual(500, cup.BoostLimit, "never more than 500%");
            Assert.IsFalse(Enumerable.Range(0, _data.Board.Count).Any(i => _data.Board[i].Name == "Sokak Haberi"), "the street news is gone");
            Assert.AreEqual("Champions' Cup", Lang.Load(Lazpoly.Infra.Settings.Language.English)["Şampiyonlar Kupası"]);
        }

        [Test]
        public void TheCupCornerShowsItsTrophyAndTheCupsTownAShortOne()
        {
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, _data);
                Assert.IsNotNull(board.transform.Find("Scene Cup"), "the stadium stands on the corner");
                Assert.AreSame(board.transform.Find("Scene Cup"), board.CupScene);
                Assert.IsNotNull(board.CupScene.Find(FigureModels.BodyPart), "the owner's stadium model, not the drawn cup");
                Assert.Greater(board.CupScene.Find(FigureModels.BodyPart).GetComponent<MeshFilter>().sharedMesh.bounds.size.x, 0.9f, "the stadium (one unit wide), not the cup");
                Vector3 front = board.CupScene.localRotation * Vector3.forward;
                Assert.Greater(Vector3.Dot(front, -BoardView.ScreenUp(BoardCameraRig.BaseYaw)), 0.99f, "its stand faces the camera");
                Vector3 up = board.CupScene.localPosition - board.Layout.SquareCenter(CupCorner);
                Assert.Greater(Vector3.Dot(up, BoardView.ScreenUp(BoardCameraRig.BaseYaw)), 0f, "up the screen, behind the name");
                Assert.GreaterOrEqual(BoardView.StadiumShift - BoardView.StadiumSize * BoardView.StadiumDepth / 2f, BoardView.CupNameDepth / 2f - BoardView.CupNameShift,
                    "its front edge stays behind the name");
                Assert.IsNull(board.transform.Find("Scene Card"), "the kiosk is gone");

                TextMesh name = board.NameLabel(CupCorner);
                Assert.AreEqual("ŞAMPİYONLAR\nKUPASI", name.text, "in capitals like the jail, the tax and Şans, on two lines to print large");
                Assert.AreEqual("ONE", BoardView.TwoLines("ONE"));
                Assert.AreEqual("CHAMPIONS'\nCUP", BoardView.TwoLines("CHAMPIONS' CUP"));
                Vector3 line = name.transform.localRotation * Vector3.right;
                Assert.Greater(Vector3.Dot(line, BoardView.DiagonalLine(BoardCameraRig.BaseYaw)), 0.99f, "it runs across the corner (the owner's wish)");
                Assert.That(Mathf.Abs(Vector3.Dot(line, board.Layout.Outward(CupCorner))), Is.EqualTo(Mathf.Sqrt(0.5f)).Within(0.01f), "at 45 degrees to the edges");
                Assert.Greater(Vector3.Dot(line, BoardView.ScreenRight(BoardCameraRig.BaseYaw)), 0f, "reading left to right");
                board.FaceCamera(BoardCameraRig.BaseYaw + 90f);
                Assert.Greater(Vector3.Dot(name.transform.localRotation * Vector3.right, BoardView.ScreenRight(BoardCameraRig.BaseYaw + 90f)), 0.7f, "still left to right with the table turned");
                board.FaceCamera(BoardCameraRig.BaseYaw);

                board.SetCup(GameState.NoCup);
                Assert.IsTrue(board.CupTrophy == null || !board.CupTrophy.gameObject.activeSelf, "no town has it yet");

                int paris = Town("Paris");
                board.SetCup(paris);
                Assert.AreEqual(paris, board.CupShownOn);
                Assert.IsTrue(board.CupTrophy.gameObject.activeSelf);
                Vector3 offset = board.CupTrophy.localPosition - board.Layout.SquareCenter(paris);
                offset.y = 0f;
                Assert.Less(offset.magnitude, board.Layout.SquareSize(paris).x / 2f, "it stands on the town's own tile");

                int london = Town("Londra");
                board.SetCup(london);
                Assert.AreEqual(london, board.CupShownOn, "one cup only: it moves");
                Assert.AreEqual(1, holder.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Cup on town"));

                board.SetCup(Town("Manchester"));
                Assert.IsFalse(board.CupTrophy.gameObject.activeSelf, "a resort never shows it");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheCardsSayWhereTheCupIsAndWhatTheNextPickGives()
        {
            int paris = Town("Paris");
            GameState state = State(s =>
            {
                s.Owners[paris] = 1;
                s.CupSquare = paris;
                s.CupPercent = 100;
                s.CupPicks = 2;
            });

            SquareInfo corner = SquareInfo.Describe(_data.Board, _data.Rules, state, CupCorner, null);
            Assert.AreEqual("Kupa", corner.Kind);
            Assert.AreEqual("%150", corner.Rows.Single(r => r.Label == "Sıradaki artış").Value, "the third pick: 50, 100, 150");
            Assert.AreEqual("Paris (%100)", corner.Rows.Single(r => r.Label == "Kupanın şehri").Value);

            SquareInfo town = SquareInfo.Describe(_data.Board, _data.Rules, state, paris, null);
            Assert.AreEqual("kira +%100", town.Rows.Single(r => r.Label == "Kupa").Value);
            Assert.IsTrue(town.Notes.Any(n => n.Contains("Şampiyonlar Kupası")));

            string line = EventDescriber.Describe(new CupHostedEvent(1, paris, 100), state, _data.Board);
            StringAssert.Contains("Paris", line);
            StringAssert.Contains("%100", line);
            Assert.IsTrue(corner.Notes.Any(n => n.Contains("en fazla %500")), "the limit is told");

            Assert.AreEqual("Şampiyonlar Kupası maçları Paris şehrinde yapılıyor", CupNews.Text("Paris"));
            Assert.AreEqual("Şehrin kirası %100 artıyor.", CupNews.RaiseText(100));
            Assert.AreEqual("ŞAMPİYONLAR KUPASI", CupNews.Title);
            Lang.Set(Lazpoly.Infra.Settings.Language.English);
            Assert.AreEqual("The Champions' Cup matches are played in Paris", CupNews.Text("Paris"));
            Lang.Set(Lazpoly.Infra.Settings.Language.Turkish);
            Assert.Greater(CupNews.Seconds, 2f);
            Assert.AreEqual("kupa", EventDescriber.ReasonText(MoneyReason.Cup));
        }

        [Test]
        public void HowToPlayAndTheTipExplainTheCup()
        {
            var section = HowToPlay.Sections(_data.Board, _data.Rules).Single(s => s.Key == "Şampiyonlar Kupası");
            StringAssert.Contains("50", section.Value);
            StringAssert.Contains("%50", section.Value);
            StringAssert.Contains("en fazla %500", section.Value);
            StringAssert.Contains("tek bir şehirde", section.Value);
            Assert.IsFalse(HowToPlay.Sections(_data.Board, _data.Rules).Any(s => s.Key.Contains("Sokak Haberi")), "the card section is Şans alone");

            int paris = Town("Paris");
            GameState state = State(s =>
            {
                s.Owners[paris] = 1;
                s.Players[0].Position = CupCorner;
                s.Phase = GamePhase.AwaitingCupChoice;
            });
            Tip? tip = Tips.Next(_data.Board, _data.Rules, state, 1, id => false);
            Assert.AreEqual(TipId.Cup, tip?.Id);
            StringAssert.Contains("%50", tip?.Text);
        }
    }
}
