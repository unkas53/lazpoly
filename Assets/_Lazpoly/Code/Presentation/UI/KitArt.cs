using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// The owner's interface kit (the main menu kit, made with Higgsfield): its button, panel and chip skins and
    /// its line icons, shipped as PNG bytes under Resources/Lazpoly/Art/Menu (Tools/Art/make_menu_art.py) and
    /// decoded once into sprites. Every window, card and button of the game wears these skins, so the table looks
    /// like the menu. A missing picture leaves the plain rounded shape it replaced, never an error.
    /// </summary>
    public static class KitArt
    {
        public const string Folder = "Lazpoly/Art/Menu/";

        /// <summary>A window: cream paper with a folded top right corner.</summary>
        public const string Panel = "Panel";

        /// <summary>A card inside a window, a field, a profile: cream, rounded, a thin grey edge.</summary>
        public const string Chip = "Chip";

        /// <summary>A dark petrol disc with a brass rim.</summary>
        public const string Circle = "Circle";

        /// <summary>The height, in design pixels, the kit draws its buttons at: thinner buttons scale their corners down to match.</summary>
        public const float ButtonDesignHeight = 84f;

        /// <summary>The height the kit draws its chip at (the profile card): cards, fields and boxes scale its corners from it.</summary>
        public const float ChipDesignHeight = 76f;

        /// <summary>The kit's two button looks: the dark petrol one for the main action and the cream one for the rest.</summary>
        public enum Skin { Primary, Secondary }

        /// <summary>A button's four pictures, by state.</summary>
        public static string Picture(Skin skin, string state) => (skin == Skin.Primary ? "Primary_" : "Secondary_") + state;

        /// <summary>The 9-slice borders of the stretchable skins, in picture pixels (the kit's SpriteManifest.json).</summary>
        public static Vector4 Borders(string name)
        {
            if (name.StartsWith("Primary_", StringComparison.Ordinal) || name.StartsWith("Secondary_", StringComparison.Ordinal)) return new Vector4(32f, 32f, 32f, 32f);
            if (name == Panel) return new Vector4(36f, 36f, 36f, 36f);
            if (name == Chip) return new Vector4(30f, 30f, 30f, 30f);
            return Vector4.zero;
        }

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>A kit picture as a sprite (sliced when it has borders), or null when it is not in the build.</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Sprites.TryGetValue(name, out Sprite cached) && cached != null) return cached;
            if (Missing.Contains(name)) return null;

            var asset = Resources.Load<TextAsset>(Folder + name);
            Texture2D texture = asset != null ? Decode(asset.bytes, name, mipmaps: Borders(name) == Vector4.zero) : null;
            if (texture == null)
            {
                Missing.Add(name);
                Debug.LogWarning($"[Lazpoly] Menu picture '{Folder}{name}' is missing; run Tools/Art/make_menu_art.py.");
                return null;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, Borders(name));
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            Sprites[name] = sprite;
            return sprite;
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
                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            return texture;
        }

        /// <summary>
        /// Dresses <paramref name="image"/> in a sliced kit skin, its corners scaled so that a part
        /// <paramref name="height"/> pixels tall keeps the kit's proportions (a 44 pixel button gets corners about
        /// half the size of the 84 pixel menu buttons'). False, and nothing changed, when the picture is missing.
        /// </summary>
        public static bool Dress(Image image, string name, float height, float designHeight = ButtonDesignHeight)
        {
            Sprite sprite = Get(name);
            if (sprite == null) return false;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = CornerScale(height, designHeight);
            return true;
        }

        /// <summary>How much smaller than in the kit a skin's corners are drawn on a part this tall: never larger than in the kit, and as drawn for a height of 0.</summary>
        public static float CornerScale(float height, float designHeight = ButtonDesignHeight) =>
            height > 0f && height < designHeight ? designHeight / height : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sprites.Clear();
            Missing.Clear();
        }
    }
}
