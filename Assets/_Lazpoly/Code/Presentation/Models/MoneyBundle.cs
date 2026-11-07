using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// One bundle of paid money: the owner's cartoon dollar stack (Meshy AI, Tools/Art/make_corner_models.py
    /// "dollars", <see cref="FigureModels.DollarsId"/>), a thick banded stack of green notes. It lies flat with its lowest
    /// face at y = 0, its middle on the y axis, its length along x and its print reading to a camera that looks along +Z.
    /// Every bundle shares one material, and none of it casts a shadow. Without the model a plain green stack with a band
    /// stands in.
    /// </summary>
    public static class MoneyBundle
    {
        /// <summary>The bundle's size in board units: this long, and as wide and thick as the model's own proportions.</summary>
        public const float Length = 1.4f;
        public const float Width = Length * 0.629f;
        public const float Height = Length * 0.443f;

        /// <summary>The stand-in's band: its width along the bundle and how far it stands off the paper.</summary>
        public const float BandWidth = 0.22f;
        public const float BandLift = 0.008f;

        private static readonly Color Green = new Color(0.45f, 0.72f, 0.40f);
        private static readonly Color Band = new Color(0.96f, 0.74f, 0.20f);

        private static Material _modelMaterial;
        private static Mesh _block;
        private static Mesh _band;
        private static Material _blockMaterial;
        private static Material _bandMaterial;

        /// <summary>Whether the owner's dollar stack is in the build.</summary>
        public static bool HasModel => FigureModels.IsAvailable(FigureModels.DollarsId);

        /// <summary>Reads the model and makes its mesh ahead of the first payment, so it does not stall a frame.</summary>
        public static void Preload() => FigureModels.IsAvailable(FigureModels.DollarsId);

        /// <summary>A bundle as a parent object with its parts; its scale is one.</summary>
        public static GameObject Create(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            GameObject model = FigureModels.Create(FigureModels.DollarsId, "Dollars", root.transform);
            if (model != null)
            {
                // The model is one unit long; its print reads from -Z, so it turns round to read to the camera.
                model.transform.localScale = Vector3.one * Length;
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Share(model.GetComponentInChildren<Renderer>());
            }
            else
            {
                StandIn(root.transform);
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        /// <summary>Every bundle draws with the first one's material; the copy each new model brings is thrown away.</summary>
        private static void Share(Renderer renderer)
        {
            if (renderer == null) return;
            Material own = renderer.sharedMaterial;
            if (_modelMaterial == null)
            {
                _modelMaterial = own;
                return;
            }

            if (own == _modelMaterial) return;
            renderer.sharedMaterial = _modelMaterial;
            if (own != null)
            {
                if (Application.isPlaying) Object.Destroy(own);
                else Object.DestroyImmediate(own);
            }
        }

        /// <summary>A plain green stack with a band round its middle, for when the model is missing.</summary>
        private static void StandIn(Transform parent)
        {
            if (_block == null || _band == null || _blockMaterial == null || _bandMaterial == null)
            {
                Mesh block = MeshFactory.RoundedBox(new Vector3(Length, Height, Width), 0.04f, 2);
                _block = MeshFactory.Combine("money-bundle-block", new[] { new KeyValuePair<Mesh, Matrix4x4>(block, Matrix4x4.Translate(new Vector3(0f, Height / 2f, 0f))) });
                Mesh strip = MeshFactory.RoundedBox(new Vector3(BandWidth, Height + 2f * BandLift, Width + 2f * BandLift), BandLift, 2);
                _band = MeshFactory.Combine("money-bundle-band", new[] { new KeyValuePair<Mesh, Matrix4x4>(strip, Matrix4x4.Translate(new Vector3(0f, Height / 2f, 0f))) });
                _blockMaterial = Shared("money-bundle-green", Green, 0.15f);
                _bandMaterial = Shared("money-bundle-band", Band, 0.3f);
            }

            Part("Stack", parent, _block, _blockMaterial);
            Part("Band", parent, _band, _bandMaterial);
        }

        private static Material Shared(string name, Color color, float smoothness)
        {
            var template = Resources.Load<Material>(ModelParts.MaterialResource);
            if (template == null) return null;
            var material = new Material(template) { name = name, hideFlags = HideFlags.DontSave, color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static void Part(string name, Transform parent, Mesh mesh, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _modelMaterial = null;
            _block = null;
            _band = null;
            _blockMaterial = null;
            _bandMaterial = null;
        }
    }
}
