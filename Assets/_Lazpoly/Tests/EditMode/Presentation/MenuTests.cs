using System.IO;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Game;
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
            UiTheme.Use(UiPalette.Paper);
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
            UiTheme.Use(UiPalette.Paper);
            Assert.Throws<System.ArgumentNullException>(() => UiTheme.Use(null));
        }

        [Test]
        public void TheTableUsesPaperToo()
        {
            string controller = File.ReadAllText(Path.Combine(Application.dataPath, "_Lazpoly", "Code", "Presentation", "Game", "GameSceneController.cs"));
            Assert.That(controller, Does.Contain("UiTheme.Use(UiPalette.Paper)"), "the game scene switches to the paper palette");
            Assert.That(controller, Does.Not.Contain("UiTheme.Use(UiPalette.Dusk)"));
            string hud = File.ReadAllText(Path.Combine(Application.dataPath, "_Lazpoly", "Code", "Presentation", "Game", "GameHud.cs"));
            Assert.That(hud, Does.Not.Contain("new Color(0.08f, 0.09f, 0.12f"), "no dark slate panels left over from the dusk look");
            // The current player's corner card stands out from the cream cards and keeps dark ink readable.
            Color current = GameHud.CurrentCardColor;
            Assert.That(Contrast(UiPalette.Paper.Text, current), Is.GreaterThan(4.5f));
            Assert.That(Mathf.Abs(ColorVision.Luminance(current) - ColorVision.Luminance(UiPalette.Paper.Card)), Is.GreaterThan(0.2f), "the card on turn is plainly greener, not a faint tint");
            // Money at rest is gold-brown, readable on cream, and never the red of a loss.
            Assert.That(Contrast(UiPalette.Paper.Money, UiPalette.Paper.Card), Is.GreaterThan(4.5f));
            Assert.That(Contrast(UiPalette.Paper.Money, current), Is.GreaterThan(3f));
            Assert.AreNotEqual(UiPalette.Paper.Warning, UiPalette.Paper.Money);
        }

        [Test]
        public void TheHomePageFitsTheReferenceScreen()
        {
            Assert.That(MenuLayout.MinScreenHeight, Is.LessThanOrEqualTo(UiTheme.ReferenceResolution.y), "the six cards, title and footer stack on 1080 rows");
            Assert.That(MenuLayout.CardHeight(UiTheme.ReferenceResolution.y), Is.GreaterThanOrEqualTo(MenuLayout.MinCardHeight));
            Assert.That(MenuLayout.CardWidth(UiTheme.ReferenceResolution.x), Is.GreaterThan(400f), "a card holds a heading and a line of text");
            Assert.That(MenuLayout.MastheadHeight, Is.GreaterThanOrEqualTo(MenuLayout.LogoSize + 8f), "the logo fits in the masthead with a margin");
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
                // The medallion at the top right holds the navy mark now, not the portrait: no skin tones there.
                Color medallion = logo.GetPixel(logo.width * 3 / 4, logo.height * 3 / 5);
                Assert.That(medallion.a, Is.GreaterThan(0.98f), "the medallion is painted");
                Assert.That(medallion.b, Is.GreaterThan(medallion.r), $"the medallion is blue, not a face: {medallion}");
            }
            finally
            {
                if (logo != null) Object.DestroyImmediate(logo);
            }
        }

        [Test]
        public void TheOpeningScreenHoldsLongEnoughToBeSeen()
        {
            Assert.That(BootSplash.HoldSeconds, Is.GreaterThanOrEqualTo(1f), "a fast machine would flash it otherwise");
            Assert.AreEqual(0f, BootSplash.Progress(0f));
            Assert.AreEqual(0.5f, BootSplash.Progress(BootSplash.HoldSeconds / 2f), 1e-5f);
            Assert.AreEqual(1f, BootSplash.Progress(BootSplash.HoldSeconds * 3f), "the bar stops at the end");
        }

        [Test]
        public void ThePlayerCarriesOurIconAndNoUnitySplash()
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            // Read as checked out on any machine: Windows clones carry CRLF line ends.
            string settings = File.ReadAllText(Path.Combine(project, "ProjectSettings", "ProjectSettings.asset")).Replace("\r\n", "\n");
            Assert.That(settings, Does.Contain("m_ShowUnitySplashScreen: 0"), "the Unity splash is off");
            Assert.That(settings, Does.Contain("m_ShowUnitySplashLogo: 0"));

            string iconPath = Path.Combine(project, "Assets", "_Lazpoly", "Art", "Textures", "lazpoly-icon.png");
            Assert.IsTrue(File.Exists(iconPath), "run Tools/Art/make_logo.py");
            string meta = File.ReadAllText(iconPath + ".meta");
            string guid = System.Text.RegularExpressions.Regex.Match(meta, @"guid: ([0-9a-f]{32})").Groups[1].Value;
            Assert.That(guid, Is.Not.Empty, "the icon's meta names its guid");
            Assert.That(settings, Does.Contain($"m_Icon: {{fileID: 2800000, guid: {guid}, type: 3}}"), "the default icon is the logo");
            string smallMeta = File.ReadAllText(Path.Combine(project, "Assets", "_Lazpoly", "Art", "Textures", "lazpoly-icon-small.png.meta"));
            string smallGuid = System.Text.RegularExpressions.Regex.Match(smallMeta, @"guid: ([0-9a-f]{32})").Groups[1].Value;
            Assert.That(settings, Does.Contain("m_BuildTarget: Standalone"), "the desktop player lists its icon sizes");
            Assert.That(settings, Does.Contain($"guid: {smallGuid}, type: 3}}\n      m_Width: 16"), "the 16 pixel icon is the medallion");
            Assert.That(settings, Does.Contain($"guid: {guid}, type: 3}}\n      m_Width: 256"), "the 256 pixel icon is the whole logo");

            Texture2D icon = MenuArt.Decode(File.ReadAllBytes(iconPath), "test icon");
            try
            {
                Assert.IsNotNull(icon);
                Assert.AreEqual(256, icon.width);
                Assert.AreEqual(256, icon.height);
                Assert.That(icon.GetPixel(1, 1).a, Is.LessThan(0.02f), "the icon's corner is see-through");
            }
            finally
            {
                if (icon != null) Object.DestroyImmediate(icon);
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
