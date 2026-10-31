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
                        vertices.Add(clamped + normal * radius);
                        normals.Add(normal);
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

            return Store(key, vertices, normals, triangles);
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

        /// <summary>A cylinder with flat caps, base at y = 0, radius and height 1 (scale it as needed).</summary>
        public static Mesh Cylinder(int segments = 24) =>
            Lathe("cylinder", new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) }, segments);

        /// <summary>A cone, base at y = 0, radius and height 1.</summary>
        public static Mesh Cone(int segments = 24) =>
            Lathe("cone", new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) }, segments);

        /// <summary>A flat disc at y = 0 with radius 1 and the given thickness.</summary>
        public static Mesh Disc(float thickness, int segments = 48) =>
            Lathe($"disc:{thickness:F3}", new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, thickness), new Vector2(0f, thickness) }, segments);

        /// <summary>A ring lying flat: inner radius <paramref name="inner"/>, outer radius 1, given thickness.</summary>
        public static Mesh Ring(float inner, float thickness, int segments = 48) =>
            Lathe($"ring:{inner:F3}:{thickness:F3}", new[]
            {
                new Vector2(inner, 0f), new Vector2(1f, 0f), new Vector2(1f, thickness), new Vector2(inner, thickness), new Vector2(inner, 0f),
            }, segments);

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

        private static Mesh Store(string key, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (normals != null) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }
    }
}
