using System.Collections.Generic;
using System.IO;
using Lazpoly.Core.Board;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>The main menu: its colours read, its parts fit the design, its pictures and logo are in the build, the character pick is kept.</summary>
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
            Assert.That(Mathf.Abs(ColorVision.Luminance(current) - ColorVision.Luminance(UiPalette.Paper.Card)), Is.GreaterThan(0.2f), "the card on turn is plainly darker, not a faint tint");
            // Money at rest is gold-brown, readable on cream, and never the red of a loss.
            Assert.That(Contrast(UiPalette.Paper.Money, UiPalette.Paper.Card), Is.GreaterThan(4.5f));
            Assert.That(Contrast(UiPalette.Paper.Money, current), Is.GreaterThan(3f));
            Assert.AreNotEqual(UiPalette.Paper.Warning, UiPalette.Paper.Money);
        }

        [Test]
        public void TheHomeScreensPartsStayInTheDesignAndNeverOverlap()
        {
            var design = new Rect(Vector2.zero, MenuLayout.Design);
            var parts = new List<KeyValuePair<string, Rect>>(MenuLayout.HomeParts);
            foreach (KeyValuePair<string, Rect> part in parts)
            {
                Assert.IsTrue(design.Contains(part.Value.min) && design.Contains(part.Value.max), $"{part.Key} leaves the 1920x1080 design");
            }

            for (int i = 0; i < parts.Count; i++)
            {
                for (int j = i + 1; j < parts.Count; j++)
                {
                    // The profile card holds its disc, name and caption; nothing else may share room.
                    Assert.IsFalse(parts[i].Value.Overlaps(parts[j].Value), $"{parts[i].Key} overlaps {parts[j].Key}");
                }
            }

            Assert.IsTrue(MenuLayout.Profile.Contains(MenuLayout.ProfileDisc.center) && MenuLayout.Profile.Contains(MenuLayout.ProfileName.center));
            Assert.That(MenuLayout.Play.height, Is.GreaterThan(MenuLayout.Host.height), "playing at once is the big button");
            Assert.IsTrue(design.Contains(MenuLayout.Sheet.min) && design.Contains(MenuLayout.Sheet.max));
            float cards = 4f * MenuLayout.CharacterCardWidth + 3f * MenuLayout.CharacterCardGap;
            Assert.That(cards + 80f, Is.LessThanOrEqualTo(MenuLayout.CharacterPanel.width), "the four portraits fit their window");
        }

        [Test]
        public void TheDesignFrameShowsWholeOnEveryScreenShape()
        {
            Assert.AreEqual(1f, MenuLayout.FrameScale(new Vector2(1920f, 1080f)), 1e-5f);
            Assert.AreEqual(1f, MenuLayout.FrameScale(new Vector2(2560f, 1080f)), 1e-5f, "a wide screen keeps the height and shows more city");
            Assert.AreEqual(1600f / 1920f, MenuLayout.FrameScale(new Vector2(1600f, 1200f)), 1e-5f, "a squarer screen keeps the width");
        }

        [Test]
        public void TheFirstSeatGoesByTheSteamNameUnlessTheyTypedTheirOwn()
        {
            Assert.AreEqual("UNKAS", MainMenuController.FirstSeatName("Oyuncu 1", "Oyuncu 1", " UNKAS "));
            Assert.AreEqual("Mami", MainMenuController.FirstSeatName("Mami", "Oyuncu 1", "UNKAS"), "a typed name stays");
            Assert.AreEqual("Oyuncu 1", MainMenuController.FirstSeatName("Oyuncu 1", "Oyuncu 1", ""), "no Steam, no change");
            Assert.AreEqual("Oyuncu 1", MainMenuController.FirstSeatName("Oyuncu 1", "Oyuncu 1", null));
        }

        [Test]
        public void EveryPictureOfTheMenuKitIsInTheBuild()
        {
            foreach (string name in MenuKit.Pictures)
            {
                Sprite sprite = MenuKit.Get(name);
                Assert.IsNotNull(sprite, $"{name}: run Tools/Art/make_menu_art.py");
                Assert.AreEqual(MenuKit.Borders(name), sprite.border, $"{name} stretches by its kit borders");
                if (sprite.border != Vector4.zero)
                {
                    Assert.AreEqual(1, sprite.texture.mipmapCount, $"{name}: a squeezed skin has no blurred mips to draw pale stripes");
                }
            }

            Assert.AreEqual(new Vector4(32f, 32f, 32f, 32f), MenuKit.Borders("Primary_Normal"));
            Assert.AreEqual(Vector4.zero, MenuKit.Borders("Icon_Dice"));
            Sprite background = MenuKit.Get(MenuKit.Background);
            Assert.That(background.rect.width / background.rect.height, Is.EqualTo(16f / 9f).Within(0.05f), "the city picture is a wide screen's shape");
            Assert.That(background.rect.width, Is.GreaterThanOrEqualTo(1920f), "sharp on a 1080p screen");
            Assert.IsNull(MenuKit.Get("Not_A_Picture"), "a missing picture leaves a plain shape");
        }

        [Test]
        public void TheHomeScreenNamesTheBoardsDearestTowns()
        {
            BoardDefinition board = BoardDataLoader.LoadFromResources().Board;
            string line = MainMenuController.TownsLine(board, MainMenuController.TownsShown);
            string[] towns = line.Split(new[] { " · " }, System.StringSplitOptions.None);
            Assert.AreEqual(MainMenuController.TownsShown, towns.Length);
            int previous = int.MaxValue;
            foreach (string town in towns)
            {
                int price = -1;
                for (int i = 0; i < board.Count; i++)
                {
                    if (board[i].Type == SquareType.Property && board[i].Name == town) price = board[i].Price;
                }

                Assert.That(price, Is.GreaterThan(0), $"{town} is a town on the board");
                Assert.That(price, Is.LessThanOrEqualTo(previous), "dearest first");
                previous = price;
            }
        }

        private sealed class MemoryStorage : ISettingsStorage
        {
            private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
            public int Saves;
            public bool HasKey(string key) => _strings.ContainsKey(key);
            public int GetInt(string key) => 0;
            public float GetFloat(string key) => 0f;
            public void SetInt(string key, int value) { }
            public void SetFloat(string key, float value) { }
            public string GetString(string key) => _strings.TryGetValue(key, out string value) ? value : string.Empty;
            public void SetString(string key, string value) => _strings[key] = value ?? string.Empty;
            public void Save() => Saves++;
        }

        [Test]
        public void ThePickedCharacterIsKeptAndDecidesTheFigure()
        {
            var storage = new MemoryStorage();
            var choice = new CharacterChoice(storage);
            Assert.AreEqual("Ece", choice.Selected.Name, "the first character until one is picked");
            Assert.AreEqual(4, CharacterChoice.All.Count);

            choice.Select("Man02");
            Assert.AreEqual("Deniz", new CharacterChoice(storage).Selected.Name, "the pick is kept");
            Assert.AreEqual(CharacterKind.Man, choice.Selected.Figure);
            Assert.AreEqual(1, storage.Saves);
            Assert.Throws<System.ArgumentException>(() => choice.Select("Nobody"));

            storage.SetString(CharacterChoice.StorageKey, "an old id");
            Assert.AreEqual("Ece", choice.Selected.Name, "an unknown stored id falls back");
            foreach (CharacterOption option in CharacterChoice.All)
            {
                Assert.AreEqual(option.Id.StartsWith("Woman") ? CharacterKind.Woman : CharacterKind.Man, option.Figure, option.Name);
                Assert.IsNotNull(MenuKit.Get(option.Portrait), $"{option.Name}'s portrait");
            }
        }

        [Test]
        public void ThePickedFigureReplacesTheSeatsOwn()
        {
            var holder = new GameObject("Figure Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, BoardDataLoader.LoadFromResources());
                var crowd = new PawnCrowd(2);
                // Seat 0 plays the man unless the player picked a woman.
                Assert.AreEqual(CharacterKind.Man, PawnView.Create(holder.transform, board, 0, Color.red, crowd, 1).Kind);
                Assert.AreEqual(CharacterKind.Woman, PawnView.Create(holder.transform, board, 0, Color.red, crowd, 2, CharacterKind.Woman).Kind);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
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
        public void SceneChangesShowTheLoadingScreenLongEnoughAndFillTheBar()
        {
            Assert.That(SceneFader.MinShowSeconds, Is.GreaterThan(SceneFader.OutSeconds + 0.3f), "the logo is seen, not flashed");
            Assert.That(SceneFader.MinShowSeconds + SceneFader.InSeconds, Is.LessThan(1.6f), "and does not keep the player waiting");
            Assert.AreEqual(0f, SceneFader.Progress(1f, 0f), "the bar starts empty even when loading is instant");
            Assert.AreEqual(1f, SceneFader.Progress(0.9f, SceneFader.MinShowSeconds), 1e-5f, "a loaded scene fills it once the time is up");
            Assert.AreEqual(0.5f, SceneFader.Progress(0.45f, 10f), 1e-5f, "a slow load moves it as it goes");
            Assert.AreEqual(Lang.L("Menüye dönülüyor..."), SceneFader.CaptionFor("MainMenu"));
            Assert.AreEqual(Lang.L("Masa kuruluyor..."), SceneFader.CaptionFor("Game"));
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
            Assert.That(settings, Does.Contain("\n  productName: Lazpoly\n"), "the window title reads Lazpoly, capital L");
            Assert.That(settings, Does.Contain("\n  companyName: DefaultCompany\n"), "the saves and settings stay where they are until the release moves them");
            // Before our first frame the player clears the window to the loading screen's paper, not to dark grey or white.
            Assert.That(settings, Does.Contain("m_SplashScreenBackgroundColor: {r: 1, g: 0.9921569, b: 0.9607843, a: 1}"));
            Assert.That(Vector4.Distance(UiPalette.Paper.Panel, new Color(1f, 0.9921569f, 0.9607843f, 1f)), Is.LessThan(0.002f), "the start colour is the loading screen's paper");

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
            Assert.AreEqual("DÜNYA MASASI", Lang.Upper("Dünya Masası"));
            Assert.AreEqual("İSTANBUL VE SİNGAPUR", Lang.Upper("İstanbul ve Singapur"));
            Assert.AreEqual("", Lang.Upper(""));
            Assert.IsNull(Lang.Upper(null));
            Lang.Set(Language.English);
            Assert.AreEqual("ONLINE", Lang.Upper("Online"));
        }
    }
}
