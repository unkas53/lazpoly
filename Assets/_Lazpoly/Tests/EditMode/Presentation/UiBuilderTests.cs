using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Tests
{
    public class UiBuilderTests
    {
        private GameObject _root;
        private RectTransform _container;

        [SetUp]
        public void CreateContainer()
        {
            _root = new GameObject("UI Test Root", typeof(RectTransform));
            _container = (RectTransform)_root.transform;
        }

        [TearDown]
        public void DestroyContainer() => Object.DestroyImmediate(_root);

        [Test]
        public void ButtonRunsItsActionThenAsksForARedraw()
        {
            int clicks = 0, redraws = 0;
            var ui = new UiBuilder(_container, () => redraws++);
            Button button = ui.Button("Zar At", () => clicks++);

            button.onClick.Invoke();

            Assert.AreEqual(1, clicks);
            Assert.AreEqual(1, redraws);
            Assert.AreEqual("Zar At", button.GetComponentInChildren<Text>().text);
        }

        [Test]
        public void RoundButtonCarriesItsCaptionAndTakesTheClickAsAWhole()
        {
            int clicks = 0;
            var ui = new UiBuilder(_container);
            Button button = ui.RoundButton(UiIcon.Dice, "Zar At", () => clicks++, style: UiButtonStyle.Primary);
            Assert.AreEqual("Zar At", button.GetComponentInChildren<Text>().text, "the caption names the button");
            Assert.IsNotNull(button.GetComponent<UiPrimaryMark>(), "the primary round button takes the keyboard focus first");
            Assert.AreEqual(UiSprites.Circle, button.targetGraphic.GetComponent<Image>().sprite, "the disc is round");
            button.onClick.Invoke();
            Assert.AreEqual(1, clicks);

            Button grey = ui.RoundButton(UiIcon.Book, "Yardım (H)", () => { }, enabled: false, style: UiButtonStyle.Primary);
            Assert.IsFalse(grey.interactable);
            Assert.AreEqual(UiTheme.RestingFill, grey.targetGraphic.color, "a resting disc keeps a visible fill instead of the disabled tint");
            Assert.AreEqual(Color.white, grey.colors.disabledColor, "nothing dims the resting disc further");
            Assert.IsNull(grey.GetComponent<UiPrimaryMark>(), "a resting primary button does not claim the focus");
            Assert.AreEqual(2, _container.childCount, "each round button is one column in the dock");
        }

        [Test]
        public void DisabledButtonIsNotInteractable()
        {
            Button button = new UiBuilder(_container).Button("Satın Al", () => { }, enabled: false);
            Assert.IsFalse(button.interactable);
        }

        [Test]
        public void RowsAndCardsPassTheRedrawRequestOn()
        {
            int redraws = 0;
            var ui = new UiBuilder(_container, () => redraws++);
            ui.Card().Row().Button("+10", () => { }).onClick.Invoke();
            Assert.AreEqual(1, redraws);
        }

        [Test]
        public void ToggleReportsItsValue()
        {
            bool? reported = null;
            Toggle toggle = new UiBuilder(_container).Toggle("Tam ekran", false, on => reported = on);
            toggle.isOn = true;
            Assert.AreEqual(true, reported);
        }

        [Test]
        public void InputShowsItsValueWithoutReportingIt()
        {
            string reported = null;
            InputField input = new UiBuilder(_container).Input("Oyuncu 1", text => reported = text);
            Assert.AreEqual("Oyuncu 1", input.text);
            Assert.IsNull(reported);
            input.text = "Ayşe";
            Assert.AreEqual("Ayşe", reported);
        }

        [Test]
        public void ClearRemovesEverything()
        {
            var ui = new UiBuilder(_container);
            ui.Label("a");
            ui.Button("b", null);
            ui.Clear();
            Assert.AreEqual(0, _container.childCount);
        }

        [Test]
        public void ViewRebuildsOnlyWhenItsKeyChangesOrItIsInvalidated()
        {
            int renders = 0;
            var view = new UiView(new UiBuilder(_container), ui =>
            {
                renders++;
                ui.Button("x", () => { });
            });

            Assert.IsTrue(view.Refresh(1));
            Assert.IsFalse(view.Refresh(1));
            Assert.IsTrue(view.Refresh(2));

            _container.GetComponentInChildren<Button>().onClick.Invoke();
            Assert.IsTrue(view.Refresh(2), "A click must redraw the view.");
            Assert.AreEqual(3, renders);
            Assert.AreEqual(1, _container.childCount, "Old content must be removed.");
        }

        [Test]
        public void TileRunsItsActionAndHoldsItsContent()
        {
            int clicks = 0;
            var ui = new UiBuilder(_container);
            UiBuilder tile = ui.Tile(() => clicks++);
            tile.Label("Tek Oyunculu");
            Button button = _container.GetComponentInChildren<Button>();
            Assert.IsNotNull(button, "A tile is a button.");
            Assert.IsNotNull(button.GetComponent<UiHover>(), "Buttons answer the pointer.");
            Assert.AreEqual(1, button.GetComponentsInChildren<Text>().Length);
            button.onClick.Invoke();
            Assert.AreEqual(1, clicks);
        }

        [Test]
        public void EveryIconIsDrawnFromShapesAndAnIconButtonCarriesOne()
        {
            var ui = new UiBuilder(_container);
            foreach (UiIcon icon in System.Enum.GetValues(typeof(UiIcon)))
            {
                RectTransform drawn = ui.Icon(icon, 24f);
                // The kit's line icon is one picture; an icon the kit lacks is drawn from two shapes or more.
                int least = UiIcons.KitPicture(icon) != null ? 1 : 2;
                Assert.GreaterOrEqual(drawn.GetComponentsInChildren<Image>().Length, least, $"{icon} has too few shapes.");
                foreach (Image shape in drawn.GetComponentsInChildren<Image>()) Assert.IsFalse(shape.raycastTarget, $"{icon} would take clicks.");
            }

            Button button = ui.IconButton(UiIcon.Door, "Çıkış", () => { });
            Assert.AreEqual("Çıkış", button.GetComponentInChildren<Text>().text);
            Assert.IsTrue(button.transform.Find("Icon Door") != null, "The icon sits inside the button.");
        }

        [Test]
        public void ButtonsCardsAndWindowsWearTheKitsSkins()
        {
            var ui = new UiBuilder(_container);
            Button play = ui.Button("Oyunu Başlat", () => { }, style: UiButtonStyle.Primary);
            var playSkin = (Image)play.targetGraphic;
            Assert.AreSame(KitArt.Get("Primary_Normal"), playSkin.sprite, "the main action is the kit's dark petrol button");
            Assert.AreEqual(Selectable.Transition.SpriteSwap, play.transition);
            Assert.AreSame(KitArt.Get("Primary_Hover"), play.spriteState.highlightedSprite);
            Assert.AreSame(KitArt.Get("Primary_Pressed"), play.spriteState.pressedSprite);
            Assert.AreSame(KitArt.Get("Primary_Disabled"), play.spriteState.disabledSprite);
            Assert.AreEqual(Image.Type.Sliced, playSkin.type);
            Assert.AreEqual(Color.white, playSkin.color, "the skin is drawn as the kit drew it");
            Assert.IsNull(play.GetComponent<Shadow>(), "the skin has its own shadow");
            Assert.That(playSkin.pixelsPerUnitMultiplier, Is.EqualTo(KitArt.ButtonDesignHeight / UiTheme.ButtonHeight).Within(0.001f),
                "a thinner button keeps the kit's proportions");

            Button back = ui.Button("Geri", () => { });
            Assert.AreSame(KitArt.Get("Secondary_Normal"), back.targetGraphic.GetComponent<Image>().sprite, "the rest are the cream ones");
            Button chosen = ui.Button("Orta", () => { }, style: UiButtonStyle.Selected);
            Assert.AreSame(KitArt.Get("Primary_Hover"), chosen.targetGraphic.GetComponent<Image>().sprite, "a choice made is the lighter petrol");
            Button leave = ui.Button("Çık", () => { }, style: UiButtonStyle.Danger);
            Assert.AreSame(KitArt.Get("Secondary_Normal"), leave.targetGraphic.GetComponent<Image>().sprite, "leaving is the kit's cream button, as its Oyundan Çık");
            Assert.AreEqual(UiTheme.Warning, leave.GetComponentInChildren<Text>().color, "told apart by brick red words");
            Button resting = ui.Button("Satın Al", () => { }, enabled: false, style: UiButtonStyle.Primary);
            Assert.Less(resting.GetComponentInChildren<Text>().color.a, 1f, "a button that cannot be pressed fades its label");

            Image card = ui.Card().Container.GetComponent<Image>();
            Assert.AreSame(KitArt.Get(KitArt.Chip), card.sprite, "a card is the kit's chip");
            Assert.AreEqual(Color.white, card.color);
            Assert.AreEqual(Color.red, ui.Card(Color.red).Container.GetComponent<Image>().color, "a coloured card tints the chip");
            Image tile = ui.Tile(() => { }).Container.GetComponent<Image>();
            Assert.AreSame(KitArt.Get(KitArt.Chip), tile.sprite, "a clickable card too");

            Image window = UiRoot.Panel(UiRoot.Stretch(_container, "Window"));
            Assert.AreSame(KitArt.Get(KitArt.Panel), window.sprite, "a window is the kit's paper with its folded corner");
            Assert.AreEqual(1f, window.pixelsPerUnitMultiplier, "its corners as drawn");
            Assert.AreSame(KitArt.Get(KitArt.Chip), ui.Input("Oyuncu", null).targetGraphic.GetComponent<Image>().sprite, "a field is the kit's field");
        }

        [Test]
        public void KitCornersShrinkOnThinPartsAndNeverGrow()
        {
            Assert.That(KitArt.CornerScale(42f), Is.EqualTo(2f).Within(0.001f));
            Assert.AreEqual(1f, KitArt.CornerScale(KitArt.ButtonDesignHeight));
            Assert.AreEqual(1f, KitArt.CornerScale(200f), "a tall part keeps the kit's corners, not bigger ones");
            Assert.AreEqual(1f, KitArt.CornerScale(0f), "no height: as drawn");
            foreach (UiIcon icon in System.Enum.GetValues(typeof(UiIcon)))
            {
                string picture = UiIcons.KitPicture(icon);
                if (picture != null) Assert.Contains(picture, Lazpoly.Presentation.Menu.MenuKit.Pictures, $"{icon}'s kit icon ships with the game");
            }
        }

        [Test]
        public void CircleSpriteIsRoundAndRoundSwatchesUseIt()
        {
            Assert.AreEqual(1f, UiSprites.CircleCoverage(16, 16));
            Assert.AreEqual(0f, UiSprites.CircleCoverage(0, 0));
            Assert.AreEqual(0f, UiSprites.CircleCoverage(31, 31));
            Assert.AreSame(UiSprites.Circle, UiSprites.Circle);

            var ui = new UiBuilder(_container);
            Assert.AreSame(UiSprites.Circle, ui.Swatch(Color.red, 20f, round: true).sprite);
            Assert.AreSame(UiSprites.Rounded, ui.Swatch(Color.red, 20f).sprite);
        }

        [Test]
        public void RoundedSpriteIsOpaqueInsideAndClearInTheCorner()
        {
            Assert.AreEqual(1f, UiSprites.Coverage(16, 16));
            Assert.AreEqual(0f, UiSprites.Coverage(0, 0));
            Assert.IsNotNull(UiSprites.Rounded);
            Assert.Greater(UiSprites.Rounded.border.x, 0f);
        }
    }
}
