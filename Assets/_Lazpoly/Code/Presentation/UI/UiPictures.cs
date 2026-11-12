using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Pictures on cards: a texture shown with smoothly rounded corners and a thin edge line, drawn by the
    /// <see cref="ShaderResource"/> shader (a <see cref="RawImage"/> alone has square corners). A picture can be
    /// greyed (a choice that is not open yet) and can show only a part of its texture (a close-up). One material is
    /// kept per look. Without the shader in the build the picture shows whole, with square corners.
    /// </summary>
    public static class UiPictures
    {
        public const string ShaderResource = "Lazpoly/Shaders/RoundedPicture";

        /// <summary>The edge line over a picture: the ink, barely there, so the sky does not bleed into the paper.</summary>
        public const float EdgeAlpha = 0.22f;
        public const float EdgeWidth = 1.5f;

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static Shader _shader;
        private static bool _reportedMissing;

        /// <summary>Whether the rounded picture shader is in the build.</summary>
        public static bool IsAvailable => Find() != null;

        /// <summary>
        /// Shows <paramref name="picture"/> on <paramref name="box"/>, which is <paramref name="size"/> reference pixels,
        /// with corners of <paramref name="radius"/>. <paramref name="view"/> is the part of the texture shown (the whole
        /// when null); <paramref name="grey"/> takes its colour away (0 none, 1 all).
        /// </summary>
        public static RawImage Show(RectTransform box, Texture picture, Vector2 size, float radius, Rect? view = null, float grey = 0f)
        {
            var image = box.gameObject.AddComponent<RawImage>();
            image.texture = picture;
            image.raycastTarget = false;
            Rect part = view ?? new Rect(0f, 0f, 1f, 1f);
            Material material = MaterialFor(size, radius, part, grey);
            if (material != null) image.material = material;
            else image.uvRect = part; // Square corners, but the same part of the picture.
            return image;
        }

        /// <summary>What a look's material is kept under.</summary>
        public static string Key(Vector2 size, float radius, Rect view, float grey) => string.Format(CultureInfo.InvariantCulture,
            "{0:0.#}x{1:0.#}:{2:0.#}:{3:0.###},{4:0.###},{5:0.###},{6:0.###}:{7:0.##}", size.x, size.y, radius, view.x, view.y, view.width, view.height, grey);

        /// <summary>The material of a look, made once; null when the shader is not in the build.</summary>
        public static Material MaterialFor(Vector2 size, float radius, Rect view, float grey)
        {
            string key = Key(size, radius, view, grey);
            if (Materials.TryGetValue(key, out Material kept) && kept != null) return kept;
            Shader shader = Find();
            if (shader == null) return null;
            var material = new Material(shader) { name = $"Picture {key}", hideFlags = HideFlags.DontSave };
            material.SetVector("_Size", new Vector4(size.x, size.y, 0f, 0f));
            material.SetFloat("_Radius", Mathf.Min(radius, Mathf.Min(size.x, size.y) / 2f));
            material.SetVector("_View", new Vector4(view.x, view.y, view.width, view.height));
            material.SetFloat("_Grey", Mathf.Clamp01(grey));
            Color edge = UiTheme.Text;
            edge.a = EdgeAlpha;
            material.SetColor("_Edge", edge);
            material.SetFloat("_EdgeWidth", EdgeWidth);
            Materials[key] = material;
            return material;
        }

        private static Shader Find()
        {
            if (_shader != null) return _shader;
            _shader = Resources.Load<Shader>(ShaderResource);
            if (_shader == null && !_reportedMissing)
            {
                _reportedMissing = true;
                Debug.LogWarning($"[Lazpoly] Picture shader '{ShaderResource}' is missing; the cards' pictures keep square corners.");
            }

            return _shader;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Materials.Clear();
            _shader = null;
            _reportedMissing = false;
        }
    }
}
