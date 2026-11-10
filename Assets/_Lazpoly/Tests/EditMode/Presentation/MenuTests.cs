using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using UnityEngine.UI;

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
            float cards = MenuLayout.CharacterCardsPerRow * MenuLayout.CharacterCardWidth + (MenuLayout.CharacterCardsPerRow - 1) * MenuLayout.CharacterCardGap;
            Assert.That(cards + 80f, Is.LessThanOrEqualTo(MenuLayout.CharacterPanel.width), "four portraits fit a row of their window");
            Vector2 last = MenuLayout.CharacterCard(CharacterChoice.All.Count - 1);
            Assert.That(last.y + MenuLayout.CharacterCardHeight, Is.LessThan(MenuLayout.CharacterPanel.height - 60f), "both rows fit above the line naming the pick");
            Assert.IsTrue(design.Contains(MenuLayout.CharacterPanel.min) && design.Contains(MenuLayout.CharacterPanel.max));
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
        public void ThePickedCharacterIsKeptAndCarriesOverFromTheOldMenu()
        {
            var storage = new MemoryStorage();
            var choice = new CharacterChoice(storage);
            Assert.AreEqual("Ece", choice.Selected.Name, "the first character until one is picked");
            Assert.AreEqual(8, CharacterChoice.All.Count);

            choice.Select("boss");
            Assert.AreEqual("Baron", new CharacterChoice(storage).Selected.Name, "the pick is kept");
            Assert.AreEqual("boss", choice.Selected.Id);
            Assert.AreEqual(1, storage.Saves);
            Assert.Throws<System.ArgumentException>(() => choice.Select("Nobody"));

            storage.SetString(CharacterChoice.StorageKey, "an old id");
            Assert.AreEqual("Ece", choice.Selected.Name, "an unknown stored id falls back");
            storage.SetString(CharacterChoice.StorageKey, "Man02");
            Assert.AreEqual("navy", choice.Selected.Id, "Deniz picked in the old menu is the navy-suit figure");
            Assert.AreEqual("Deniz", choice.Selected.Name);
            Assert.AreEqual("teal", CharacterChoice.Find("Woman02").Id);
        }

        [Test]
        public void ThePickerMarksTheSeatsOwnTheOthersAndTheFreeOnes()
        {
            Assert.AreEqual(CharacterPicker.Mark.Picked, CharacterPicker.MarkOf("teal", "teal", null));
            Assert.AreEqual(CharacterPicker.Mark.Taken, CharacterPicker.MarkOf("boss", "teal", "Ali"));
            Assert.AreEqual(CharacterPicker.Mark.Free, CharacterPicker.MarkOf("mob", "teal", null));

            var root = new GameObject("Picker Test", typeof(RectTransform));
            try
            {
                var ui = new UiBuilder((RectTransform)root.transform);
                string picked = null;
                CharacterPicker.Render(ui, "teal", id => id == "boss" ? "Ali" : null, id => picked = id);
                Button[] tiles = root.GetComponentsInChildren<Button>(true);
                Assert.AreEqual(CharacterChoice.All.Count, tiles.Length, "one tile per character");
                Button boss = tiles.Single(tile => tile.name == "Character boss");
                Assert.IsFalse(boss.interactable, "another player's character cannot be picked");
                tiles.Single(tile => tile.name == "Character mob").onClick.Invoke();
                Assert.AreEqual("mob", picked);
                Assert.IsTrue(root.GetComponentsInChildren<Text>(true).Any(text => text.text == "Ali"), "the holder is named under their character");
                int[] lines = tiles.Select(tile => tile.GetComponentsInChildren<Text>(true).Length).Distinct().ToArray();
                Assert.AreEqual(1, lines.Length, "every tile has as many lines, so the row stays level");
                Assert.IsTrue(tiles.All(tile => tile.GetComponent<LayoutElement>().preferredWidth >= CharacterPicker.TileWidth), "room for the longest name");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ALongHoldersNameIsCutToFitUnderItsTile()
        {
            Assert.AreEqual("Oyuncu 2", CharacterPicker.Short("Oyuncu 2"));
            Assert.AreEqual("Bilgisayar", CharacterPicker.Short("Bilgisayar"));
            Assert.AreEqual("Ali", CharacterPicker.Short("Ali"));
            Assert.IsNull(CharacterPicker.Short(null));
            Assert.AreEqual("Mehmet Ca…", CharacterPicker.Short("Mehmet Can Yılmazoğlu"));
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
                // The Cash Father logo (the top-hatted character over the lettering), trimmed to its picture: about square.
                Assert.That(logo.height, Is.GreaterThanOrEqualTo(600), "sharp at 4K");
                Assert.That((float)logo.width / logo.height, Is.InRange(0.9f, 1.1f), "the owner's second logo, about square");
                Assert.That(logo.GetPixel(2, 2).a, Is.LessThan(0.02f), "the corner is see-through");
                Assert.That(logo.GetPixel(logo.width - 3, logo.height - 3).a, Is.LessThan(0.02f), "the other corner is see-through");
                Assert.That(logo.GetPixel(logo.width / 2, logo.height / 2).a, Is.GreaterThan(0.98f), "the middle is painted");
                int gold = 0, seen = 0;
                for (int y = logo.height * 28 / 100; y < logo.height * 38 / 100; y += 3)
                for (int x = logo.width / 4; x < logo.width * 3 / 4; x += 3)
                {
                    Color c = logo.GetPixel(x, y);
                    seen++;
                    if (c.r > 0.6f && c.r > c.g && c.g > c.b && c.r - c.b > 0.3f) gold++;
                }

                Assert.That((float)gold / seen, Is.GreaterThan(0.4f), "the word CASH across the lower middle is gold");

                Vector2 extent = BoardView.TitleExtent(logo);
                Assert.AreEqual(BoardView.TitleSize, extent.x, "the lawn's logo is TitleSize wide");
                Assert.AreEqual(BoardView.TitleSize * logo.height / logo.width, extent.y, 1e-5f, "and not stretched to a square");

                var kit = Resources.Load<TextAsset>("Lazpoly/Art/Menu/" + MenuKit.Logo);
                Assert.IsNotNull(kit, "the menu kit's wide logo is there too");
                CollectionAssert.AreEqual(asset.bytes, kit.bytes, "the menu, the loading screens and the lawn show the same logo");
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
            Assert.That(settings, Does.Contain("\n  productName: Cash Father\n"), "the window title reads the game's name");
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
            Assert.That(settings, Does.Contain($"guid: {smallGuid}, type: 3}}\n      m_Width: 16"), "the 16 pixel icon is the character's face, cropped tighter");
            Assert.That(settings, Does.Contain($"guid: {guid}, type: 3}}\n      m_Width: 256"), "the 256 pixel icon is the character in his medallion");

            Texture2D icon = MenuArt.Decode(File.ReadAllBytes(iconPath), "test icon");
            try
            {
                Assert.IsNotNull(icon);
                Assert.AreEqual(256, icon.width);
                Assert.AreEqual(256, icon.height);
                Assert.That(icon.GetPixel(1, 1).a, Is.LessThan(0.02f), "the icon's corner is see-through, outside the round medallion");
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
