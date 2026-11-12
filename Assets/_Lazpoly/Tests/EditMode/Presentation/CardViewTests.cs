using System.Collections.Generic;
using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using NUnit.Framework;
using UnityEngine;
using Language = Lazpoly.Infra.Settings.Language;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>Faz 30: the strategic cards on the shipped board, in the texts, the sounds, the corner cards and on the board view.</summary>
    public class CardViewTests
    {
        private BoardData _data;

        [SetUp]
        public void SetUp()
        {
            Lang.Set(Language.Turkish);
            _data = BoardDataLoader.LoadFromResources();
        }

        [TearDown]
        public void BackToTurkish() => Lang.Set(Language.Turkish);

        private BoardDefinition Board => _data.Board;

        private int Town(string name) => Enumerable.Range(0, Board.Count).Single(i => Board[i].Name == name);

        private int Card(CardKind kind) => Enumerable.Range(0, Board.Cards.Count).First(i => Board.Cards[i].Kind == kind);

        /// <summary>A two-player state on the shipped board, changed through its snapshot.</summary>
        private GameState State(System.Action<GameStateSnapshot> setup)
        {
            GameState fresh = GameState.CreateInitial(Board, _data.Rules, new[] { new PlayerSeat(1, "Ayşe"), new PlayerSeat(2, "Can") });
            GameStateSnapshot snapshot = GameStateSnapshot.Capture(fresh);
            setup(snapshot);
            return snapshot.Restore();
        }

        [Test]
        public void EveryKeptCardHasItsOwnNameInBothLanguages()
        {
            var names = new HashSet<string>();
            foreach (CardDefinition card in Board.Cards.Where(c => c.IsKept))
            {
                string name = CardPlay.Name(card.Kind);
                Assert.AreNotEqual("Kart", name, card.Kind.ToString());
                names.Add(name);
            }

            Assert.AreEqual(14, names.Count, "the old spyglass and the fifteen new cards: fourteen kinds");
            Assert.AreEqual("İmarsız Yapı", CardPlay.Name(CardKind.Demolish));
            Assert.AreEqual("KAÇAK KAT", CardPlay.Title(Board, Card(CardKind.IllegalFloor)));
            Lang.Set(Language.English);
            Assert.AreEqual("Illegal Building", CardPlay.Name(CardKind.Demolish));
            Assert.AreEqual("Rent Shield", CardPlay.Name(CardKind.RentShield));
        }

        [Test]
        public void APlayedCardSaysWhoPlayedItOnWhat()
        {
            int paris = Town("Paris");
            GameState state = State(s => s.Owners[paris] = 2);
            var demolish = new CardPlayedEvent(1, Card(CardKind.Demolish), paris);
            Assert.AreEqual("Ayşe kart oynadı: İmarsız Yapı → Paris", CardPlay.LogLine(Board, state, demolish));
            Assert.AreEqual("Ayşe kart oynadı: İmarsız Yapı → Paris", EventDescriber.Describe(demolish, state, Board));
            Assert.AreEqual("Ayşe oynadı → Paris", CardPlay.Headline(Board, state, demolish));
            Assert.AreEqual(1, EventDescriber.ActorOf(demolish));

            Assert.AreEqual("Ayşe oynadı → Can", CardPlay.Headline(Board, state, new CardPlayedEvent(1, Card(CardKind.Audit), 2)));
            Assert.AreEqual("Ayşe oynadı → yatırım", CardPlay.Headline(Board, state, new CardPlayedEvent(1, Card(CardKind.Fund), -1, 1)));
            Assert.AreEqual("Ayşe oynadı → nakit", CardPlay.Headline(Board, state, new CardPlayedEvent(1, Card(CardKind.Fund))));
            Assert.AreEqual("Ayşe kart oynadı: Avukat", CardPlay.LogLine(Board, state, new CardPlayedEvent(1, Card(CardKind.Lawyer))));

            Assert.IsTrue(CardPlay.Blocked(new ShieldChangedEvent(paris, false, blocked: true)));
            Assert.IsFalse(CardPlay.Blocked(new ShieldChangedEvent(paris, true)));
            Assert.IsFalse(CardPlay.Blocked(null));

            Assert.AreEqual("Şehirdeki her şey yıkıldı; şehir bankaya döndü.", CardPlay.Effect(Board, demolish));
            Assert.AreEqual("200K yatırdı; Başlangıç'tan geçince 500K alacak.", CardPlay.Effect(Board, new CardPlayedEvent(1, Card(CardKind.Fund), -1, 1)));
            Assert.AreEqual("3 sıra boyunca bu şehirde kira alınmayacak.", CardPlay.Effect(Board, new CardPlayedEvent(1, Card(CardKind.Strike), paris)));
            for (int i = 0; i < Board.Cards.Count; i++)
            {
                if (Board.Cards[i].IsKept) Assert.IsNotEmpty(CardPlay.Effect(Board, new CardPlayedEvent(1, i)), Board.Cards[i].Kind.ToString());
            }

            Assert.AreEqual("Kendi şehrine kalkan koy, ilk saldırıyı durdursun.", CardPlay.Body(Board, Card(CardKind.Shield)), "the name is the title already");
            StringAssert.StartsWith("Bir şehirde imarsız yapı", CardPlay.Body(Board, Card(CardKind.Demolish)), "a text without the name stays whole");
            Lang.Set(Language.English);
            Assert.AreEqual("Put a shield on your town to stop the first attack.", CardPlay.Body(Board, Card(CardKind.Shield)));
            Lang.Set(Language.Turkish);
            Assert.AreEqual(3f, CardPlay.Seconds);
        }

        [Test]
        public void TheLogTellsShieldsStrikesFundsAndDiscards()
        {
            int paris = Town("Paris");
            GameState state = State(s => s.Owners[paris] = 2);
            Assert.AreEqual("Paris koruma kalkanı altında", EventDescriber.Describe(new ShieldChangedEvent(paris, true), state, Board));
            Assert.AreEqual("Paris: koruma kalkanı saldırıyı durdurdu", EventDescriber.Describe(new ShieldChangedEvent(paris, false, true), state, Board));
            Assert.IsNull(EventDescriber.Describe(new ShieldChangedEvent(paris, false), state, Board), "a town that changes hands loses it quietly");
            Assert.IsNull(EventDescriber.Describe(new StrikeChangedEvent(paris, 1, 2), state, Board), "the countdown stays out of the log");
            Assert.AreEqual("Paris: kira grevi bitti", EventDescriber.Describe(new StrikeChangedEvent(paris, GameState.NoOwner, 0), state, Board));
            Assert.AreEqual("Ayşe Başlangıç'tan geçince 500K alacak", EventDescriber.Describe(new FundChangedEvent(1, 500000), state, Board));
            Assert.IsNull(EventDescriber.Describe(new FundChangedEvent(1, 0), state, Board));
            Assert.AreEqual("Can elindeki bir kartı bıraktı: Dürbün", EventDescriber.Describe(new CardDiscardedEvent(2, Card(CardKind.Lookout)), state, Board));
            Assert.AreEqual("vergi denetimi", EventDescriber.ReasonText(MoneyReason.Audit));
            Assert.AreEqual("yatırım fonu", EventDescriber.ReasonText(MoneyReason.Fund));
        }

        [Test]
        public void CardsSoundWhenPlayedAndAShieldWhenItHolds()
        {
            Assert.AreEqual(SoundCue.UiOpen, SoundCues.For(new CardPlayedEvent(1, 0)));
            Assert.AreEqual(SoundCue.Achievement, SoundCues.For(new ShieldChangedEvent(1, false, true)));
            Assert.AreEqual(SoundCue.Build, SoundCues.For(new ShieldChangedEvent(1, true)));
            Assert.AreEqual(SoundCue.MoneyOut, SoundCues.For(new MoneyChangedEvent(2, -100, MoneyReason.Audit)));
            Assert.AreEqual(SoundCue.MoneyIn, SoundCues.For(new MoneyChangedEvent(1, 500000, MoneyReason.Fund)));
            Assert.AreEqual(SoundCue.MoneyOut, SoundCues.For(new MoneyChangedEvent(1, -200000, MoneyReason.Fund)));
        }

        [Test]
        public void ATownsCardTellsItsShieldAndItsStrike()
        {
            int paris = Town("Paris");
            int london = Town("Londra");
            GameState state = State(s =>
            {
                s.Owners[paris] = 2;
                s.Owners[london] = 2;
                s.Shields = new bool[Board.Count];
                s.Strikers = Enumerable.Repeat(GameState.NoOwner, Board.Count).ToArray();
                s.StrikeTurns = new int[Board.Count];
                s.Shields[paris] = true;
                s.Strikers[london] = 1;
                s.StrikeTurns[london] = 2;
            });

            SquareInfo shielded = SquareInfo.Describe(Board, _data.Rules, state, paris, null);
            Assert.IsTrue(shielded.Notes.Any(n => n.StartsWith("Koruma kalkanı var")));
            SquareInfo struck = SquareInfo.Describe(Board, _data.Rules, state, london, null);
            Assert.IsTrue(struck.Notes.Contains("Kira grevi: Ayşe oyuncusunun 2 sırası daha bitene kadar kira alınmaz."));
        }

        [Test]
        public void TheCardsTipComesBeforeTheRollWithAHand()
        {
            GameState state = State(s =>
            {
                s.Players[0].Hand.Add(Card(CardKind.Shield));
                s.Round = 2;
            });
            var seen = new HashSet<TipId> { TipId.FirstRoll, TipId.RoundLimit };
            Tip? tip = Tips.Next(Board, _data.Rules, state, state.Players[0].Id, seen.Contains);
            Assert.AreEqual(TipId.Cards, tip?.Id);
            StringAssert.Contains("\"Kartlar\" düğmesinden", tip?.Text);
        }

        [Test]
        public void TheHelpExplainsTheStrategyCards()
        {
            IReadOnlyList<KeyValuePair<string, string>> sections = HowToPlay.Sections(Board, _data.Rules);
            KeyValuePair<string, string> cards = sections.Single(s => s.Key == "Strateji kartları");
            StringAssert.Contains("en çok 4 kart", cards.Value);
            StringAssert.Contains("hemen kullanılır ya da saklanır", cards.Value, "the owner's wish, 2026-11-12");
            StringAssert.Contains("takas edilmez", cards.Value);
        }

        [Test]
        public void ACornerCardShowsHowManyCardsAreHeldOnTheFace()
        {
            var parent = new GameObject("Corner", typeof(RectTransform));
            try
            {
                var model = new PlayerCardModel { Name = "Ayşe", Color = Color.red, Cards = 3 };
                PlayerCardView view = PlayerCardView.Create((RectTransform)parent.transform, 300f, model, _ => { });
                Assert.AreEqual(3, view.CardBacks);
                Assert.AreEqual(1, view.Root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Hand Badge"), "one badge on the face");
                Assert.AreEqual("3", view.Root.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(t => t.name == "Hand Count").text);
                model.Cards = 0;
                Assert.IsFalse(PlayerCardView.Create((RectTransform)parent.transform, 300f, model, _ => { }).Root
                    .GetComponentsInChildren<Transform>(true).Any(t => t.name == "Hand Badge"), "no badge without cards");

                model.Cards = 9;
                Assert.AreEqual(PlayerCardView.MaxCards, PlayerCardView.Create((RectTransform)parent.transform, 300f, model, _ => { }).CardBacks);
                model.Cards = 2;
                model.State = PlayerCardState.Bankrupt;
                Assert.AreEqual(0, PlayerCardView.Create((RectTransform)parent.transform, 300f, model, _ => { }).CardBacks, "a bankrupt hand is empty");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void EveryCardInTheMiddleHasItsPictureAndColour()
        {
            foreach (string name in CardArt.All.Distinct())
            {
                Sprite picture = CardArt.Get(name);
                Assert.IsNotNull(picture, name);
                Assert.AreEqual(8f / 3f, picture.rect.width / picture.rect.height, 0.01f, $"{name} is an 8:3 band");
            }

            Assert.IsNotNull(CardArt.Get(CardArt.Face));
            Assert.AreNotEqual(Vector4.zero, CardArt.Get(CardArt.Face).border, "the face stretches as a 9-slice");
            Assert.IsNotNull(CardArt.Get(CardArt.Shadow));
            Assert.AreEqual("Card_demolish", CardArt.PictureFor(CardKind.Demolish));
            Assert.AreEqual(CardTone.Attack, CardArt.ToneFor(CardKind.ForceSale));
            Assert.AreEqual(CardTone.Defence, CardArt.ToneFor(CardKind.Shield));
            Assert.AreEqual(CardTone.Money, CardArt.ToneFor(CardKind.Fund));
            Assert.AreEqual("SALDIRI KARTI", CardArt.Family(CardKind.Demolish));
            Assert.IsNull(CardArt.Family(CardKind.Collect), "a plain card has the deck's name over it instead");
            Assert.AreEqual("HAPSE GİR", CardArt.Title(CardKind.GoToJail));
            Assert.AreEqual("İMARSIZ YAPI", CardArt.Title(CardKind.Demolish));
        }

        [Test]
        public void ACardInTheMiddleGrowsWithItsTextAndShowsItsPicture()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var notice = new CenterNotice(canvas.transform, "Test Card");
                notice.Paint(CardArt.Cup, CardTone.Cup, "KUPA");
                notice.Title.text = "ŞAMPİYONLAR KUPASI";
                notice.Body.text = "Bir satır.";
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(notice.Rect);
                float short_ = notice.Rect.rect.height;
                notice.Body.text = string.Join("\n", Enumerable.Repeat("Şampiyonlar Kupası maçları Paris şehrinde yapılıyor.", 4));
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(notice.Rect);
                Assert.Greater(notice.Rect.rect.height, short_ + 3f * CenterNotice.BodySize, "the card grows with its text instead of cutting it");
                Assert.Greater(short_, CenterNotice.PictureHeight, "the picture is part of the card");
                Assert.IsTrue(notice.Picture.gameObject.activeSelf);
                Assert.IsTrue(notice.Family.gameObject.activeSelf);
                Assert.AreEqual(CardArt.ToneColor(CardTone.Cup), notice.Title.color);
                Assert.GreaterOrEqual(notice.Body.fontSize, 24, "large enough to read at a glance");

                notice.Paint(null, CardTone.Grey);
                Assert.IsFalse(notice.Picture.gameObject.activeSelf, "no picture, no empty band");
                Assert.IsFalse(notice.Family.gameObject.activeSelf);
                Assert.IsFalse(notice.Shown, "hidden until shown");
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ACardSaysTheMoneyItMovesAndTheMoneyWaitsForItToClose()
        {
            // The owner's wish (2026-11-12): what a Şans card pays or costs is written on the card, and the money moves
            // only once the card has closed.
            var prize = new MoneyChangedEvent(1, 100000, MoneyReason.Card);
            Assert.AreEqual(100000, GameSceneController.CardMoney(1, new GameEvent[] { prize, new PhaseChangedEvent(GamePhase.TurnEnd) }, true));
            Assert.AreEqual(-50000, GameSceneController.CardMoney(1,
                new GameEvent[] { new MoneyChangedEvent(1, -50000, MoneyReason.Card), new MoneyChangedEvent(2, 50000, MoneyReason.Card) }, true),
                "the receiver's gain is not the drawer's");
            Assert.AreEqual(-80000, GameSceneController.CardMoney(1,
                new GameEvent[] { new DebtIncurredEvent(1, GameState.NoOwner, 80000, MoneyReason.Card), new PhaseChangedEvent(GamePhase.AwaitingDebtPayment) }, true),
                "a fine that cannot be paid yet is still what the card costs");
            Assert.AreEqual(0, GameSceneController.CardMoney(1,
                new GameEvent[] { new PlayerMovedEvent(1, 3, 0, true), new MoneyChangedEvent(1, 200000, MoneyReason.PassedStart) }, true),
                "a card that moves the pawn writes no sum");
            Assert.AreEqual(0, GameSceneController.CardMoney(1, new GameEvent[] { new MoneyChangedEvent(1, 200000, MoneyReason.PassedStart) }, true),
                "only the card's own money");
            Assert.AreEqual(0, GameSceneController.CardMoney(1, new GameEvent[] { new PhaseChangedEvent(GamePhase.AwaitingCardUse), prize }, true),
                "a card that may be kept moves nothing yet");
            Assert.AreEqual(0, GameSceneController.CardMoney(1, null, true));

            // A card played from the hand: whatever it moves for its player, until the next thing happens.
            Assert.AreEqual(-40000, GameSceneController.CardMoney(1,
                new GameEvent[] { new MoneyChangedEvent(1, -40000, MoneyReason.Fund), new FundChangedEvent(1, 100000), new DiceRolledEvent(1, 2, 3), prize }, false));
            Assert.AreEqual(0, GameSceneController.CardMoney(1, new GameEvent[] { new MoneyChangedEvent(2, -60000, MoneyReason.Audit) }, false),
                "an audit takes the rival's money, not the player's");

            Assert.AreEqual("+100K", CenterNotice.MoneyText(100000));
            Assert.AreEqual("-50K", CenterNotice.MoneyText(-50000));
            Assert.AreEqual(CenterNotice.GainInk, CenterNotice.MoneyInk(1));
            Assert.AreEqual(CenterNotice.LossInk, CenterNotice.MoneyInk(-1));
            Assert.AreNotEqual(CenterNotice.GainInk, CenterNotice.LossInk);

            Assert.Greater(GameSceneController.CardSeconds, 2.5f, "time to read the card before its money moves");
            Assert.Less(GameSceneController.CardOfferSeconds, 0.6f, "a card that asks shows its buttons almost at once");
        }

        [Test]
        public void ACardInTheMiddleWritesItsSumAndTakesClicksOnlyWhenItAsks()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var notice = new CenterNotice(canvas.transform, "Test Card", GameSceneController.CardSeconds);
                notice.Title.text = "PARA GELDİ";
                notice.Body.text = "Bir satır.";
                Assert.IsFalse(notice.Money.gameObject.activeInHierarchy, "no sum, no line");
                Assert.AreEqual(0, notice.MoneyShown);
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(notice.Rect);
                float plain = notice.Rect.rect.height;

                notice.SetMoney(100000);
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(notice.Rect);
                Assert.IsTrue(notice.Money.gameObject.activeInHierarchy);
                Assert.AreEqual("+100K", notice.Money.text);
                Assert.AreEqual(CenterNotice.GainInk, notice.Money.color);
                Assert.AreEqual(100000, notice.MoneyShown);
                Assert.Greater(notice.Rect.rect.height, plain + CenterNotice.MoneySize * 0.8f, "the sum has a line of its own");
                Assert.Greater(notice.Money.fontSize, notice.Title.fontSize, "the largest letters on the card");
                Assert.Less(notice.Money.transform.parent.GetComponent<UnityEngine.UI.Image>().color.a, 0.3f, "on a pale plate of its ink");

                notice.SetMoney(-50000);
                Assert.AreEqual("-50K", notice.Money.text);
                Assert.AreEqual(CenterNotice.LossInk, notice.Money.color);
                notice.SetMoney(0);
                Assert.IsFalse(notice.Money.gameObject.activeInHierarchy);
                Assert.AreEqual(0, notice.MoneyShown);

                // Only a card that asks ("Kullan" / "Sakla") shows its buttons and takes the mouse, and it stays up.
                var group = notice.Rect.GetComponent<CanvasGroup>();
                Assert.IsFalse(notice.Interactive);
                Assert.IsFalse(group.blocksRaycasts);
                Assert.IsFalse(notice.Actions.gameObject.activeSelf);
                notice.Interactive = true;
                Assert.IsTrue(group.blocksRaycasts);
                Assert.IsTrue(group.interactable);
                Assert.IsTrue(notice.Actions.gameObject.activeSelf);
                notice.Hold(0.05f);
                Assert.Less(group.alpha, 1f, "it swells in like any card");
                notice.Hold(100f);
                Assert.AreEqual(1f, group.alpha, 1e-4f, "a card that waits for an answer never fades");
                Assert.IsTrue(notice.Shown);
                notice.Hide();
                Assert.IsFalse(notice.Shown);
                Assert.IsFalse(notice.Interactive, "a hidden card takes no clicks");
                Assert.IsFalse(group.blocksRaycasts);
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }

        [Test]
        public void ACardJustDrawnSaysWhyItCannotBeUsedNow()
        {
            int lawyer = Card(CardKind.Lawyer);
            int rentShield = Card(CardKind.RentShield);
            int chauffeur = Card(CardKind.Chauffeur);
            int shield = Card(CardKind.Shield);
            int fund = Card(CardKind.Fund);
            int demolish = Card(CardKind.Demolish);
            int paris = Town("Paris");
            GameState Drawn(int card, System.Action<GameStateSnapshot> more = null) => State(s =>
            {
                s.Players[0].Hand.Add(card);
                s.Phase = GamePhase.AwaitingCardUse;
                more?.Invoke(s);
            });

            Assert.AreEqual("Bu kart yalnızca hapisteyken kullanılır.", CardHand.UseBlock(Board, Drawn(lawyer), lawyer));
            Assert.AreEqual("Bu kart yalnızca kira öderken kullanılır.", CardHand.UseBlock(Board, Drawn(rentShield), rentShield));
            Assert.AreEqual("Bu kart yalnızca sıranın başında, zar atmadan önce kullanılır.", CardHand.UseBlock(Board, Drawn(chauffeur), chauffeur));
            Assert.AreEqual("Bu kartı şimdi kullanabileceğin bir yer yok.", CardHand.UseBlock(Board, Drawn(shield), shield), "no town of their own to protect");
            Assert.IsNull(CardHand.UseBlock(Board, Drawn(shield, s => s.Owners[paris] = 1), shield));
            Assert.AreEqual("Bu kartı şimdi kullanabileceğin bir yer yok.", CardHand.UseBlock(Board, Drawn(demolish), demolish), "no rival town to hit");
            Assert.IsNull(CardHand.UseBlock(Board, Drawn(demolish, s => s.Owners[paris] = 2), demolish));
            Assert.IsNull(CardHand.UseBlock(Board, Drawn(fund), fund), "the fund's cash is always there");
            Assert.AreEqual("Bu sırada bir kart oynadın; bu kart sonraki sıralar için saklanır.",
                CardHand.UseBlock(Board, Drawn(fund, s => s.CardPlayedThisTurn = true), fund), "one card a turn");

            Lang.Set(Language.English);
            Assert.AreEqual("This card is used only while in jail.", CardHand.UseBlock(Board, Drawn(lawyer), lawyer));
            Assert.AreEqual("Use", Lang.L("Kullan"));
            Assert.AreEqual("Keep", Lang.L("Sakla"));
        }

        [Test]
        public void TheBoardShowsAShieldAndAStrikeBesideTheBuildings()
        {
            Mesh shield = MeshFactory.Shield();
            Assert.AreEqual(1f, shield.bounds.size.x, 0.01f);
            Assert.AreEqual(1.2f, shield.bounds.size.y, 0.01f);
            Assert.AreEqual(0f, shield.bounds.min.y, 0.01f, "its point stands on the tile");

            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, _data);
                int paris = Town("Paris");
                Assert.IsFalse(board.ShowsShield(paris));
                board.SetCup(paris);
                board.SetShield(paris, true);
                board.SetStrike(paris, true);
                Assert.IsTrue(board.ShowsShield(paris));
                Assert.IsTrue(board.ShowsStrike(paris));
                foreach (string mark in new[] { "Shield mark", "Strike mark" })
                {
                    Transform shown = holder.GetComponentsInChildren<Transform>(true).Single(t => t.name == mark);
                    Vector3 offset = shown.localPosition - board.Layout.SquareCenter(paris);
                    offset.y = 0f;
                    Assert.Less(offset.magnitude, board.Layout.SquareSize(paris).x / 2f, $"{mark} stands on the town's own tile");
                    Vector3 along = Vector3.Cross(Vector3.up, board.Layout.Outward(paris));
                    Vector3 cup = board.CupTrophy.localPosition - board.Layout.SquareCenter(paris);
                    Assert.Less(Vector3.Dot(offset, along) * Vector3.Dot(cup, along), 0f, $"{mark} stands on the other side of the buildings from the cup");
                }

                board.SetShield(paris, false);
                board.SetStrike(paris, false);
                Assert.IsFalse(board.ShowsShield(paris));
                Assert.IsFalse(board.ShowsStrike(paris));
                board.SetShield(paris, true);
                Assert.AreEqual(1, holder.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Shield mark"), "one shield per town, used again");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
