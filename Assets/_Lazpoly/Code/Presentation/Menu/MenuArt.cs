using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The menu's pictures: the Lazpoly logo the owner drew, shipped as PNG bytes in a TextAsset
    /// (Resources/Lazpoly/Art/logo.bytes, made by Tools/Art/make_logo.py from Tools/Art/Logo) and
    /// decoded once. When the asset is missing the menu shows its name in letters instead.
    /// </summary>
    public static class MenuArt
    {
        public const string LogoResource = "Lazpoly/Art/logo";

        private static Sprite _logo;
        private static bool _loaded;

        /// <summary>Whether the logo picture is in the build.</summary>
        public static bool HasLogo => Logo != null;

        /// <summary>The logo as a sprite, or null when the asset is missing or not a picture.</summary>
        public static Sprite Logo
        {
            get
            {
                if (_loaded) return _logo;
                _loaded = true;
                var asset = Resources.Load<TextAsset>(LogoResource);
                if (asset == null)
                {
                    Debug.LogWarning($"[Lazpoly] Logo '{LogoResource}' is missing; the menu shows the name in letters. Run Tools/Art/make_logo.py.");
                    return null;
                }

                Texture2D texture = Decode(asset.bytes, "Lazpoly Logo");
                if (texture == null)
                {
                    Debug.LogWarning($"[Lazpoly] Logo '{LogoResource}' could not be decoded; the menu shows the name in letters.");
                    return null;
                }

                _logo = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _logo.name = texture.name;
                _logo.hideFlags = HideFlags.DontSave;
                return _logo;
            }
        }

        /// <summary>
        /// PNG (or JPEG) bytes as a texture, or null when they are not a picture. A 9-sliced skin is decoded without
        /// mipmaps: its middle squeezes hard on a narrow button, and a blurred mip there would draw pale stripes
        /// across the button's top and bottom edges.
        /// </summary>
        public static Texture2D Decode(byte[] bytes, string name, bool mipmaps = true)
        {
            if (bytes == null || bytes.Length == 0) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipmaps) { name = name, hideFlags = HideFlags.DontSave };
            if (!texture.LoadImage(bytes, false))
            {
                if (Application.isPlaying) Object.Destroy(texture);
                else Object.DestroyImmediate(texture);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            return texture;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _logo = null;
            _loaded = false;
        }
    }
}
