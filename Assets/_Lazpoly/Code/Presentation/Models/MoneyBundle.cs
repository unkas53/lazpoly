using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// A thick bundle (tomar) of the owner's 100 L notes, the owner's wish (Faz 25): a block of paper as big as a note, its
    /// sheets showing at the edges in the note's ivory and pale blue, the owner's note on top and a kraft paper band round
    /// the middle. It lies flat with its lowest face at y = 0, its middle on the y axis, its length along x and the top
    /// note's lettering reading to a camera that looks along +Z. The meshes and materials are made once and shared, and
    /// none of it casts a shadow. Without the money set the block and the band still show, only the top note is missing.
    /// </summary>
    public static class MoneyBundle
    {
        /// <summary>The bundle's size in board units: as long and as wide as a note on the table, and this thick.</summary>
        public const float Length = 1.8f;
        public const float Width = Length * 1.36f / 2.8f;
        public const float Height = 0.3f;

        /// <summary>How many sheets show at the block's edges, and how far each one lies off the line at most.</summary>
        public const int Sheets = 12;
        public const float SheetJitter = 0.012f;

        /// <summary>The band's width along the bundle and how far it stands off the paper.</summary>
        public const float BandWidth = 0.24f;
        public const float BandLift = 0.008f;

        private static readonly Color Ivory = new Color(0.95f, 0.93f, 0.84f);
        private static readonly Color PaleBlue = new Color(0.78f, 0.88f, 0.87f);
        private static readonly Color Kraft = new Color(0.78f, 0.58f, 0.36f);

        private static Mesh _light;
        private static Mesh _dark;
        private static Mesh _band;
        private static Material _lightMaterial;
        private static Material _darkMaterial;
        private static Material _bandMaterial;

        /// <summary>The note's thickness on the table (the set's note is 0.0396 of its 2.8 units).</summary>
        public static float NoteThickness => 0.0396f * Length / 2.8f;

        /// <summary>A bundle as a parent object with its parts; its scale is one.</summary>
        public static GameObject Create(string name, Transform parent)
        {
            Build();
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Part("Sheets", root.transform, _light, _lightMaterial);
            Part("Sheets Between", root.transform, _dark, _darkMaterial);
            Part("Band", root.transform, _band, _bandMaterial);

            GameObject note = MoneyModels.Create(MoneyKind.Note, "Top Note", root.transform);
            if (note != null)
            {
                note.transform.localPosition = new Vector3(0f, Height - NoteThickness, 0f);
                note.transform.localScale = Vector3.one * (Length / MoneyModels.Length(MoneyKind.Note));
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        /// <summary>
        /// Where the <paramref name="sheet"/>th sheet of the block lies: from the bottom up, each a little off the line so
        /// the edges look like loose paper. Pure, so it can be checked without a scene.
        /// </summary>
        public static Vector3 SheetOffset(int sheet)
        {
            float step = (Height - NoteThickness) / Sheets;
            float x = ((sheet * 37) % 5 - 2) / 2f * SheetJitter;
            float z = ((sheet * 53) % 3 - 1) * SheetJitter;
            return new Vector3(x, (sheet + 0.5f) * step, z);
        }

        private static void Build()
        {
            if (_light != null && _dark != null && _band != null && _lightMaterial != null) return;

            float step = (Height - NoteThickness) / Sheets;
            Mesh sheet = MeshFactory.RoundedBox(new Vector3(Length - 2f * SheetJitter, step, Width - 2f * SheetJitter), step * 0.3f, 1);
            var light = new List<KeyValuePair<Mesh, Matrix4x4>>();
            var dark = new List<KeyValuePair<Mesh, Matrix4x4>>();
            for (int i = 0; i < Sheets; i++)
            {
                (i % 2 == 0 ? light : dark).Add(new KeyValuePair<Mesh, Matrix4x4>(sheet, Matrix4x4.Translate(SheetOffset(i))));
            }

            _light = MeshFactory.Combine("money-bundle-light", light);
            _dark = MeshFactory.Combine("money-bundle-dark", dark);

            // The band: a strip round the block, over the top note and under the bottom sheet.
            float bandHeight = Height + 2f * BandLift;
            Mesh strip = MeshFactory.RoundedBox(new Vector3(BandWidth, bandHeight, Width + 2f * BandLift), BandLift, 2);
            _band = MeshFactory.Combine("money-bundle-band", new[] { new KeyValuePair<Mesh, Matrix4x4>(strip, Matrix4x4.Translate(new Vector3(0f, Height / 2f, 0f))) });

            _lightMaterial = Shared("money-bundle-ivory", Ivory, 0.15f);
            _darkMaterial = Shared("money-bundle-blue", PaleBlue, 0.15f);
            _bandMaterial = Shared("money-bundle-band", Kraft, 0.2f);
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
            _light = null;
            _dark = null;
            _band = null;
            _lightMaterial = null;
            _darkMaterial = null;
            _bandMaterial = null;
        }
    }
}
