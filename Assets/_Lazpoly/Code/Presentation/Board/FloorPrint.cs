using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Pictures printed flat on a tile (no relief, no shadow), like its name: the tax square's document (Faz 24), a sheet
    /// with a folded corner and lines of writing. Built in a unit space: the sheet is <see cref="DocumentSize"/> across
    /// (x) and up the page (z), its base at y = 0, a few thousandths thick.
    /// </summary>
    public static class FloorPrint
    {
        public static readonly Vector2 DocumentSize = new Vector2(0.72f, 0.92f);
        public static readonly Color Paper = new Color(0.97f, 0.96f, 0.92f);

        private const float Sheet = 0.003f;
        private const float Stroke = 0.06f;
        private const float Line = 0.05f;

        // Where the folded corner meets the top and the right edges.
        private const float FoldX = 0.12f;
        private const float FoldZ = 0.22f;

        /// <summary>
        /// The document: the paper and the ink as two children of one object, whose local +z is the page's up and whose
        /// size is <see cref="DocumentSize"/> times <paramref name="scale"/>.
        /// </summary>
        public static GameObject Document(string name, Transform parent, float scale, Color ink)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;
            Flat(ModelParts.Create("Paper", root.transform, DocumentPaper(), Paper, 0f));
            Flat(ModelParts.Create("Ink", root.transform, DocumentInk(), ink, 0f));
            return root;
        }

        /// <summary>The sheet's paper: the page less the corner folded down, with the flap (a square turned to a diamond whose edge is the fold).</summary>
        public static Mesh DocumentPaper()
        {
            float halfX = DocumentSize.x / 2f - Stroke / 2f;
            float halfZ = DocumentSize.y / 2f - Stroke / 2f;
            return MeshFactory.Combine("print:document-paper", new[]
            {
                Bar(new Vector2(0f, (FoldZ - halfZ) / 2f), new Vector2(2f * halfX, FoldZ + halfZ), 0f, 0f),
                Bar(new Vector2((FoldX - halfX) / 2f, (FoldZ + halfZ) / 2f), new Vector2(FoldX + halfX, halfZ - FoldZ), 0f, 0f),
                Bar(new Vector2(FoldX, FoldZ), Vector2.one * (Mathf.Sqrt(2f) * (halfX - FoldX)), 45f, 0f),
            });
        }

        /// <summary>The sheet's ink: its outline with the folded corner, and four lines of writing.</summary>
        public static Mesh DocumentInk()
        {
            float halfX = DocumentSize.x / 2f - Stroke / 2f;
            float halfZ = DocumentSize.y / 2f - Stroke / 2f;
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                Bar(new Vector2(-halfX, 0f), new Vector2(Stroke, 2f * halfZ + Stroke), 0f, Sheet),
                Bar(new Vector2(0f, -halfZ), new Vector2(2f * halfX + Stroke, Stroke), 0f, Sheet),
                Bar(new Vector2((FoldX - halfX) / 2f, halfZ), new Vector2(FoldX + halfX + Stroke, Stroke), 0f, Sheet),
                Bar(new Vector2(halfX, (FoldZ - halfZ) / 2f), new Vector2(Stroke, FoldZ + halfZ + Stroke), 0f, Sheet),
                // The fold: a diagonal across the corner and the flap's two edges.
                Bar(new Vector2((FoldX + halfX) / 2f, (FoldZ + halfZ) / 2f),
                    new Vector2(Mathf.Sqrt(2f) * (halfX - FoldX) + Stroke, Stroke), 45f, Sheet),
                Bar(new Vector2(FoldX, (FoldZ + halfZ) / 2f), new Vector2(Stroke * 0.7f, halfZ - FoldZ), 0f, Sheet),
                Bar(new Vector2((FoldX + halfX) / 2f, FoldZ), new Vector2(halfX - FoldX, Stroke * 0.7f), 0f, Sheet),
            };

            float[] rows = { 0.08f, -0.06f, -0.2f, -0.32f };
            for (int i = 0; i < rows.Length; i++)
            {
                float length = i == rows.Length - 1 ? 0.26f : 0.42f;
                parts.Add(Bar(new Vector2(-0.21f + length / 2f, rows[i]), new Vector2(length, Line), 0f, Sheet));
            }

            return MeshFactory.Combine("print:document-ink", parts);
        }

        /// <summary>A flat bar centred at <paramref name="at"/> (x across, z up the page), turned by <paramref name="angle"/> about y.</summary>
        private static KeyValuePair<Mesh, Matrix4x4> Bar(Vector2 at, Vector2 size, float angle, float y) =>
            new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(size.x, Sheet, size.y), 0.0005f, 1),
                Matrix4x4.TRS(new Vector3(at.x, y + Sheet / 2f, at.y), Quaternion.Euler(0f, angle, 0f), Vector3.one));

        private static void Flat(GameObject part)
        {
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
