using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Pictures printed flat on a tile (no relief, no shadow), like its name (Faz 24): the tax square's document, a sheet
    /// with a folded corner and lines of writing, and the start square's arrow showing which way the pawns go. Built in a
    /// unit space: the sheet is <see cref="DocumentSize"/> across (x) and up the page (z), the arrow <see cref="ArrowSize"/>
    /// with its tip at +z, their base at y = 0, a few thousandths thick. The document's ink is a picture drawn with the
    /// board's print shader (see <see cref="DocumentSheet"/>), paper and ink alike, so its edges hold still under TAA.
    /// </summary>
    public static class FloorPrint
    {
        public static readonly Vector2 DocumentSize = new Vector2(0.72f, 0.92f);
        public static readonly Color Paper = new Color(0.97f, 0.96f, 0.92f);

        /// <summary>
        /// The paper's colour when printed as a picture (unlit, like the names): as light as the lit paper looked under the
        /// noon sun, which brightened <see cref="Paper"/> to almost white.
        /// </summary>
        public static readonly Color PrintedPaper = new Color(1f, 0.99f, 0.96f);
        public static readonly Vector2 ArrowSize = new Vector2(0.52f, 1f);
        public static readonly Color ArrowRed = new Color(0.86f, 0.22f, 0.18f);

        private const float Sheet = 0.003f;
        // Bold strokes (since the TAA flicker): the outline and the writing a quarter thicker than the first drawing.
        private const float Stroke = 0.075f;
        private const float Line = 0.065f;

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
            Material paper = PrintMaterial.ForPicture(DocumentPaperPicture());
            Material print = PrintMaterial.ForPicture(DocumentInkPicture(), 1);
            if (paper != null && print != null)
            {
                PrintedSheet("Paper", root.transform, paper, PrintedPaper, 0.5f * Sheet);
                PrintedSheet("Ink", root.transform, print, ink, Sheet);
            }
            else
            {
                Flat(ModelParts.Create("Paper", root.transform, DocumentPaper(), Paper, 0f));
                Flat(ModelParts.Create("Ink", root.transform, DocumentInkBars(), ink, 0f));
            }

            return root;
        }

        private static void PrintedSheet(string name, Transform parent, Material material, Color color, float height)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = DocumentSheet(color, height);
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = false;
            Flat(part);
        }

        /// <summary>The arrow as one object, its local +z the way it points and its size <see cref="ArrowSize"/> times <paramref name="scale"/>.</summary>
        public static GameObject Arrow(string name, Transform parent, float scale, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;
            Flat(ModelParts.Create("Ink", root.transform, ArrowMesh(), color, 0f));
            return root;
        }

        /// <summary>A bold arrow pointing +z: a shaft and a triangular head (a three-sided cylinder turned so a corner leads).</summary>
        public static Mesh ArrowMesh()
        {
            const float head = 0.3f; // The head's circumradius: its tip 0.3 ahead of its middle, its back 0.15 behind it.
            const float headMiddle = 0.5f - head;
            const float shaftWidth = 0.2f;
            float shaftEnd = headMiddle - head / 2f + 0.02f;
            return MeshFactory.Combine("print:arrow", new[]
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(3),
                    Matrix4x4.TRS(new Vector3(0f, 0f, headMiddle), Quaternion.Euler(0f, -90f, 0f), new Vector3(head, Sheet, head))),
                Bar(new Vector2(0f, (shaftEnd - 0.5f) / 2f), new Vector2(shaftWidth, shaftEnd + 0.5f), 0f, 0f),
            });
        }

        /// <summary>The sheet's paper: the page less the corner folded down, with the flap (a square turned to a diamond whose edge is the fold).</summary>
        public static Mesh DocumentPaper()
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (InkBar bar in PaperShapes()) parts.Add(Bar(bar.At, bar.Size, bar.Angle, 0f));
            return MeshFactory.Combine("print:document-paper", parts);
        }

        /// <summary>
        /// One flat sheet the size of the document at <paramref name="height"/>, drawn with the board's print shader (like
        /// the names): the paper and the ink are each a picture on such a sheet. As thin solid bars (the first version,
        /// <see cref="DocumentPaper"/> and <see cref="DocumentInkBars"/>) their edges shimmered under TAA, which nudges the
        /// picture by part of a pixel each frame; the print shader averages each pixel's footprint on the picture, so the
        /// edges hold still. The vertices carry <paramref name="color"/>, the picture's alpha gives the shape.
        /// </summary>
        public static Mesh DocumentSheet(Color color, float height)
        {
            float halfX = DocumentSize.x / 2f, halfZ = DocumentSize.y / 2f;
            var mesh = new Mesh { name = "print:document-sheet" };
            mesh.vertices = new[]
            {
                new Vector3(-halfX, height, -halfZ), new Vector3(-halfX, height, halfZ), new Vector3(halfX, height, halfZ), new Vector3(halfX, height, -halfZ),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.colors = new[] { color, color, color, color };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The pictures' width in texels; their height keeps the sheet's proportions. A line of writing is ~17 texels thick;
        /// small enough that the print shader's 4x4 samples per pixel cover the picture well when the table is seen whole.
        /// </summary>
        public const int PictureWidth = 192;

        private static Texture2D _inkPicture;
        private static Texture2D _paperPicture;

        /// <summary>The paper as a picture over the whole sheet: the page less the folded corner, its alpha the coverage.</summary>
        public static Texture2D DocumentPaperPicture() => _paperPicture != null ? _paperPicture : _paperPicture = Picture("print-document-paper", PaperShapes());

        /// <summary>The ink as a picture over the whole sheet: the outline, the fold and the writing, its alpha the coverage.</summary>
        public static Texture2D DocumentInkPicture() => _inkPicture != null ? _inkPicture : _inkPicture = Picture("print-document-ink", InkBars());

        /// <summary>
        /// A picture over the whole sheet (<see cref="DocumentSize"/>) of <paramref name="shapes"/>: white, its alpha how
        /// much of each texel they cover (4x4 samples per texel), so the edges come out soft at any size.
        /// </summary>
        private static Texture2D Picture(string name, List<InkBar> shapes) =>
            Picture(name, new Rect(-DocumentSize / 2f, DocumentSize), PictureWidth, (x, z) =>
            {
                foreach (InkBar bar in shapes)
                {
                    if (bar.Covers(x, z)) return true;
                }

                return false;
            });

        /// <summary>
        /// A picture of whatever <paramref name="covers"/> says lies on the page, over <paramref name="area"/> (x across,
        /// y up the page), <paramref name="width"/> texels across: white, its alpha how much of each texel is covered
        /// (<paramref name="samples"/> by <paramref name="samples"/> points per texel), so the edges come out soft at any size.
        /// Kept readable, so a test can check the drawing.
        /// </summary>
        public static Texture2D Picture(string name, Rect area, int width, System.Func<float, float, bool> covers, int samples = 4)
        {
            int height = Mathf.Max(1, Mathf.RoundToInt(width * area.height / area.width));
            var picture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[width * height];
            samples = Mathf.Max(1, samples);
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int covered = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float x = area.xMin + (column + (sx + 0.5f) / samples) / width * area.width;
                            float z = area.yMin + (row + (sy + 0.5f) / samples) / height * area.height;
                            if (covers(x, z)) covered++;
                        }
                    }

                    pixels[row * width + column] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * covered / (samples * samples)));
                }
            }

            picture.SetPixels32(pixels);
            picture.Apply(false, false);
            return picture;
        }

        /// <summary>
        /// One flat sheet over <paramref name="area"/> of the page (x across, y up the page) at <paramref name="height"/>,
        /// for a picture drawn with the print shader: the vertices carry <paramref name="color"/>, the picture's alpha the shape.
        /// </summary>
        public static Mesh PictureSheet(string name, Rect area, Color color, float height)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(area.xMin, height, area.yMin), new Vector3(area.xMin, height, area.yMax), new Vector3(area.xMax, height, area.yMax), new Vector3(area.xMax, height, area.yMin),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.colors = new[] { color, color, color, color };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A part drawn flat, casting no shadow: for the pictures printed on a tile.</summary>
        public static void NoShadow(GameObject part) => Flat(part);

        /// <summary>The ink as separate thin bars on the paper: the drawing when the print shader is missing.</summary>
        public static Mesh DocumentInkBars()
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (InkBar bar in InkBars()) parts.Add(Bar(bar.At, bar.Size, bar.Angle, Sheet));
            return MeshFactory.Combine("print:document-ink-bars", parts);
        }

        /// <summary>One stroke of the ink: a bar centred at <see cref="At"/> (x across, z up the page), turned by <see cref="Angle"/> about y.</summary>
        private readonly struct InkBar
        {
            public InkBar(Vector2 at, Vector2 size, float angle)
            {
                At = at;
                Size = size;
                Angle = angle;
            }

            public Vector2 At { get; }
            public Vector2 Size { get; }
            public float Angle { get; }

            /// <summary>Whether the point (x, z) of the page lies on the bar.</summary>
            public bool Covers(float x, float z)
            {
                // Undo the bar's turn: Quaternion.Euler(0, angle, 0) maps (x, z) to (x cos + z sin, -x sin + z cos).
                float dx = x - At.x, dz = z - At.y;
                float radians = Angle * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
                float across = dx * cos - dz * sin;
                float up = dx * sin + dz * cos;
                return Mathf.Abs(across) <= Size.x / 2f && Mathf.Abs(up) <= Size.y / 2f;
            }
        }

        /// <summary>The sheet's paper: the page less the corner folded down, with the flap (a square turned to a diamond whose edge is the fold).</summary>
        private static List<InkBar> PaperShapes()
        {
            float halfX = DocumentSize.x / 2f - Stroke / 2f;
            float halfZ = DocumentSize.y / 2f - Stroke / 2f;
            return new List<InkBar>
            {
                new InkBar(new Vector2(0f, (FoldZ - halfZ) / 2f), new Vector2(2f * halfX, FoldZ + halfZ), 0f),
                new InkBar(new Vector2((FoldX - halfX) / 2f, (FoldZ + halfZ) / 2f), new Vector2(FoldX + halfX, halfZ - FoldZ), 0f),
                new InkBar(new Vector2(FoldX, FoldZ), Vector2.one * (Mathf.Sqrt(2f) * (halfX - FoldX)), 45f),
            };
        }

        /// <summary>The sheet's ink: its outline with the folded corner, and four lines of writing.</summary>
        private static List<InkBar> InkBars()
        {
            float halfX = DocumentSize.x / 2f - Stroke / 2f;
            float halfZ = DocumentSize.y / 2f - Stroke / 2f;
            var bars = new List<InkBar>
            {
                new InkBar(new Vector2(-halfX, 0f), new Vector2(Stroke, 2f * halfZ + Stroke), 0f),
                new InkBar(new Vector2(0f, -halfZ), new Vector2(2f * halfX + Stroke, Stroke), 0f),
                new InkBar(new Vector2((FoldX - halfX) / 2f, halfZ), new Vector2(FoldX + halfX + Stroke, Stroke), 0f),
                new InkBar(new Vector2(halfX, (FoldZ - halfZ) / 2f), new Vector2(Stroke, FoldZ + halfZ + Stroke), 0f),
                // The fold: a diagonal across the corner and the flap's two edges.
                new InkBar(new Vector2((FoldX + halfX) / 2f, (FoldZ + halfZ) / 2f),
                    new Vector2(Mathf.Sqrt(2f) * (halfX - FoldX), Stroke), 45f), // No longer than the fold, so its ends stay inside the sheet.
                new InkBar(new Vector2(FoldX, (FoldZ + halfZ) / 2f), new Vector2(Stroke * 0.8f, halfZ - FoldZ), 0f),
                new InkBar(new Vector2((FoldX + halfX) / 2f, FoldZ), new Vector2(halfX - FoldX, Stroke * 0.8f), 0f),
            };

            float[] rows = { 0.08f, -0.06f, -0.2f, -0.32f };
            for (int i = 0; i < rows.Length; i++)
            {
                float length = i == rows.Length - 1 ? 0.26f : 0.42f;
                bars.Add(new InkBar(new Vector2(-0.21f + length / 2f, rows[i]), new Vector2(length, Line), 0f));
            }

            return bars;
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
