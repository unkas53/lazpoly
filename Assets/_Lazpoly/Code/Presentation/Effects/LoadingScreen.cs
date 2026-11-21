using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// The loading screen (the owner's wish, 2026-11-21: loading screens that suit the game and move): a deep navy
    /// night, lighter behind the logo, a slow sunburst of gold rays turning behind it, gold dust rising, the logo
    /// hovering with a sheen crossing it now and then, a gold bar in a gold frame, the line saying where the player
    /// is going and, at the foot, a tip about the game. The opening screen (<see cref="BootSplash"/>) and every scene
    /// change (<see cref="SceneFader"/>) draw the same one, so leaving the menu for the table looks like the game
    /// starting. It never takes the pointer; the caller decides when it shows and how full the bar is.
    /// </summary>
    public sealed class LoadingScreen
    {
        public const float BarWidth = 440f;
        public const float BarHeight = 14f;

        /// <summary>How thick the gold frame round the bar is.</summary>
        public const float BarFrame = 3f;

        /// <summary>The night at the screen's edges and the lighter navy behind the logo. The player's window starts in <see cref="Ground"/> before the first frame.</summary>
        public static readonly Color Ground = new Color(0.03137255f, 0.0627451f, 0.16470589f, 1f);
        public static readonly Color Halo = new Color(0.114f, 0.216f, 0.478f, 1f);

        /// <summary>The bar's gold, the dark of its empty part, and the gold of the rays and the dust.</summary>
        public static readonly Color BarFill = new Color(0.976f, 0.745f, 0.251f);
        public static readonly Color BarTrack = new Color(0.027f, 0.051f, 0.137f);
        public static readonly Color Gold = new Color(0.949f, 0.780f, 0.361f);

        /// <summary>How strong the rays are, how fast they turn (degrees a second), and how many motes of dust rise.</summary>
        public const float RaysStrength = 0.10f;
        public const float RaysTurn = 2.2f;
        public const int Motes = 34;

        /// <summary>The logo's middle, as a share of the screen's height from its bottom: a little above the middle, the bar and the line under it.</summary>
        public const float LogoHeight = 0.56f;

        private static int _tip;

        private readonly RectTransform _fill;
        private readonly Text _caption;
        private readonly Text _tipLine;

        private LoadingScreen(RectTransform fill, Text caption, Text tipLine)
        {
            _fill = fill;
            _caption = caption;
            _tipLine = tipLine;
        }

        /// <summary>The interface kit's wide logo.</summary>
        public const string WideLogo = "Lazpoly_Logo";

        /// <summary>Short true things about the game, one shown under each loading screen in turn.</summary>
        public static readonly string[] Tips =
        {
            "Bir rengin bütün şehirlerini topla: kiraları iki katına çıkar.",
            "Üç evli şehrine bir daha gelince otel kurabilirsin.",
            "Koruma kartı, şehrini bir saldırıdan kurtarır.",
            "Kupayı getirdiğin şehrin kirası her seçimde daha da artar.",
            "Paran kiraya yetmezse şehir satarsın; o da yetmezse iflas edersin.",
            "Otelli şehirler devralınamaz, füzeyle de vurulamaz.",
            "Bir kareye tıkla: fiyatı, kirası ve sahibi kartında görünür.",
        };

        /// <summary>The tip of a given turn, written out: "İpucu: ..." in the game's language; the turns go round the list.</summary>
        public static string TipLine(int turn)
        {
            int index = ((turn % Tips.Length) + Tips.Length) % Tips.Length;
            return F($"İpucu: {L(Tips[index])}");
        }

        /// <summary>Builds the screen under <paramref name="root"/>, stretched over it; without the logo picture the night, the bar and the lines still show.</summary>
        public static LoadingScreen Build(RectTransform root, float logoSize, string caption)
        {
            Image ground = UiRoot.Background(UiRoot.Stretch(root, "Night"), Ground, rounded: false);
            ground.raycastTarget = false;

            // Everything hangs on the point the logo's middle stands on.
            var middle = new Vector2(0.5f, LogoHeight);
            Lamp(root, "Halo", middle, new Vector2(2600f, 1900f), UiFx.Glow, Halo);
            RectTransform rays = Lamp(root, "Rays", middle, new Vector2(2500f, 2500f), UiFx.Rays, new Color(Gold.r, Gold.g, Gold.b, RaysStrength));
            UiSpin.Attach(rays, RaysTurn);
            Lamp(root, "Glow", middle, new Vector2(logoSize * 1.9f, logoSize * 1.9f), UiFx.Glow, new Color(Gold.r, Gold.g, Gold.b, 0.22f));
            UiDust.Attach(UiRoot.Stretch(root, "Dust"), Motes, Gold);

            // The kit's wide logo, as on the main menu; the older square one when the kit's is not in the build.
            Sprite mark = KitArt.Get(WideLogo);
            if (mark == null) mark = MenuArt.Logo;
            float top = logoSize / 2f;
            if (mark != null)
            {
                RectTransform place = UiRoot.Rect(root, "Logo", middle, middle, Vector2.zero, Vector2.zero);
                place.sizeDelta = new Vector2(logoSize, logoSize);
                RectTransform picture = UiRoot.Stretch(place, "Picture");
                var logo = picture.gameObject.AddComponent<Image>();
                logo.sprite = mark;
                logo.preserveAspect = true;
                logo.raycastTarget = false;
                UiShine.Attach(picture, mark, every: 3.4f, seconds: 0.9f, strength: 0.4f, offset: 0.5f, like: logo);
                UiFloat.Attach(picture, 6f, 5f);
            }

            // Under the logo: the bar in its gold frame, then the line.
            float barTop = -top - 34f;
            RectTransform frame = UiRoot.Rect(root, "Bar", middle, middle, Vector2.zero, Vector2.zero);
            frame.pivot = new Vector2(0.5f, 1f);
            frame.anchoredPosition = new Vector2(0f, barTop);
            frame.sizeDelta = new Vector2(BarWidth + 2f * BarFrame, BarHeight + 2f * BarFrame);
            Pill(frame, Gold);
            RectTransform track = UiRoot.Rect(frame, "Track", Vector2.zero, Vector2.one, new Vector2(BarFrame, BarFrame), new Vector2(-BarFrame, -BarFrame));
            Pill(track, BarTrack);
            RectTransform fill = UiRoot.Rect(track, "Fill", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.sizeDelta = Vector2.zero;
            Pill(fill, BarFill);
            // The fill's lit upper half.
            RectTransform sheen = UiRoot.Rect(fill, "Sheen", new Vector2(0f, 0.5f), Vector2.one, new Vector2(3f, 0f), new Vector2(-3f, -2f));
            Pill(sheen, new Color(1f, 1f, 1f, 0.3f));

            Text line = Line(root, "Caption", middle, barTop - BarHeight - 2f * BarFrame - 14f, 34f, 20, UiPalette.Ink.OnMasthead, bold: true);
            line.text = caption;

            // At the foot of the screen: a tip, a new one each time a loading screen is built.
            RectTransform foot = UiRoot.Rect(root, "Tip", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            foot.pivot = new Vector2(0.5f, 0f);
            foot.anchoredPosition = new Vector2(0f, 56f);
            foot.sizeDelta = new Vector2(1300f, 60f);
            Text tip = Words(foot, 20, UiPalette.Ink.OnMastheadMuted, bold: false);
            tip.text = TipLine(_tip);
            return new LoadingScreen(fill, line, tip);
        }

        /// <summary>Fills the bar to <paramref name="progress"/> (0..1).</summary>
        public void SetProgress(float progress) => _fill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(progress), 0f);

        public void SetCaption(string caption) => _caption.text = caption;

        /// <summary>The line at the foot of the screen.</summary>
        public string Tip => _tipLine.text;

        /// <summary>Moves on to the next tip (each scene change shows another).</summary>
        public void NextTip()
        {
            _tip++;
            _tipLine.text = TipLine(_tip);
        }

        /// <summary>A soft light of a colour about a point of the screen.</summary>
        private static RectTransform Lamp(RectTransform root, string name, Vector2 anchor, Vector2 size, Sprite sprite, Color color)
        {
            RectTransform rect = UiRoot.Rect(root, name, anchor, anchor, Vector2.zero, Vector2.zero);
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void Pill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Pill;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>A centred line hung <paramref name="y"/> under the logo's middle.</summary>
        private static Text Line(RectTransform root, string name, Vector2 anchor, float y, float height, int size, Color color, bool bold)
        {
            RectTransform rect = UiRoot.Rect(root, name, anchor, anchor, Vector2.zero, Vector2.zero);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(900f, height);
            return Words(rect, size, color, bold);
        }

        private static Text Words(RectTransform rect, int size, Color color, bool bold)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = bold ? UiTheme.BoldFont : UiTheme.Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _tip = 0;
    }
}
