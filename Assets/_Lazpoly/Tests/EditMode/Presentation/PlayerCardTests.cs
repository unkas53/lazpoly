using System.Collections.Generic;
using System.Linq;
using Lazpoly.App.Avatars;
using Lazpoly.App.Lobby;
using Lazpoly.Core.Board;
using Lazpoly.Core.Events;
using Lazpoly.Core.Rules;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Tests
{
    public class PlayerCardTests
    {
        private static readonly int[] Rents = { 50, 150, 400, 550, 700 };

        private static readonly BoardDefinition Board = new BoardDefinition(new[]
        {
            SquareDefinition.Start("Başlangıç"),
            SquareDefinition.Property("Kahire", 60, 4, 1, 50, Rents),
            SquareDefinition.Property("Tunus", 60, 4, 1, 50, Rents),
            SquareDefinition.Property("Lima", 100, 6, 2, 50, Rents),
            SquareDefinition.Rest("Park"),
        });

        private static GameState State(params string[] names)
        {
            if (names.Length == 0) names = new[] { "Ayşe", "Can" };
            return GameState.CreateInitial(Board, new RulesConfig(1000, 200, 6, 3, 2, 50), names.Select((name, i) => new PlayerSeat(i + 1, name)).ToArray());
        }

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void CleanUp()
        {
            foreach (GameObject made in _made) Object.DestroyImmediate(made);
            _made.Clear();
        }

        private sealed class FakeAvatars : IPlayerAvatars
        {
            public readonly Dictionary<ulong, AvatarPicture> Pictures = new Dictionary<ulong, AvatarPicture>();
            public int Asked;

            public AvatarPicture Get(ulong platformId)
            {
                Asked++;
                return Pictures.TryGetValue(platformId, out AvatarPicture picture) ? picture : null;
            }
        }

        [Test]
        public void TheCardKitAndTheCharactersFacesShipWithTheGame()
        {
            foreach (string name in PlayerCardView.Pictures.Concat(CharacterChoice.All.Select(option => option.Face)))
            {
                Assert.IsNotNull(KitArt.Get(name), $"{name} is in Resources/Lazpoly/Art/Menu");
            }

            Assert.AreEqual(new Vector2(640f, 224f), KitArt.Get(PlayerCardView.NormalCard).rect.size, "the card is drawn at twice its 320 by 112 design size");
        }

        [Test]
        public void SteamPicturesAreTurnedRightSideUpForTheTexture()
        {
            byte[] topFirst = { 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4 }; // Two pixels wide, two rows.
            CollectionAssert.AreEqual(new byte[] { 3, 3, 3, 3, 4, 4, 4, 4, 1, 1, 1, 1, 2, 2, 2, 2 }, AvatarSprites.FlipRows(topFirst, 2, 2));
            Assert.Throws<System.ArgumentException>(() => AvatarSprites.FlipRows(topFirst, 3, 2));
        }

        [Test]
        public void EachCardWearsItsPlayersCharacter()
        {
            var seats = new[] { new PlayerSeat(1, "Ayşe", "boss"), new PlayerSeat(2, "Can", "mob"), new PlayerSeat(3, "Ece") };
            GameState state = GameState.CreateInitial(Board, new RulesConfig(1000, 200, 6, 3, 2, 50), seats);
            Assert.AreEqual("Baron", GameSceneController.CharacterOf(state, state.Players[0]).Name);
            Assert.AreEqual("mob", GameSceneController.CharacterOf(state, state.Players[1]).Id);
            Assert.AreEqual(CharacterChoice.ForSeat(2), GameSceneController.CharacterOf(state, state.Players[2]), "no pick (an older game): the seats take turns");
        }

        [Test]
        public void OnlineEveryPersonIsMatchedToTheirSteamAccount()
        {
            var members = new List<LobbyMember> { new LobbyMember(20, "Can"), new LobbyMember(10, "Ayşe"), new LobbyMember(30, "Ece") };
            IReadOnlyList<PlayerState> players = State("Ayşe", "Can", "Ece").Players;
            IReadOnlyDictionary<int, ulong> accounts = SeatAccounts.Online(10, members, players);
            Assert.AreEqual(10UL, accounts[1], "the host plays the first seat");
            Assert.AreEqual(20UL, accounts[2]);
            Assert.AreEqual(30UL, accounts[3]);

            players = State("Ayşe", "Can", "Başkası").Players;
            Assert.IsFalse(SeatAccounts.Online(10, members, players).ContainsKey(3), "a seat under another name gets nobody's picture");
            Assert.AreEqual(0, SeatAccounts.Online(99, members, players).Count, "a host outside the room maps nobody");

            Assert.AreEqual(77UL, SeatAccounts.Local(2, 77)[2], "at a shared computer only the one who set it up is the account here");
            Assert.AreEqual(0, SeatAccounts.Local(GameState.NoOwner, 77).Count);
            Assert.AreEqual(0, SeatAccounts.Local(2, 0).Count, "no Steam, no account");
        }

        [Test]
        public void TheSteamPictureReplacesThePortraitOnceItArrives()
        {
            var avatars = new FakeAvatars();
            var portraits = new Dictionary<int, string> { [1] = CharacterChoice.All[1].Face, [2] = CharacterChoice.All[2].Face };
            var faces = new PlayerFaces(avatars, new Dictionary<int, ulong> { [1] = 55 }, portraits);
            try
            {
                Assert.AreEqual(CharacterChoice.All[1].Face, faces.For(1)?.name, "the character stands in while the picture is on its way");
                Assert.IsFalse(faces.Update(0f));
                avatars.Pictures[55] = new AvatarPicture(4, 4, Enumerable.Repeat((byte)200, 64).ToArray());
                Assert.IsFalse(faces.Update(0.1f), "asked again only every half second");
                Assert.IsTrue(faces.Update(PlayerFaces.PollSeconds + 0.01f));
                Assert.AreEqual(1, faces.Version);
                Assert.IsTrue(faces.HasPhoto(1));
                Assert.AreEqual(4f, faces.For(1).rect.width);
                Assert.IsFalse(faces.HasPhoto(2));
                Assert.AreEqual(CharacterChoice.All[2].Face, faces.For(2)?.name, "a computer keeps its character's face");
                Assert.IsNull(faces.For(9), "nobody at seat nine");

                int asked = avatars.Asked;
                faces.Update(10f);
                Assert.AreEqual(asked, avatars.Asked, "a picture is fetched once");
            }
            finally
            {
                faces.Dispose();
            }

            var nobody = new PlayerFaces(null, new Dictionary<int, ulong> { [1] = 55 }, portraits);
            Assert.IsFalse(nobody.Update(1f), "without Steam nothing is asked");
            Assert.IsNotNull(nobody.For(1), "the character's face without Steam");
        }

        [Test]
        public void OneHousePerColourGroupOpensItsFirstTown()
        {
            GameState state = State();
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(2, GameState.NoOwner, 1));
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(1, GameState.NoOwner, 1));
            GameStateReducer.Apply(state, new PropertyOwnerChangedEvent(3, GameState.NoOwner, 1));
            List<PlayerCardPip> pips = PlayerCardView.GroupsOf(state, 1, square => Board[square].GroupId, square => square == 3 ? Color.blue : Color.red);
            Assert.AreEqual(2, pips.Count);
            Assert.AreEqual(1, pips[0].Square, "the group's first town, in board order");
            Assert.AreEqual(Color.red, pips[0].Color);
            Assert.AreEqual(3, pips[1].Square);
            Assert.AreEqual(0, PlayerCardView.GroupsOf(state, 2, square => Board[square].GroupId, _ => Color.red).Count);
        }

        private PlayerCardView Card(PlayerCardState cardState, Sprite face = null)
        {
            var parent = new GameObject("Corner", typeof(RectTransform));
            _made.Add(parent);
            var model = new PlayerCardModel
            {
                Name = "Ayşe",
                Number = PlayerCardView.NumberOf(0),
                Color = Color.red,
                State = cardState,
                Face = face,
                Initial = "A",
                Pill = "SIRA SENDE",
                Status = "Bilgisayar",
                Pips = new[] { new PlayerCardPip(Color.green, 1), new PlayerCardPip(Color.blue, 3) },
            };
            return PlayerCardView.Create((RectTransform)parent.transform, 300f, model, _ => { });
        }

        private static Text Named(PlayerCardView card, string name) =>
            card.Root.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.name == name);

        [Test]
        public void ARestingCardShowsTheFaceNumberMoneyAndHouses()
        {
            PlayerCardView card = Card(PlayerCardState.Normal, KitArt.Get(CharacterChoice.All[0].Face));
            LayoutElement size = card.Root.GetComponent<LayoutElement>();
            Assert.AreEqual(300f, size.preferredWidth);
            Assert.AreEqual(105f, size.preferredHeight, 0.01f, "the kit's 320 by 112 card, scaled to the corner");
            Assert.AreEqual(PlayerCardView.NormalCard, card.Background.sprite.name);
            Assert.IsNotNull(card.Face);
            Assert.IsNotNull(card.Face.GetComponentInParent<Mask>(), "the face is clipped to the disc");
            Assert.AreEqual("01", Named(card, "Number").text);
            Assert.IsNotNull(card.Balance);
            Assert.AreEqual(2, card.Pips.Count);
            Assert.AreEqual(Color.green, card.Pips[0].color);
            Assert.IsNotNull(card.Pips[0].GetComponentInParent<SquareClickTarget>(), "a house opens its town");
            Assert.AreEqual("Bilgisayar", Named(card, "Status").text);
            Assert.IsNull(card.Root.Find("Turn Pill"), "only the card on turn has the pill");
        }

        [Test]
        public void TheCardOnTurnHasThePillAndAClockLine()
        {
            PlayerCardView card = Card(PlayerCardState.Active);
            Assert.AreEqual(PlayerCardView.ActiveCard, card.Background.sprite.name);
            Assert.AreEqual("SIRA SENDE", Named(card, "Label").text);
            Assert.IsNull(card.Face);
            Assert.AreEqual("A", Named(card, "Initials").text, "no picture: the initial on the player's colour");
            Image fill = card.Root.Find("Turn Time").GetComponent<Image>();
            Assert.IsFalse(fill.gameObject.activeSelf, "no clock until one runs");
            card.SetTime(0.25f);
            Assert.IsTrue(fill.gameObject.activeSelf);
            Assert.AreEqual(0.25f, fill.fillAmount, 0.001f);
            card.SetTime(null);
            Assert.IsFalse(fill.gameObject.activeSelf);
        }

        [Test]
        public void ABankruptCardGoesGreyAndSaysSo()
        {
            PlayerCardView card = Card(PlayerCardState.Bankrupt, KitArt.Get(CharacterChoice.All[0].Face));
            Assert.AreEqual(PlayerCardView.BankruptCard, card.Background.sprite.name);
            Assert.IsNull(card.Balance, "no money to count");
            Assert.AreEqual(0, card.Pips.Count);
            Assert.AreEqual("İFLAS", Named(card, "Bankrupt").text);
            Assert.Less(card.Face.color.a, 1f, "the face fades");
            Assert.AreEqual(PlayerCardView.Muted, card.Root.Find("Avatar").GetComponent<Image>().color);
        }

        [Test]
        public void TheCornerCardsAreDrawnLargerThanTheKitsDesign()
        {
            Assert.Greater(UiLayout.CornerWidth, PlayerCardView.DesignWidth, "the cards were asked to be a little bigger");
            // Eight players stack two cards in each corner; both stay in the top or bottom third of the 1080 screen.
            float height = PlayerCardView.DesignHeight * UiLayout.CornerWidth / PlayerCardView.DesignWidth;
            Assert.Less(UiLayout.Margin + 2f * height + UiTheme.Spacing, 1080f / 3f);
        }

        [Test]
        public void TheMenusProfileShowsTheSteamPictureInPlaceOfTheLetter()
        {
            var frame = new GameObject("Frame", typeof(RectTransform));
            _made.Add(frame);
            var home = new MenuHome((RectTransform)frame.transform, new MenuHomeActions(), "");
            home.SetProfile("Unkas", "Aras");
            Assert.IsFalse(home.ShowsPhoto, "the letter until Steam has the picture");
            Text initial = frame.GetComponentsInChildren<Text>(true).First(text => text.name == "Profile Initial");
            Assert.IsTrue(initial.gameObject.activeSelf);

            Sprite photo = AvatarSprites.ToSprite(new AvatarPicture(2, 2, Enumerable.Repeat((byte)90, 16).ToArray()));
            try
            {
                home.SetPhoto(photo);
                Assert.IsTrue(home.ShowsPhoto);
                Assert.IsFalse(initial.gameObject.activeSelf);
                Image picture = frame.GetComponentsInChildren<Image>(true).First(image => image.sprite == photo);
                Assert.IsTrue(picture.gameObject.activeInHierarchy, "the round window opens with the picture");
                Assert.IsNotNull(picture.GetComponentInParent<Mask>(), "the picture is cut round");
                home.SetPhoto(null);
                Assert.IsFalse(home.ShowsPhoto);
                Assert.IsTrue(initial.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(photo.texture);
                Object.DestroyImmediate(photo);
            }
        }
    }
}
