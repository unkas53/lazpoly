using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The main menu's rail (the owner's wish, 2026-11-21: a main menu that suits the game and moves): a tall band of
    /// night navy down the left of the screen whose right edge leans (<see cref="MenuLayout.RailEdge"/>), closed by a
    /// gold line with a hairline inside it and a soft shadow on the sky beside it. Behind the logo a lighter navy, a
    /// slow sunburst of gold rays and a gold glow, all kept inside the band; gold dust rises through it. The logo,
    /// the words and the buttons are laid over it by <see cref="MenuHome"/>.
    /// </summary>
    public static class MenuRail
    {
        /// <summary>The band is far larger than the design, so it reaches the screen's edges on any screen shape.</summary>
        public const float BandWidth = 3200f;
        public const float BandHeight = 2800f;

        /// <summary>The navy at the top and the bottom of the design, the lighter navy behind the logo, and the gold of the edge.</summary>
        public static readonly Color Top = Hex(0x132555);
        public static readonly Color Bottom = Hex(0x0A1433);
        public static readonly Color Halo = Hex(0x23408C);
        public static readonly Color Gold = Hex(0xF2C75C);
        public static readonly Color PaleGold = Hex(0xFFE7A0);

        /// <summary>How strong the rays are, how fast they turn (degrees a second), and how many motes of dust rise.</summary>
        public const float RaysStrength = 0.10f;
        public const float RaysTurn = 2.2f;
        public const int Motes = 26;

        /// <summary>The gold line is drawn this wide (its soft sides included), this much of it beyond the band's edge.</summary>
        public const float EdgeDrawn = 10f;
        public const float EdgeBeyond = 3f;

        /// <summary>How far the rail's shadow reaches over the sky.</summary>
        public const float ShadowReach = 54f;

        /// <summary>The share of the band's height the design takes, from its bottom: the navy runs from dark to light between these.</summary>
        public static float DesignFrom => (BandHeight - MenuLayout.Design.y) / 2f / BandHeight;

        public static float DesignTo => 1f - DesignFrom;

        /// <summary>
        /// A point of the design (x and y from its top left) as seen from the middle of the rail's edge, x to the
        /// right and y up: where a part kept inside the band is put.
        /// </summary>
        public static Vector2 FromEdge(Vector2 design) =>
            new Vector2(design.x - MenuLayout.RailEdge(MenuLayout.Design.y / 2f), MenuLayout.Design.y / 2f - design.y);

        /// <summary>Builds the rail on the design frame <paramref name="frame"/>, with its lights about <paramref name="light"/> (a point of the design).</summary>
        public static void Build(RectTransform frame, Vector2 light)
        {
            float lean = MenuLayout.RailLean;

            // The shadow the rail casts on the sky, from its edge outwards.
            RectTransform shadow = Leaning(frame, "Rail Shadow", new Vector2(0f, 0.5f), lean);
            shadow.sizeDelta = new Vector2(ShadowReach, BandHeight);
            Paint(shadow, UiFx.Fade, new Color(Bottom.r * 0.5f, Bottom.g * 0.5f, Bottom.b * 0.5f, 0.34f));

            // The band: everything in it is cut to its shape.
            RectTransform band = Leaning(frame, "Rail", new Vector2(1f, 0.5f), lean);
            band.sizeDelta = new Vector2(BandWidth, BandHeight);
            Paint(band, UiFx.Shaded("Lazpoly Rail", Bottom, Top, DesignFrom, DesignTo), Color.white);
            band.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            // Inside the band, a frame that stands upright again, so the lights are placed as the design has them.
            RectTransform upright = UiRoot.Rect(band, "Upright", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            upright.localRotation = Quaternion.Euler(0f, 0f, lean);
            Vector2 middle = FromEdge(light);
            Light(upright, "Halo", middle, 1150f, UiFx.Glow, Halo);
            RectTransform rays = Light(upright, "Rays", middle, 1700f, UiFx.Rays, new Color(Gold.r, Gold.g, Gold.b, RaysStrength));
            UiSpin.Attach(rays, RaysTurn);
            Light(upright, "Glow", middle, 660f, UiFx.Glow, new Color(Gold.r, Gold.g, Gold.b, 0.20f));

            // The gold line that closes the band, over its edge so the edge's steps never show; its lit side, and the hairline inside.
            RectTransform edge = Leaning(frame, "Rail Edge", new Vector2(1f, 0.5f), lean);
            edge.sizeDelta = new Vector2(0f, BandHeight);
            Line(edge, "Gold", EdgeBeyond - EdgeDrawn, EdgeBeyond, UiFx.Edge, Gold);
            // Thin lines are drawn with the band of light, soft right across: a few pixels wide, they would show steps otherwise.
            Line(edge, "Lit", EdgeBeyond - EdgeDrawn, EdgeBeyond - EdgeDrawn + 5f, UiFx.Band, new Color(PaleGold.r, PaleGold.g, PaleGold.b, 0.9f));
            Line(edge, "Hairline", -MenuLayout.RailHairline - 5f, -MenuLayout.RailHairline, UiFx.Band, new Color(Gold.r, Gold.g, Gold.b, 0.5f));

            // Dust rises through the part of the rail that is never cut by the edge.
            RectTransform field = MenuKit.Place(frame, "Rail Dust", 0f, 0f, MenuLayout.RailBottom - 40f, MenuLayout.Design.y);
            UiDust.Attach(field, Motes, new Color(PaleGold.r, PaleGold.g, PaleGold.b, 0.9f));
        }

        /// <summary>A rectangle hung by <paramref name="pivot"/> on the middle of the rail's edge and leaning with it.</summary>
        private static RectTransform Leaning(RectTransform frame, string name, Vector2 pivot, float lean)
        {
            RectTransform rect = UiRoot.Rect(frame, name, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            rect.pivot = pivot;
            rect.anchoredPosition = new Vector2(MenuLayout.RailEdge(MenuLayout.Design.y / 2f), -MenuLayout.Design.y / 2f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -lean);
            return rect;
        }

        private static Image Paint(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A round light of a size about a point (as <see cref="FromEdge"/> gives it).</summary>
        private static RectTransform Light(RectTransform upright, string name, Vector2 middle, float size, Sprite sprite, Color color)
        {
            RectTransform rect = UiRoot.Rect(upright, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.anchoredPosition = middle;
            rect.sizeDelta = new Vector2(size, size);
            Paint(rect, sprite, color);
            return rect;
        }

        /// <summary>A soft-sided line down the edge, between two distances from it (negative: inside the band).</summary>
        private static void Line(RectTransform edge, string name, float from, float to, Sprite sprite, Color color)
        {
            RectTransform rect = UiRoot.Rect(edge, name, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(from, 0f), new Vector2(to, 0f));
            Paint(rect, sprite, color);
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
