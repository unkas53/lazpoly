using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>The two pieces paid money travels as.</summary>
    public enum MoneyPiece
    {
        /// <summary>The owner's banded stack of green notes (Meshy AI).</summary>
        Stack = 0,

        /// <summary>The owner's gold bar (Meshy AI), for the big sums.</summary>
        Gold = 1,
    }

    /// <summary>
    /// One piece of paid money: the owner's banded money stack or gold bar (Meshy AI, Tools/Art/make_corner_models.py
    /// "moneystack" and "goldbar" at their original quality, <see cref="FigureModels.MoneyStackId"/>,
    /// <see cref="FigureModels.GoldBarId"/>). A piece lies flat with its lowest face at y = 0, its middle on the y axis,
    /// its length along x and its print reading to a camera that looks along +Z. Every piece of a kind shares one
    /// material, and none casts a shadow. Without its model a plain block in its colour stands in.
    /// </summary>
    public static class MoneyBundle
    {
        /// <summary>A piece's length on the table in board units; its width and thickness follow the model's proportions.</summary>
        public static float Length(MoneyPiece piece) => piece == MoneyPiece.Gold ? 1.2f : 1.4f;
        public static float Width(MoneyPiece piece) => Length(piece) * (piece == MoneyPiece.Gold ? 0.545f : 0.586f);
        public static float Height(MoneyPiece piece) => Length(piece) * (piece == MoneyPiece.Gold ? 0.315f : 0.473f);

        /// <summary>The model of each piece.</summary>
        public static string ModelId(MoneyPiece piece) => piece == MoneyPiece.Gold ? FigureModels.GoldBarId : FigureModels.MoneyStackId;

        private static readonly Color Green = new Color(0.45f, 0.72f, 0.40f);
        private static readonly Color GoldColor = new Color(0.95f, 0.72f, 0.20f);

        private static readonly Dictionary<MoneyPiece, Material> ModelMaterials = new Dictionary<MoneyPiece, Material>();
        private static readonly Dictionary<MoneyPiece, Material> StandInMaterials = new Dictionary<MoneyPiece, Material>();

        /// <summary>Whether the owner's model of the piece is in the build.</summary>
        public static bool HasModel(MoneyPiece piece) => FigureModels.IsAvailable(ModelId(piece));

        /// <summary>
        /// Reads both models (the owner's full-quality pieces, big) over the first frames of the table, ahead of the first
        /// payment, so neither the table's opening nor the payment waits for them all at once. Run it as a coroutine.
        /// </summary>
        public static IEnumerator Preload()
        {
            yield return FigureModels.Warm(FigureModels.MoneyStackId);
            yield return FigureModels.Warm(FigureModels.GoldBarId);
        }

        /// <summary>A piece as a parent object with its parts; its scale is one.</summary>
        public static GameObject Create(MoneyPiece piece, string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            GameObject model = FigureModels.Create(ModelId(piece), piece.ToString(), root.transform);
            if (model != null)
            {
                // The models are one unit long; their print reads from -Z, so they turn round to read to the camera.
                model.transform.localScale = Vector3.one * Length(piece);
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Share(piece, model.GetComponentInChildren<Renderer>());
            }
            else
            {
                StandIn(piece, root.transform);
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        /// <summary>Every piece of a kind draws with the first one's material; the copy each new model brings is thrown away.</summary>
        private static void Share(MoneyPiece piece, Renderer renderer)
        {
            if (renderer == null) return;
            Material own = renderer.sharedMaterial;
            if (!ModelMaterials.TryGetValue(piece, out Material shared) || shared == null)
            {
                ModelMaterials[piece] = own;
                return;
            }

            if (own == shared) return;
            renderer.sharedMaterial = shared;
            if (own != null) Destroy(own);
        }

        /// <summary>A plain block in the piece's colour, for when its model is missing.</summary>
        private static void StandIn(MoneyPiece piece, Transform parent)
        {
            if (!StandInMaterials.TryGetValue(piece, out Material material) || material == null)
            {
                var template = Resources.Load<Material>(ModelParts.MaterialResource);
                if (template != null)
                {
                    material = new Material(template) { name = $"money-{piece}", hideFlags = HideFlags.DontSave, color = piece == MoneyPiece.Gold ? GoldColor : Green };
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", piece == MoneyPiece.Gold ? 0.6f : 0.15f);
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", piece == MoneyPiece.Gold ? 0.8f : 0f);
                }

                StandInMaterials[piece] = material;
            }

            Vector3 size = new Vector3(Length(piece), Height(piece), Width(piece));
            Mesh block = MeshFactory.RoundedBox(size, 0.04f, 2);
            var part = new GameObject("Block");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = new Vector3(0f, size.y / 2f, 0f);
            part.AddComponent<MeshFilter>().sharedMesh = block;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Destroy(Object thing)
        {
            if (Application.isPlaying) Object.Destroy(thing);
            else Object.DestroyImmediate(thing);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ModelMaterials.Clear();
            StandInMaterials.Clear();
        }
    }
}
