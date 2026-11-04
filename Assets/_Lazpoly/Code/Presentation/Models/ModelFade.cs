using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// See-through copies of the model material (Resources/Lazpoly/Materials/ModelFade, the same Lit shader
    /// set to transparent, so its variant is part of player builds): the waiting players' pieces fade to these.
    /// </summary>
    public static class ModelFade
    {
        public const string MaterialResource = "Lazpoly/Materials/ModelFade";

        private static Material _base;
        private static bool _reportedMissing;

        /// <summary>A see-through copy of <paramref name="solid"/>: its colour and shine at <paramref name="alpha"/>; null when the fade material is missing.</summary>
        public static Material Copy(Material solid, float alpha)
        {
            Material fade = Base();
            if (fade == null || solid == null) return null;
            var copy = new Material(fade) { name = solid.name + " (faded)" };
            Color color = solid.color;
            color.a = Mathf.Clamp01(alpha);
            copy.color = color;
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
