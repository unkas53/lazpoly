using Lazpoly.Presentation.UI;
using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The menu's pictures: the Cash Father logo the owner gave, shipped as PNG bytes in a TextAsset
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

        /// <summary>PNG (or JPEG) bytes as a texture, or null when they are not a picture (see <see cref="KitArt.Decode"/>).</summary>
        public static Texture2D Decode(byte[] bytes, string name, bool mipmaps = true) => KitArt.Decode(bytes, name, mipmaps);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _logo = null;
            _loaded = false;
        }
    }
}
