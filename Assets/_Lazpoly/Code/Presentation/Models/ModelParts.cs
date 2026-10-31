using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// Creates scene objects from <see cref="MeshFactory"/> meshes. Every part gets its own copy of the
    /// project's model material from Resources, so the shader is always part of player builds.
    /// The material asset is made by the editor menu "Lazpoly/Set Up Render Pipeline".
    /// </summary>
    public static class ModelParts
    {
        public const string MaterialResource = "Lazpoly/Materials/Model";

        private static Material _baseMaterial;
        private static bool _reportedMissing;

        public static GameObject Create(string name, Transform parent, Mesh mesh, Color color, float smoothness = 0.25f)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            Material baseMaterial = BaseMaterial();
            if (baseMaterial == null) return part;
            var material = new Material(baseMaterial);
            Tint(material, color, smoothness);
            renderer.sharedMaterial = material;
            return part;
        }

        /// <summary>Recolours a part made by <see cref="Create"/>; it already owns its material.</summary>
        public static void Paint(GameObject part, Color color, float smoothness = 0.25f)
        {
            Material material = part.GetComponent<Renderer>().sharedMaterial;
            if (material != null) Tint(material, color, smoothness);
        }

        private static void Tint(Material material, Color color, float smoothness)
        {
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        }

        private static Material BaseMaterial()
        {
            if (_baseMaterial != null) return _baseMaterial;
            _baseMaterial = Resources.Load<Material>(MaterialResource);
            if (_baseMaterial == null && !_reportedMissing)
            {
                _reportedMissing = true;
                Debug.LogError($"[Lazpoly] Model material '{MaterialResource}' is missing; run Lazpoly/Set Up Render Pipeline.");
            }
            return _baseMaterial;
        }
    }
}
