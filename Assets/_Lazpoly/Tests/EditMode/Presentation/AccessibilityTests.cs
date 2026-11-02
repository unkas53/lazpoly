using System.Collections.Generic;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Tests
{
    public class AccessibilityTests
    {
        private const string TwoGroups = @"{
            ""startingMoney"": 500, ""passStartBonus"": 50, ""diceSides"": 6,
            ""maxConsecutiveDoubles"": 3, ""fullGroupRentMultiplier"": 2,
            ""groups"": [ { ""id"": 7, ""name"": ""Son"", ""color"": ""#FF0000"" }, { ""id"": 2, ""name"": ""İlk"", ""color"": ""#00FF00"" } ],
            ""squares"": [
                { ""type"": ""start"", ""name"": ""S"" },
                { ""type"": ""property"", ""name"": ""P"", ""price"": 60, ""rent"": 5, ""group"": 7 },
                { ""type"": ""property"", ""name"": ""Q"", ""price"": 60, ""rent"": 5, ""group"": 2 }
            ]
        }";

        [TearDown]
        public void NormalColours() => ColorVision.Apply(false);

        [Test]
        public void TheFriendlyPalettesAreOffByDefaultAndTellEveryGroupAndSeatApart()
        {
            Assert.IsFalse(ColorVision.Friendly);
            Assert.AreEqual(Color.red, ColorVision.Group(0, Color.red));
            Assert.AreEqual(Color.red, ColorVision.Player(3, Color.red));
            Assert.IsNull(ColorVision.GroupMark(0));

            ColorVision.Apply(true);
            var groups = new HashSet<Color>();
            var seats = new HashSet<Color>();
            for (int i = 0; i < ColorVision.PaletteSize; i++)
            {
                groups.Add(ColorVision.Group(i, Color.red));
                seats.Add(ColorVision.Player(i, Color.red));
            }

            Assert.AreEqual(ColorVision.PaletteSize, groups.Count, "every group has its own colour");
            Assert.AreEqual(ColorVision.PaletteSize, seats.Count, "every seat has its own colour");
            Assert.AreEqual("A", ColorVision.GroupMark(0));
            Assert.AreEqual("H", ColorVision.GroupMark(7));
            Assert.AreEqual(ColorVision.Group(0, Color.red), ColorVision.Group(ColorVision.PaletteSize, Color.red), "a ninth group starts the palette over");
            Assert.AreNotEqual(PlayerPalette.ForSeat(0), PlayerPalette.ForSeat(1));
        }

        [Test]
        public void ColoursMustNotBeTooCloseForSomeoneWhoDoesNotSeeRedAndGreenApart()
        {
            ColorVision.Apply(true);
            // Seen without the red-green axis, every pair of player colours still differs in brightness or in blue.
            for (int a = 0; a < ColorVision.PaletteSize; a++)
            {
                for (int b = a + 1; b < ColorVision.PaletteSize; b++)
                {
                    Color x = ColorVision.Player(a, Color.black), y = ColorVision.Player(b, Color.black);
                    float apart = Mathf.Abs(ColorVision.Luminance(x) - ColorVision.Luminance(y)) + Mathf.Abs(x.b - y.b) * 0.5f;
                    Assert.Greater(apart, 0.08f, $"seats {a} and {b} look alike without red and green");
                }
            }
        }

        [Test]
        public void GroupsTakeTheirLetterFromTheirOrderInTheBoardFile()
        {
            BoardData data = BoardDataLoader.Parse(TwoGroups);
            Assert.IsNull(data.GroupMark(7));
            Assert.AreEqual(Color.red, data.GroupColor(7), "the file's colour while the friendly palette is off");
            Assert.AreEqual(-1, data.GroupOrdinal(99));

            ColorVision.Apply(true);
            Assert.AreEqual("A", data.GroupMark(2), "the lower id comes first");
            Assert.AreEqual("B", data.GroupMark(7));
            Assert.IsNull(data.GroupMark(99), "an unknown group has no letter");
            Assert.AreEqual(ColorVision.Group(1, Color.red), data.GroupColor(7));
            Assert.AreNotEqual(data.GroupColor(2), data.GroupColor(7));
        }

        [Test]
        public void TabWalksTheScreenRowByRowFromTheTopLeftAndWrapsAround()
        {
            var rects = new List<Rect>
            {
                new Rect(300, 100, 100, 40), // bottom right
                new Rect(0, 500, 100, 40),   // top left
                new Rect(200, 505, 100, 40), // top, right of the first (a few pixels higher is still the same row)
                new Rect(0, 100, 100, 40),   // bottom left
            };

            List<int> order = FocusOrder.Sort(rects);

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 0 }, order);
            Assert.AreEqual(1, FocusOrder.Step(order, current: -1, backwards: false), "with nothing focused Tab takes the first");
            Assert.AreEqual(0, FocusOrder.Step(order, current: -1, backwards: true), "Shift+Tab takes the last");
            Assert.AreEqual(3, FocusOrder.Step(order, current: 2, backwards: false));
            Assert.AreEqual(1, FocusOrder.Step(order, current: 0, backwards: false), "past the end it wraps");
            Assert.AreEqual(0, FocusOrder.Step(order, current: 1, backwards: true));
            Assert.AreEqual(-1, FocusOrder.Step(new List<int>(), 0, false));
        }

        [Test]
        public void EnterPressesTheFocusedButtonInsteadOfRollingTheDice()
        {
            var onTurn = new HotkeyContext { Acting = true, Phase = GamePhase.WaitingForRoll, CanBuild = true, CanTrade = true };
            Assert.AreEqual(HotkeyAction.Roll, Hotkeys.Resolve(KeyCode.Return, onTurn));

            onTurn.ButtonFocused = true;
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.Return, onTurn));
            Assert.AreEqual(HotkeyAction.None, Hotkeys.Resolve(KeyCode.Space, onTurn));
            Assert.AreEqual(HotkeyAction.BuildCard, Hotkeys.Resolve(KeyCode.B, onTurn), "letters are not Submit keys, so they still work");
            Assert.AreEqual(HotkeyAction.PauseMenu, Hotkeys.Resolve(KeyCode.Escape, onTurn));
        }

        [Test]
        public void TheWindowOnTopIsTheOnlyScopeForTab()
        {
            var root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                UiRoot.Modal(root.transform, "Pause", 300f, out GameObject pause);
                UiRoot.Modal(root.transform, "Settings", 300f, out GameObject settings);
                Assert.IsNotNull(pause.GetComponent<UiFocusScope>(), "a window is a focus scope");

                Assert.AreEqual(settings.transform, UiFocus.TopScope(), "the later window sits on top");
                settings.SetActive(false);
                Assert.AreEqual(pause.transform, UiFocus.TopScope());
                pause.SetActive(false);
                Assert.IsNull(UiFocus.TopScope(), "with no window open the whole screen is the scope");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ASwatchCanCarryItsLetterAndAPrimaryButtonIsMarkedForTheFocus()
        {
            var root = new GameObject("UI", typeof(RectTransform));
            try
            {
                var ui = new UiBuilder((RectTransform)root.transform);
                Image plain = ui.Swatch(Color.red, 16f);
                Image lettered = ui.Swatch(Color.red, 16f, mark: "C");
                Assert.IsNull(plain.GetComponentInChildren<Text>());
                Assert.AreEqual("C", lettered.GetComponentInChildren<Text>().text);

                Button primary = ui.Button("Tamam", null, style: UiButtonStyle.Primary);
                Button secondary = ui.Button("Vazgeç", null);
                Assert.IsNotNull(primary.GetComponent<UiPrimaryMark>());
                Assert.IsNull(secondary.GetComponent<UiPrimaryMark>());
                Text label = secondary.GetComponentInChildren<Text>();
                Assert.IsTrue(label.resizeTextForBestFit, "a long English label shrinks instead of spilling out");
                Assert.AreEqual(UiTheme.MinButtonFontSize, label.resizeTextMinSize);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
