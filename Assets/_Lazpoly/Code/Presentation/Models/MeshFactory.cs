using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// Builds the simple meshes the board uses until the Blender models arrive: rounded boxes,
    /// turned (lathe) shapes for pawns, and little houses. All meshes are centred on the origin
    /// unless noted, and are cached because many squares share them.
    /// </summary>
    public static class MeshFactory
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>A box of the given size whose edges and corners are rounded with <paramref name="radius"/>.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int subdivisions = 6)
        {
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f) throw new ArgumentOutOfRangeException(nameof(size));
            if (subdivisions < 1) throw new ArgumentOutOfRangeException(nameof(subdivisions));

            Vector3 half = size / 2f;
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)));
            string key = $"box:{size.x:F3}:{size.y:F3}:{size.z:F3}:{radius:F3}:{subdivisions}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 inner = half - Vector3.one * radius;

            // Each face is a grid; every grid point is pulled onto the rounded surface.
            Vector3[] faceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 n in faceNormals)
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u);
                int start = vertices.Count;
                for (int i = 0; i <= subdivisions; i++)
                {
                    for (int j = 0; j <= subdivisions; j++)
                    {
                        float a = (float)i / subdivisions * 2f - 1f;
                        float b = (float)j / subdivisions * 2f - 1f;
                        Vector3 p = Vector3.Scale(n + u * a + v * b, half);
                        Vector3 clamped = new Vector3(
                            Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 normal = p - clamped;
                        normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : n;
                        Vector3 vertex = clamped + normal * radius;
                        vertices.Add(vertex);
                        normals.Add(normal);
                        // Texture laid over the box from above: the top face shows it whole, the sides its edges.
                        uvs.Add(new Vector2(vertex.x / size.x + 0.5f, vertex.z / size.z + 0.5f));
                    }
                }

                int row = subdivisions + 1;
                for (int i = 0; i < subdivisions; i++)
                {
                    for (int j = 0; j < subdivisions; j++)
                    {
                        int k = start + i * row + j;
                        // Clockwise seen from outside (Unity's front face): cross(u, v) is the face normal.
                        triangles.Add(k);
                        triangles.Add(k + row);
                        triangles.Add(k + 1);
                        triangles.Add(k + 1);
                        triangles.Add(k + row);
                        triangles.Add(k + row + 1);
                    }
                }
            }

            return Store(key, vertices, normals, triangles, uvs);
        }

        /// <summary>
        /// A box with sharp edges whose four sides are each mapped on their own, for a skin that wraps round them: u runs
        /// along the sides in world units over <paramref name="repeat"/> (on round the box, so the sides meet), v up a side
        /// from its bottom (0) to its top (1). The top and the bottom take the middle of the skin's height.
        /// </summary>
        public static Mesh Slab(Vector3 size, float repeat)
        {
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f) throw new ArgumentOutOfRangeException(nameof(size));
            if (repeat <= 0f) throw new ArgumentOutOfRangeException(nameof(repeat));
            string key = $"slab:{size.x:F3}:{size.y:F3}:{size.z:F3}:{repeat:F3}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 half = size / 2f;
            float along = 0f;
            foreach (Vector3 n in new[] { Vector3.back, Vector3.right, Vector3.forward, Vector3.left })
            {
                // Clockwise seen from outside (Unity's front face): cross(u, v) is the face normal.
                Vector3 u = Vector3.Cross(Vector3.up, n);
                float length = Mathf.Abs(Vector3.Dot(u, size));
                Vector3 origin = Vector3.Scale(n, half) - u * (length / 2f) - Vector3.up * half.y;
                Quad(vertices, normals, uvs, triangles, origin, u * length, Vector3.up * size.y, n,
                    new Vector2(along / repeat, 0f), new Vector2((along + length) / repeat, 1f));
                along += length;
            }

            Quad(vertices, normals, uvs, triangles, new Vector3(-half.x, half.y, half.z), Vector3.right * size.x, Vector3.back * size.z, Vector3.up,
                new Vector2(0f, 0.5f), new Vector2(size.x / repeat, 0.5f));
            Quad(vertices, normals, uvs, triangles, new Vector3(-half.x, -half.y, -half.z), Vector3.right * size.x, Vector3.forward * size.z, Vector3.down,
                new Vector2(0f, 0.5f), new Vector2(size.x / repeat, 0.5f));
            return Store(key, vertices, normals, triangles, uvs);
        }

        private static void Quad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 origin, Vector3 u, Vector3 v, Vector3 normal, Vector2 uvFrom, Vector2 uvTo)
        {
            int k = vertices.Count;
            vertices.Add(origin);
            vertices.Add(origin + u);
            vertices.Add(origin + v);
            vertices.Add(origin + u + v);
            for (int i = 0; i < 4; i++) normals.Add(normal);
            uvs.Add(new Vector2(uvFrom.x, uvFrom.y));
            uvs.Add(new Vector2(uvTo.x, uvFrom.y));
            uvs.Add(new Vector2(uvFrom.x, uvTo.y));
            uvs.Add(new Vector2(uvTo.x, uvTo.y));
            triangles.Add(k);
            triangles.Add(k + 1);
            triangles.Add(k + 2);
            triangles.Add(k + 2);
            triangles.Add(k + 1);
            triangles.Add(k + 3);
        }

        /// <summary>
        /// A shape turned around the Y axis. <paramref name="profile"/> holds (radius, height) points
        /// from bottom to top; start and end at radius 0 for a closed solid.
        /// </summary>
        public static Mesh Lathe(string name, IReadOnlyList<Vector2> profile, int segments = 24)
        {
            if (profile == null || profile.Count < 2) throw new ArgumentException("A profile needs at least two points.", nameof(profile));
            if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments));

            string key = $"lathe:{name}:{segments}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int s = 0; s <= segments; s++)
            {
                float angle = (float)s / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                foreach (Vector2 point in profile) vertices.Add(new Vector3(point.x * cos, point.y, point.x * sin));
            }

            int count = profile.Count;
            for (int s = 0; s < segments; s++)
            {
                for (int p = 0; p < count - 1; p++)
                {
                    int a = s * count + p;
                    int b = (s + 1) * count + p;
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(b + 1);
                }
            }

            return Store(key, vertices, null, triangles);
        }

        /// <summary>A house: a box with a pitched roof. Base at y = 0, size (1, 1, 1) overall, ridge along X.</summary>
        public static Mesh House()
        {
            const string key = "house";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            const float wall = 0.6f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles.AddRange(new[] { i, i + 1, i + 2 });
            }

            float h = 0.5f;
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z);

            Quad(P(-h, 0, -h), P(-h, wall, -h), P(h, wall, -h), P(h, 0, -h));   // front
            Quad(P(h, 0, h), P(h, wall, h), P(-h, wall, h), P(-h, 0, h));       // back
            Quad(P(-h, 0, h), P(-h, wall, h), P(-h, wall, -h), P(-h, 0, -h));   // left
            Quad(P(h, 0, -h), P(h, wall, -h), P(h, wall, h), P(h, 0, h));       // right
            Quad(P(-h, wall, -h), P(-h, 1f, 0), P(h, 1f, 0), P(h, wall, -h));  // roof front
            Quad(P(h, wall, h), P(h, 1f, 0), P(-h, 1f, 0), P(-h, wall, h));    // roof back
            Triangle(P(-h, wall, h), P(-h, 1f, 0), P(-h, wall, -h));           // gable left
            Triangle(P(h, wall, -h), P(h, 1f, 0), P(h, wall, h));              // gable right
            Quad(P(-h, 0, h), P(-h, 0, -h), P(h, 0, -h), P(h, 0, h));           // floor

            return Store(key, vertices, null, triangles);
        }

        /// <summary>A triangular prism: a pitched roof. Base at y = 0, size (1, 1, 1), ridge along X.</summary>
        public static Mesh Prism()
        {
            const string key = "prism";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c });
                triangles.AddRange(new[] { i, i + 1, i + 2 });
            }

            const float h = 0.5f;
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
            Quad(P(-h, 0, -h), P(-h, 1f, 0), P(h, 1f, 0), P(h, 0, -h));   // front slope
            Quad(P(h, 0, h), P(h, 1f, 0), P(-h, 1f, 0), P(-h, 0, h));     // back slope
            Triangle(P(-h, 0, h), P(-h, 1f, 0), P(-h, 0, -h));            // gable left
            Triangle(P(h, 0, -h), P(h, 1f, 0), P(h, 0, h));               // gable right
            Quad(P(-h, 0, h), P(-h, 0, -h), P(h, 0, -h), P(h, 0, h));     // underside
            return Store(key, vertices, null, triangles);
        }

        /// <summary>
        /// A wedge: one tooth of a saw-tooth roof. Base at y = 0, size (1, 1, 1); an upright face at -X, and from its top
        /// one slope down to the base's edge at +X; the same section all along Z.
        /// </summary>
        public static Mesh Wedge()
        {
            const string key = "wedge";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c });
                triangles.AddRange(new[] { i, i + 1, i + 2 });
            }

            const float h = 0.5f;
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
            Quad(P(-h, 0, h), P(-h, 1f, h), P(-h, 1f, -h), P(-h, 0, -h));   // upright face
            Quad(P(-h, 1f, -h), P(-h, 1f, h), P(h, 0, h), P(h, 0, -h));     // slope
            Triangle(P(-h, 0, -h), P(-h, 1f, -h), P(h, 0, -h));             // end at -z
            Triangle(P(h, 0, h), P(-h, 1f, h), P(-h, 0, h));                // end at +z
            Quad(P(-h, 0, h), P(-h, 0, -h), P(h, 0, -h), P(h, 0, h));       // underside
            return Store(key, vertices, null, triangles);
        }

        /// <summary>A cylinder with flat caps, base at y = 0, radius and height 1 (scale it as needed).</summary>
        public static Mesh Cylinder(int segments = 24) =>
            Lathe("cylinder", new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) }, segments);

        /// <summary>A cone, base at y = 0, radius and height 1.</summary>
        public static Mesh Cone(int segments = 24) =>
            Lathe("cone", new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) }, segments);

        /// <summary>A ball of radius 1 centred on the origin.</summary>
        public static Mesh Sphere(int segments = 12)
        {
            var profile = new List<Vector2>();
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.PI * i / segments;
                profile.Add(new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)));
            }

            return Lathe($"sphere:{segments}", profile, segments * 2);
        }

        /// <summary>A flat disc at y = 0 with radius 1 and the given thickness.</summary>
        public static Mesh Disc(float thickness, int segments = 48) =>
            Lathe($"disc:{thickness:F3}", new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, thickness), new Vector2(0f, thickness) }, segments);

        /// <summary>A ring lying flat: inner radius <paramref name="inner"/>, outer radius 1, given thickness.</summary>
        public static Mesh Ring(float inner, float thickness, int segments = 48) =>
            Lathe($"ring:{inner:F3}:{thickness:F3}", new[]
            {
                new Vector2(inner, 0f), new Vector2(1f, 0f), new Vector2(1f, thickness), new Vector2(inner, thickness), new Vector2(inner, 0f),
            }, segments);

        /// <summary>
        /// A heraldic shield standing up (Faz 30's protection card): flat top, straight sides that curve in to a point at
        /// the bottom, 1 wide and 1.2 tall with its point at y = 0, <paramref name="thickness"/> deep along Z. The outline is
        /// convex, so each face is a fan around its middle.
        /// </summary>
        public static Mesh Shield(float thickness = 0.16f, int curve = 8)
        {
            string key = $"shield:{thickness:F3}:{curve}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            var outline = new List<Vector2> { new Vector2(-0.5f, 1.2f), new Vector2(0.5f, 1.2f), new Vector2(0.5f, 0.75f) };
            for (int i = 1; i < curve; i++)
            {
                float t = (float)i / curve; // Down the right side to the point, then the left side mirrored.
                outline.Add(new Vector2(0.5f * Mathf.Cos(t * Mathf.PI / 2f), 0.75f * (1f - Mathf.Sin(t * Mathf.PI / 2f))));
            }

            outline.Add(new Vector2(0f, 0f));
            for (int i = curve - 1; i >= 1; i--) outline.Add(new Vector2(-outline[2 + i].x, outline[2 + i].y));
            outline.Add(new Vector2(-0.5f, 0.75f));

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float half = thickness / 2f;
            var middle = new Vector2(0f, 0.7f);
            foreach (float z in new[] { -half, half })
            {
                int centre = vertices.Count;
                vertices.Add(new Vector3(middle.x, middle.y, z));
                foreach (Vector2 point in outline) vertices.Add(new Vector3(point.x, point.y, z));
                for (int i = 0; i < outline.Count; i++)
                {
                    int a = centre + 1 + i;
                    int b = centre + 1 + (i + 1) % outline.Count;
                    // The outline runs clockwise seen from -Z: the front (-Z) keeps that order, the back flips it.
                    if (z < 0f) triangles.AddRange(new[] { centre, a, b });
                    else triangles.AddRange(new[] { centre, b, a });
                }
            }

            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 p = outline[i];
                Vector2 q = outline[(i + 1) % outline.Count];
                int start = vertices.Count;
                vertices.Add(new Vector3(p.x, p.y, -half));
                vertices.Add(new Vector3(q.x, q.y, -half));
                vertices.Add(new Vector3(q.x, q.y, half));
                vertices.Add(new Vector3(p.x, p.y, half));
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            return Store(key, vertices, null, triangles);
        }

        /// <summary>Several meshes merged into one, each placed by its matrix. Cached under <paramref name="name"/>.</summary>
        public static Mesh Combine(string name, IReadOnlyList<KeyValuePair<Mesh, Matrix4x4>> parts)
        {
            if (parts == null || parts.Count == 0) throw new ArgumentException("Nothing to combine.", nameof(parts));
            string key = $"combined:{name}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            var combine = new CombineInstance[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                combine[i] = new CombineInstance { mesh = parts[i].Key, transform = parts[i].Value };
            }

            var mesh = new Mesh { name = key };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        private static Mesh Store(string key, List<Vector3> vertices, List<Vector3> normals, List<int> triangles, List<Vector2> uvs = null)
        {
            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (normals != null) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            if (uvs != null) mesh.SetUVs(0, uvs);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }
    }
}
