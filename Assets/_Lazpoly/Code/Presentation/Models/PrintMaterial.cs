using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The material for text printed on the board (square names, prices, group letters, the lawn's
    /// score and title): the font's own atlas drawn with the <see cref="ShaderResource"/> shader, which
    /// tests depth, so pieces standing on the print hide it. One material per font; a dynamic font
    /// rebuilds its atlas as new glyphs are needed, and the material follows the new texture.
    /// Text meant to show through everything (the rising "+200", a bubble's line) keeps the font's own material.
    /// </summary>
    public static class PrintMaterial
    {
        public const string ShaderResource = "Lazpoly/Shaders/Print";
        public const string ShaderName = "Lazpoly/Print";

        private static readonly Dictionary<Font, Material> Materials = new Dictionary<Font, Material>();
        private static Shader _shader;
        private static bool _listening;
        private static bool _reportedMissing;

        /// <summary>The print material for <paramref name="font"/>; the font's own material when the shader is missing.</summary>
        public static Material For(Font font)
        {
            if (font == null) return null;
            if (Materials.TryGetValue(font, out Material material) && material != null) return material;
            Shader shader = PrintShader();
            if (shader == null) return font.material;
            material = new Material(shader) { name = $"Print {font.name}", mainTexture = font.material.mainTexture };
            Materials[font] = material;
            if (!_listening)
            {
                _listening = true;
                Font.textureRebuilt += OnTextureRebuilt;
            }

            return material;
        }

        private static readonly Dictionary<Texture, Material> Pictures = new Dictionary<Texture, Material>();

        /// <summary>
        /// The print material for a picture whose alpha is the shape (the tax document's ink), coloured by the mesh's
        /// vertices; null when the shader is missing, so the caller can draw the shape another way. A higher
        /// <paramref name="layer"/> draws over a lower one.
        /// </summary>
        public static Material ForPicture(Texture picture, int layer = 0)
        {
            if (picture == null) return null;
            if (Pictures.TryGetValue(picture, out Material material) && material != null) return material;
            Shader shader = PrintShader();
            if (shader == null) return null;
            material = new Material(shader) { name = $"Print {picture.name}", mainTexture = picture };
            // Pictures lying on each other (the document's ink on its paper) draw in this order, whatever their distance.
            material.renderQueue = shader.renderQueue + layer;
            Pictures[picture] = material;
            return material;
        }

        /// <summary>Whether printed text tests depth here, or falls back to the font's own draw-through material.</summary>
        public static bool IsPrinting(Font font) => For(font) != null && For(font).shader == PrintShader();

        private static Shader PrintShader()
        {
            if (_shader != null) return _shader;
            _shader = Resources.Load<Shader>(ShaderResource);
            if (_shader == null && !_reportedMissing)
            {
                _reportedMissing = true;
                Debug.LogWarning($"[Lazpoly] Print shader '{ShaderResource}' is missing; board text draws through pieces.");
            }

            return _shader;
        }

        private static void OnTextureRebuilt(Font font)
        {
            if (font != null && Materials.TryGetValue(font, out Material material) && material != null) material.mainTexture = font.material.mainTexture;
        }
    }
}
