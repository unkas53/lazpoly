using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// See-through copies of the model material (Resources/Lazpoly/Materials/ModelFade, the same Lit shader
    /// set to transparent, so its variant is part of player builds): the waiting players' pieces fade to these.
    /// Each faded part also carries the <see cref="DepthMask"/>, drawn first, so only its front surface shows.
    /// </summary>
    public static class ModelFade
    {
        public const string MaterialResource = "Lazpoly/Materials/ModelFade";
        public const string DepthShaderResource = "Lazpoly/Shaders/FadeDepth";
        public const string DepthShaderName = "Lazpoly/FadeDepth";

        private static Material _base;
        private static Material _mask;
        private static bool _reportedMissing;
        private static bool _reportedMaskMissing;

        /// <summary>
        /// One shared material that writes a piece's depth and no colour just before the transparent queue,
        /// so the see-through colour drawn after it hides the piece's own back parts; null when the shader is missing.
        /// </summary>
        public static Material DepthMask()
        {
            if (_mask != null) return _mask;
            Shader shader = Resources.Load<Shader>(DepthShaderResource);
            if (shader == null)
            {
                if (!_reportedMaskMissing)
                {
                    _reportedMaskMissing = true;
                    Debug.LogWarning($"[Lazpoly] Fade depth shader '{DepthShaderResource}' is missing; faded pieces show their back parts.");
                }

                return null;
            }

            _mask = new Material(shader) { name = "Fade depth" };
            return _mask;
        }

        /// <summary>A see-through copy of <paramref name="solid"/>: its colour and shine at <paramref name="alpha"/>; null when the fade material is missing.</summary>
        public static Material Copy(Material solid, float alpha)
        {
            Material fade = Base();
            if (fade == null || solid == null) return null;
            var copy = new Material(fade) { name = solid.name + " (faded)" };
            Color color = solid.color;
            color.a = Mathf.Clamp01(alpha);
            copy.color = color;
            if (solid.mainTexture != null) copy.mainTexture = solid.mainTexture; // A textured figure fades with its clothes on.
            if (solid.HasProperty("_Smoothness") && copy.HasProperty("_Smoothness")) copy.SetFloat("_Smoothness", solid.GetFloat("_Smoothness"));
            return copy;
        }

        private static Material Base()
        {
            if (_base != null) return _base;
            _base = Resources.Load<Material>(MaterialResource);
            if (_base == null && !_reportedMissing)
            {
                _reportedMissing = true;
                Debug.LogWarning($"[Lazpoly] Fade material '{MaterialResource}' is missing; waiting players' pieces stay solid.");
            }

            return _base;
        }
    }
}
