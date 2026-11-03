using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>White sprites made at runtime (a nine-sliced rounded rectangle and a circle), so panels, buttons and icons need no art files.</summary>
    public static class UiSprites
    {
        private const int Size = 32;
        private const int Radius = 10;

        private static Sprite _rounded;
        private static Sprite _circle;
        private static Sprite _pill;

        /// <summary>A filled circle, drawn simple (not sliced) so it stays round at any size.</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                _circle = Make("Lazpoly Circle", CircleCoverage, Vector4.zero);
                return _circle;
            }
        }

        /// <summary>How much of pixel (x, y) lies inside the circle filling the sprite, with one pixel of anti-aliasing.</summary>
        public static float CircleCoverage(int x, int y)
        {
            const float radius = Size / 2f;
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
            return Mathf.Clamp01(radius - distance + 0.5f);
        }

        private static Sprite Make(string name, System.Func<int, int, float> coverage, Vector4 border)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage(x, y) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                _rounded = Make("Lazpoly Rounded", Coverage, new Vector4(Radius + 2, Radius + 2, Radius + 2, Radius + 2));
                return _rounded;
            }
        }

        /// <summary>How much of pixel (x, y) lies inside the rounded rectangle, with one pixel of anti-aliasing.</summary>
        public static float Coverage(int x, int y) => Coverage(x, y, Radius);

        private static float Coverage(int x, int y, float radius)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, Size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, Size - radius);
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            return Mathf.Clamp01(radius - distance + 0.5f);
        }

        /// <summary>A rounded rectangle whose short sides are half circles: sliced, it stretches into a pill of any length.</summary>
        public static Sprite Pill
        {
            get
            {
                if (_pill != null) return _pill;
                _pill = Make("Lazpoly Pill", PillCoverage, new Vector4(Size / 2f, Size / 2f, Size / 2f, Size / 2f));
                return _pill;
            }
        }

        /// <summary>How much of pixel (x, y) lies inside the pill shape, with one pixel of anti-aliasing.</summary>
        public static float PillCoverage(int x, int y) => Coverage(x, y, Size / 2f);
    }
}
