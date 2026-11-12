using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The property pictures' sky, the rounded pictures on the cards, the level tiles' ring and the cards' dark band.
    /// </summary>
    public class PropertyPictureTests
    {
        private GameObject _root;
        private RectTransform _container;

        [SetUp]
        public void CreateContainer()
        {
            _root = new GameObject("UI Test Root", typeof(RectTransform));
            _container = (RectTransform)_root.transform;
            Lang.Set(Lazpoly.Infra.Settings.Language.Turkish);
        }

        [TearDown]
        public void DestroyContainer() => Object.DestroyImmediate(_root);

        private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        [Test]
        public void ThePicturesSkyIsTheTablesOwnPalerUpwardsWithCloudsAlongTheBottomOnly()
        {
            // The table's blue at the sides, paler towards the top.
            Color low = PreviewSkin.ColourAt(0.02f, 0.4f), high = PreviewSkin.ColourAt(0.02f, 0.98f);
            Assert.Greater(low.b, low.r + 0.15f, "blue sky");
            Assert.Greater(Luma(high), Luma(low), "paler towards the top");
            Assert.AreEqual(1f, low.a);

            // A soft warm glow behind the building: lighter in the middle than at the sides at the same height, never white.
            Color middle = PreviewSkin.ColourAt(PreviewSkin.GlowCentre.x, PreviewSkin.GlowCentre.y), side = PreviewSkin.ColourAt(0.02f, PreviewSkin.GlowCentre.y);
            Assert.Greater(Luma(middle), Luma(side) + 0.02f);
            Assert.Greater(middle.b - middle.r, 0.02f, "still sky, not a white disc");

            // The cloud bank lies along the bottom; above its reach the sky is clear, so the island floats over the clouds.
            float bank = 0f;
            for (int i = 0; i < 32; i++)
            {
                float u = (i + 0.5f) / 32f;
                bank += Luma(PreviewSkin.ColourAt(u, 0.02f)) / 32f;
                for (float v = PreviewSkin.CloudReach + 0.01f; v < 1f; v += 0.07f)
                {
                    Color sky = PreviewSkin.ColourAt(u, v), again = PreviewSkin.ColourAt(u, v);
                    Assert.AreEqual(sky, again);
                    Assert.Greater(sky.b, sky.r + 0.01f, $"clear sky at {u:0.00}, {v:0.00}");
                }
            }

            Assert.Greater(bank, 0.88f, "cloud along the bottom edge");

            // Soft everywhere: no step between neighbouring points of the picture.
            for (int y = 0; y < PreviewSkin.Height - 1; y += 3)
            {
                for (int x = 0; x < PreviewSkin.Width - 1; x += 3)
                {
                    Color here = PreviewSkin.ColourAt((x + 0.5f) / PreviewSkin.Width, (y + 0.5f) / PreviewSkin.Height);
                    Color right = PreviewSkin.ColourAt((x + 1.5f) / PreviewSkin.Width, (y + 0.5f) / PreviewSkin.Height);
                    Color up = PreviewSkin.ColourAt((x + 0.5f) / PreviewSkin.Width, (y + 1.5f) / PreviewSkin.Height);
                    Assert.Less(Mathf.Abs(Luma(here) - Luma(right)), 0.2f);
                    Assert.Less(Mathf.Abs(Luma(here) - Luma(up)), 0.2f);
                }
            }

            Texture2D picture = PreviewSkin.Backdrop();
            Assert.AreEqual(PreviewSkin.Width, picture.width);
            Assert.AreEqual(PreviewSkin.Height, picture.height);
            Assert.That((float)picture.width / picture.height, Is.EqualTo((float)BuildingPreview.PictureWidth / BuildingPreview.PictureHeight).Within(1e-3f));
            Assert.AreSame(picture, PreviewSkin.Backdrop(), "drawn once");
        }

        [Test]
        public void APictureOnACardHasRoundedCornersAndOneMaterialPerLook()
        {
            var size = new Vector2(PropertyCard.PictureWidth, PropertyCard.PictureHeight);
            var whole = new Rect(0f, 0f, 1f, 1f);
            Assert.AreEqual(UiPictures.Key(size, 8f, whole, 0f), UiPictures.Key(size, 8f, whole, 0f));
            Assert.AreNotEqual(UiPictures.Key(size, 8f, whole, 0f), UiPictures.Key(size, 8f, whole, 0.8f), "a greyed picture is its own look");
            Assert.AreNotEqual(UiPictures.Key(size, 8f, whole, 0f), UiPictures.Key(size, 8f, new Rect(0.1f, 0f, 0.8f, 0.8f), 0f), "and so is a close-up");
            Assert.AreNotEqual(UiPictures.Key(size, 8f, whole, 0f), UiPictures.Key(size * 2f, 8f, whole, 0f));

            RectTransform box = UiRoot.Rect(_container, "Picture", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Texture2D texture = Texture2D.whiteTexture;
            var close = new Rect(0.125f, 0f, 0.75f, 0.75f);
            RawImage image = UiPictures.Show(box, texture, size, 8f, close, 0.5f);
            Assert.AreSame(texture, image.texture);
            Assert.IsFalse(image.raycastTarget, "a picture never takes the click from its tile");
            if (UiPictures.IsAvailable)
            {
                Material material = image.material;
                Assert.AreSame(material, UiPictures.MaterialFor(size, 8f, close, 0.5f), "kept, not made again");
                Assert.AreEqual(new Vector4(size.x, size.y, 0f, 0f), material.GetVector("_Size"));
                Assert.AreEqual(8f, material.GetFloat("_Radius"));
                Assert.AreEqual(new Vector4(close.x, close.y, close.width, close.height), material.GetVector("_View"));
                Assert.AreEqual(0.5f, material.GetFloat("_Grey"));
                Assert.That(material.GetColor("_Edge").a, Is.EqualTo(UiPictures.EdgeAlpha).Within(1e-4f), "a thin edge line");
                Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), image.uvRect, "the shader cuts the part, so the corners stay where the box's are");
                Assert.LessOrEqual(UiPictures.MaterialFor(new Vector2(20f, 10f), 40f, whole, 0f).GetFloat("_Radius"), 5f, "a corner is never rounder than half the short side");
            }
            else
            {
                Assert.AreEqual(close, image.uvRect, "without the shader: square corners, the same part");
            }
        }

        [Test]
        public void TheRingIsTheRoundedCardsOutlineAndEmptyInside()
        {
            // On the straight sides the line is as thick as it says; the middle and the corners outside the shape are empty.
            float across = 0f;
            for (int x = 0; x < 16; x++) across += UiSprites.RingCoverage(x, 16);
            Assert.That(across, Is.EqualTo(UiSprites.RingWidth).Within(0.01f));
            Assert.AreEqual(0f, UiSprites.RingCoverage(16, 16));
            Assert.AreEqual(0f, UiSprites.RingCoverage(0, 0), "outside the rounded corner");
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float ring = UiSprites.RingCoverage(x, y);
                    Assert.That(ring, Is.InRange(0f, 1f));
                    Assert.LessOrEqual(ring, UiSprites.Coverage(x, y) + 1e-5f, "never outside the card's own shape");
                    Assert.That(ring, Is.EqualTo(UiSprites.RingCoverage(31 - x, 31 - y)).Within(1e-4f), "the same all round");
                }
            }

            Sprite sprite = UiSprites.Ring;
            Assert.AreSame(sprite, UiSprites.Ring);
            Assert.AreEqual(UiSprites.Rounded.border, sprite.border, "it stretches like the card it rings");
        }

        [Test]
        public void AChoiceIsAFlatCardRingedInTheAccentWhenChosen()
        {
            int clicks = 0, redraws = 0;
            var ui = new UiBuilder(_container, () => redraws++);
            UiBuilder chosen = ui.Choice(() => clicks++, chosen: true);
            UiBuilder open = ui.Choice(() => clicks++, chosen: false);
            UiBuilder shut = ui.Choice(null, chosen: false, dimmed: true);

            Image ring = chosen.Container.Find("Ring").GetComponent<Image>();
            Assert.AreEqual(UiSprites.Ring, ring.sprite);
            Assert.AreEqual(Image.Type.Sliced, ring.type);
            Assert.AreEqual(UiTheme.Accent, ring.color, "a chosen tile is ringed in the accent");
            Assert.IsFalse(ring.raycastTarget);
            Assert.IsTrue(ring.GetComponent<LayoutElement>().ignoreLayout, "the ring lies over the tile, outside its stack");
            Assert.That(open.Container.Find("Ring").GetComponent<Image>().color.a, Is.EqualTo(UiBuilder.ChoiceEdge).Within(1e-4f), "the others keep a faint edge");

            var ground = chosen.Container.GetComponent<Image>();
            Assert.AreEqual(UiSprites.Rounded, ground.sprite, "flat, the shape the ring follows");
            Assert.AreEqual(Color.Lerp(UiTheme.Card, UiTheme.Accent, UiBuilder.ChosenWash), ground.color);
            Assert.AreEqual(UiTheme.Card, open.Container.GetComponent<Image>().color);
            Assert.AreEqual(UiTheme.Field, shut.Container.GetComponent<Image>().color, "a tile that is not open yet sinks");

            chosen.Container.GetComponent<Button>().onClick.Invoke();
            open.Container.GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(2, clicks);
            Assert.AreEqual(2, redraws);
            Button still = shut.Container.GetComponent<Button>();
            Assert.IsFalse(still.interactable, "a tile with nothing to do does not answer the pointer");
            Assert.AreEqual(Color.white, still.colors.disabledColor, "and keeps its look");

            // What is added to a choice stacks inside it.
            chosen.Label("Ev 1");
            Assert.IsNotNull(chosen.Container.GetComponent<VerticalLayoutGroup>());
            Assert.AreEqual(2, chosen.Container.childCount);
        }

        [Test]
        public void ACardAboutASquareOpensWithTheDarkBandItsNameInCapitalsAndACloseMark()
        {
            int closed = 0;
            var ui = new UiBuilder(_container);
            Color group = new Color(0.89f, 0.80f, 0.59f);
            PropertyCard.Band(ui, "Kahire", "Satılık şehir · Kuzey Afrika", group, "A", () => closed++);

            Assert.AreEqual(1, _container.childCount, "one band");
            Transform band = _container.GetChild(0);
            Assert.AreEqual(PropertyCard.BandHeight, band.GetComponent<LayoutElement>().preferredHeight);
            Assert.IsFalse(band.GetComponent<Image>().raycastTarget);
            Assert.AreEqual(group, band.Find("Ribbon").GetComponent<Image>().color, "the group's colour on a ribbon");

            Text[] words = band.GetComponentsInChildren<Text>();
            Text name = System.Array.Find(words, text => text.text == "KAHİRE");
            Assert.IsNotNull(name, "the name in capitals");
            Assert.AreEqual(UiTheme.BlackFont, name.font);
            Assert.AreEqual(UiPalette.Ink.OnMasthead, name.color);
            Assert.IsTrue(name.resizeTextForBestFit, "a long name shrinks instead of wrapping");
            Text over = System.Array.Find(words, text => text.text == "SATILIK ŞEHİR · KUZEY AFRİKA");
            Assert.IsNotNull(over, "its kind over it, in small capitals");
            Assert.AreEqual(UiPalette.Ink.Gold, over.color);
            Assert.IsNotNull(System.Array.Find(words, text => text.text == "A"), "the group's letter for those who do not see the colour");

            Button close = band.Find("Button ×").GetComponent<Button>();
            Assert.AreEqual(UiSprites.Circle, ((Image)close.targetGraphic).sprite);
            close.onClick.Invoke();
            Assert.AreEqual(1, closed);

            // Without a letter mark (a special square) the band still opens the same way.
            PropertyCard.Band(ui, "Hapis", null, UiTheme.Accent, null, () => { });
            Assert.AreEqual(2, _container.childCount);
        }
    }
}
