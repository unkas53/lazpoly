using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>A white rounded-rectangle sprite made at runtime and nine-sliced by images, so panels and buttons need no art files.</summary>
    public static class UiSprites
    {
        private const int Size = 32;
        private const int Radius = 10;

        private static Sprite _rounded;

        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;

                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    name = "Lazpoly Rounded",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };

                var pixels = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Coverage(x, y) * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _rounded = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(Radius + 2, Radius + 2, Radius + 2, Radius + 2));
                _rounded.name = texture.name;
                _rounded.hideFlags = HideFlags.DontSave;
                return _rounded;
            }
        }

        /// <summary>How much of pixel (x, y) lies inside the rounded rectangle, with one pixel of anti-aliasing.</summary>
        public static float Coverage(int x, int y)
        {
            float cx = Mathf.Clamp(x + 0.5f, Radius, Size - Radius);
            float cy = Mathf.Clamp(y + 0.5f, Radius, Size - Radius);
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            return Mathf.Clamp01(Radius - distance + 0.5f);
        }
    }
}
