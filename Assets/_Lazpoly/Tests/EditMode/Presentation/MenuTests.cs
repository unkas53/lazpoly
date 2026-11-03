using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>The main menu's paper look: its colours read, its parts fit the screen, its logo is in the build.</summary>
    public class MenuTests
    {
        [TearDown]
        public void BackToTheGameColours()
        {
            UiTheme.Use(UiPalette.Dusk);
            Lang.Set(Language.Turkish);
        }

        /// <summary>WCAG contrast between two colours (1 = none, 21 = black on white).</summary>
        private static float Contrast(Color a, Color b)
        {
            float la = Linear(a) + 0.05f;
            float lb = Linear(b) + 0.05f;
            return la > lb ? la / lb : lb / la;
        }

        private static float Linear(Color c) => 0.2126f * Channel(c.r) + 0.7152f * Channel(c.g) + 0.0722f * Channel(c.b);

        private static float Channel(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);

        [Test]
        public void PaperInkReadsOnEveryPaperSurface()
        {
            UiPalette paper = UiPalette.Paper;
            foreach (Color surface in new[] { paper.Panel, paper.Card, paper.Field, paper.NoticeFill, paper.SecondaryFill })
            {
                Assert.That(Contrast(paper.Text, surface), Is.GreaterThan(7f), $"text on {surface}");
                Assert.That(Contrast(paper.Muted, surface), Is.GreaterThan(3f), $"muted text on {surface}");
            }

            Assert.That(Contrast(paper.PrimaryLabel, paper.PrimaryFill), Is.GreaterThan(3f), "label on the green button");
            Assert.That(Contrast(paper.StrongLabel, paper.SelectedFill), Is.GreaterThan(4.5f), "label on a selected choice");
            Assert.That(Contrast(paper.StrongLabel, paper.DangerFill), Is.GreaterThan(3f), "label on the coral button");
            Assert.That(Contrast(UiPalette.Ink.OnMasthead, UiPalette.Ink.Masthead), Is.GreaterThan(4.5f), "name on the masthead");
            Assert.That(Contrast(UiPalette.Ink.OnMastheadMuted, UiPalette.Ink.MastheadDeep), Is.GreaterThan(4.5f), "chip on the masthead");
            foreach (Color stripe in UiPalette.Ink.Stripes)
            {
                Assert.That(Contrast(UiPalette.Ink.Deep(stripe), UiPalette.Ink.Wash(stripe)), Is.GreaterThan(4.5f), $"chip ink on its wash for {stripe}");
            }
        }

        [Test]
        public void TheThemeTakesThePaletteItIsGiven()
        {
            UiTheme.Use(UiPalette.Paper);
            Assert.AreEqual(UiPalette.Paper.Panel, UiTheme.Panel);
            Assert.AreEqual(UiPalette.Paper.PrimaryFill, UiTheme.Fill(UiButtonStyle.Primary));
            Assert.AreEqual(UiPalette.Paper.PrimaryLabel, UiTheme.Label(UiButtonStyle.Primary));
            Assert.AreEqual("Paper", UiTheme.Palette.Name);

            UiTheme.Use(UiPalette.Dusk);
            Assert.AreEqual(UiPalette.Dusk.Panel, UiTheme.Panel);
            Assert.AreEqual(UiPalette.Dusk.Text, UiTheme.Text);
            Assert.Throws<System.ArgumentNullException>(() => UiTheme.Use(null));
        }

        [Test]
        public void TheHomePageFitsTheReferenceScreen()
        {
            Assert.That(MenuLayout.MinScreenHeight, Is.LessThanOrEqualTo(UiTheme.ReferenceResolution.y), "the six cards, title and footer stack on 1080 rows");
            Assert.That(MenuLayout.CardHeight(UiTheme.ReferenceResolution.y), Is.GreaterThanOrEqualTo(MenuLayout.MinCardHeight));
            Assert.That(MenuLayout.CardWidth(UiTheme.ReferenceResolution.x), Is.GreaterThan(400f), "a card holds a heading and a line of text");
            Assert.That(MenuLayout.TitleHeight, Is.GreaterThanOrEqualTo(MenuLayout.LogoSize), "the logo fits in the title strip");
            Assert.AreEqual(6, MenuLayout.CardCount);
            // At the smallest height the cards are exactly their minimum: the arithmetic and the minimum agree.
            Assert.That(MenuLayout.CardHeight(MenuLayout.MinScreenHeight), Is.EqualTo(MenuLayout.MinCardHeight).Within(0.01f));
        }

        [Test]
        public void TheLogoIsInTheBuildAndKeyedOut()
        {
            var asset = Resources.Load<TextAsset>(MenuArt.LogoResource);
            Assert.IsNotNull(asset, "run Tools/Art/make_logo.py");
            Texture2D logo = MenuArt.Decode(asset.bytes, "test logo");
            try
            {
                Assert.IsNotNull(logo, "the bytes are a picture");
                Assert.AreEqual(640, logo.width);
                Assert.AreEqual(640, logo.height);
                Assert.That(logo.GetPixel(2, 2).a, Is.LessThan(0.02f), "the corner is see-through");
                Assert.That(logo.GetPixel(logo.width - 3, logo.height - 3).a, Is.LessThan(0.02f), "the other corner is see-through");
                Assert.That(logo.GetPixel(logo.width / 2, logo.height / 2).a, Is.GreaterThan(0.98f), "the middle is painted");
            }
            finally
            {
                if (logo != null) Object.DestroyImmediate(logo);
            }
        }

        [Test]
        public void CapitalsKeepTheirDotsInTurkish()
        {
            Lang.Set(Language.Turkish);
            Assert.AreEqual("ÇEVRİM İÇİ", Lang.Upper("Çevrim içi"));
            Assert.AreEqual("KARADENİZ MASASI", Lang.Upper("Karadeniz Masası"));
            Assert.AreEqual("", Lang.Upper(""));
            Assert.IsNull(Lang.Upper(null));
            Lang.Set(Language.English);
            Assert.AreEqual("ONLINE", Lang.Upper("Online"));
        }
    }
}
