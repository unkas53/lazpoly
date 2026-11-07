using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Data;
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
            Assert.AreEqual(50, cup.Fee);
            Assert.AreEqual(50, cup.BoostPercent);
            Assert.AreEqual(10, cup.BoostStep);
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
                Assert.IsNotNull(board.transform.Find("Scene Cup"), "a gold cup on its plinth stands on the corner");
                Assert.IsNull(board.transform.Find("Scene Card"), "the kiosk is gone");
                Assert.AreEqual("ŞAMPİYONLAR KUPASI", board.NameLabel(CupCorner).text, "in capitals like the jail, the tax and Şans");

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

                board.SetCup(Town("Bodrum"));
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
                s.CupPercent = 60;
                s.CupPicks = 2;
            });

            SquareInfo corner = SquareInfo.Describe(_data.Board, _data.Rules, state, CupCorner, null);
            Assert.AreEqual("Kupa", corner.Kind);
            Assert.AreEqual("%70", corner.Rows.Single(r => r.Label == "Sıradaki artış").Value);
            Assert.AreEqual("Paris (%60)", corner.Rows.Single(r => r.Label == "Kupanın şehri").Value);

            SquareInfo town = SquareInfo.Describe(_data.Board, _data.Rules, state, paris, null);
            Assert.AreEqual("kira +%60", town.Rows.Single(r => r.Label == "Kupa").Value);
            Assert.IsTrue(town.Notes.Any(n => n.Contains("Şampiyonlar Kupası")));

            string line = EventDescriber.Describe(new CupHostedEvent(1, paris, 60), state, _data.Board);
            StringAssert.Contains("Paris", line);
            StringAssert.Contains("%60", line);
            Assert.AreEqual("kupa", EventDescriber.ReasonText(MoneyReason.Cup));
        }

        [Test]
        public void HowToPlayAndTheTipExplainTheCup()
        {
            var section = HowToPlay.Sections(_data.Board, _data.Rules).Single(s => s.Key == "Şampiyonlar Kupası");
            StringAssert.Contains("50", section.Value);
            StringAssert.Contains("%10", section.Value);
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
