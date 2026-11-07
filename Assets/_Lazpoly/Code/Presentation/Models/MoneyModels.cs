using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Lazpoly.Presentation.Models
{
    /// <summary>The two pieces of the owner's money set.</summary>
    public enum MoneyKind
    {
        /// <summary>The 1 L coin: gold, navy enamel, the logo on its face, about 1.3 units across.</summary>
        Coin = 0,

        /// <summary>The 100 L banknote: ivory and blue, the logo on both faces, 2.8 x 1.36 units.</summary>
        Note = 1,
    }

    /// <summary>
    /// The owner's money set (Tools/Art/Models/Money through Tools/Art/make_money.py): each piece is a few parts in flat
    /// colours plus thin planes carrying the logo, whose clear pixels are cut away (Resources/Lazpoly/Materials/ModelCutout,
    /// the model shader with alpha clipping, so its variant is part of player builds). The pieces lie flat with their lowest
    /// face at y = 0, their middle on the y axis, and their lettering reading to a camera that looks along +Z. Meshes and
    /// materials are made once and shared by every piece. Null when the files are missing: the callers draw a plain coin.
    /// </summary>
    public static class MoneyModels
    {
        public const string Resource = "Lazpoly/Models/money";
        public const string LogoResource = "Lazpoly/Models/money_logo";
        public const string CutoutMaterialResource = "Lazpoly/Materials/ModelCutout";

        /// <summary>The model name of each kind in the pack.</summary>
        public static string ModelName(MoneyKind kind) => kind == MoneyKind.Coin ? "Coin" : "Note";

        /// <summary>A piece's long side in model units: the coin's width across, the note's length.</summary>
        public static float Length(MoneyKind kind) => kind == MoneyKind.Coin ? 1.304f : 2.8f;

        private sealed class Piece
        {
            public readonly List<(Mesh Mesh, Material Material, bool Logo)> Parts = new List<(Mesh, Material, bool)>();
        }

        private static Dictionary<MoneyKind, Piece> _pieces;
        private static bool _failed;

        /// <summary>Whether the money set is in the build and readable.</summary>
        public static bool IsAvailable => Load() != null;

        /// <summary>A piece as a parent object with its parts, or null when the set is missing.</summary>
        public static GameObject Create(MoneyKind kind, string name, Transform parent)
        {
            Dictionary<MoneyKind, Piece> pieces = Load();
            if (pieces == null || !pieces.TryGetValue(kind, out Piece piece)) return null;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            foreach ((Mesh mesh, Material material, bool logo) in piece.Parts)
            {
                var part = new GameObject(mesh.name);
                part.transform.SetParent(root.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                // The logo is a print on the face: it casts no shadow of its own.
                if (logo) renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            return root;
        }

        private static Dictionary<MoneyKind, Piece> Load()
        {
            if (_pieces != null) return _pieces;
            if (_failed) return null;

            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null) return Fail("is missing");
            IReadOnlyList<MoneyPart> parts;
            try
            {
                parts = MoneyPack.Parse(asset.bytes);
            }
            catch (FormatException exception)
            {
                return Fail($"could not be read: {exception.Message}");
            }

            var solid = Resources.Load<Material>(ModelParts.MaterialResource);
            if (solid == null) return Fail($"has no material ('{ModelParts.MaterialResource}' is missing)");
            Material logo = LogoMaterial();

            var pieces = new Dictionary<MoneyKind, Piece>();
            foreach (MoneyKind kind in new[] { MoneyKind.Coin, MoneyKind.Note })
            {
                var piece = new Piece();
                foreach (MoneyPart part in parts)
                {
                    if (part.Model != ModelName(kind)) continue;
                    if (part.Logo && logo == null) continue; // Without the logo the face shows its plain colour.
                    Material material = part.Logo ? logo : Solid(solid, part);
                    piece.Parts.Add((part.ToMesh(), material, part.Logo));
                }

                if (piece.Parts.Count == 0) return Fail($"has no {ModelName(kind)}");
                pieces[kind] = piece;
            }

            _pieces = pieces;
            return _pieces;
        }

        private static Material Solid(Material template, MoneyPart part)
        {
            var material = new Material(template) { name = $"money-{part.Name}", hideFlags = HideFlags.DontSave, color = part.Color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", part.Metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", part.Smoothness);
            return material;
        }

        /// <summary>The cut-out material with the logo on it; null (no logo planes) when either file is missing.</summary>
        private static Material LogoMaterial()
        {
            var template = Resources.Load<Material>(CutoutMaterialResource);
            var asset = Resources.Load<TextAsset>(LogoResource);
            if (template == null || asset == null)
            {
                Debug.LogWarning($"[Lazpoly] The money's logo ('{LogoResource}' or '{CutoutMaterialResource}') is missing; the money shows without it. Run Tools/Art/make_money.py.");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "money-logo", hideFlags = HideFlags.DontSave };
            if (!texture.LoadImage(asset.bytes, false))
            {
                Destroy(texture);
                Debug.LogWarning($"[Lazpoly] The money's logo '{LogoResource}' could not be read; the money shows without it.");
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            if (texture.width % 4 == 0 && texture.height % 4 == 0) texture.Compress(true);
            texture.Apply(false, true);
            var material = new Material(template) { name = "money-logo", hideFlags = HideFlags.DontSave, color = Color.white, mainTexture = texture };
            return material;
        }

        private static Dictionary<MoneyKind, Piece> Fail(string problem)
        {
            _failed = true;
            Debug.LogWarning($"[Lazpoly] Money set '{Resource}' {problem}; plain coins stand in. Run Tools/Art/make_money.py.");
            return null;
        }

        private static void Destroy(Object thing)
        {
            if (Application.isPlaying) Object.Destroy(thing);
            else Object.DestroyImmediate(thing);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pieces = null;
            _failed = false;
        }
    }
}
