using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>One part of an <see cref="ObjModel"/>: the faces that share an object/material name.</summary>
    public sealed class ObjGroup
    {
        public ObjGroup(string name, string material, Vector3[] vertices, Vector3[] normals, int[] triangles)
        {
            Name = name;
            Material = material;
            Vertices = vertices;
            Normals = normals;
            Triangles = triangles;
        }

        /// <summary>The "o"/"g" name in the file.</summary>
        public string Name { get; }

        /// <summary>The "usemtl" name; the game tells parts apart by it (e.g. a character's clothes).</summary>
        public string Material { get; }

        public Vector3[] Vertices { get; }

        /// <summary>Per-vertex normals from the file, or empty when the file has none.</summary>
        public Vector3[] Normals { get; }

        public int[] Triangles { get; }

        public int TriangleCount => Triangles.Length / 3;

        public Bounds Bounds
        {
            get
            {
                if (Vertices.Length == 0) return new Bounds();
                var bounds = new Bounds(Vertices[0], Vector3.zero);
                foreach (Vector3 vertex in Vertices) bounds.Encapsulate(vertex);
                return bounds;
            }
        }

        /// <summary>A mesh of this part; normals are taken from the file or computed when it has none.</summary>
        public Mesh ToMesh(string meshName)
        {
            var mesh = new Mesh { name = meshName, vertices = Vertices, triangles = Triangles };
            if (Normals.Length == Vertices.Length) mesh.normals = Normals;
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// A small Wavefront OBJ reader for the hand-made models that ship as text assets: vertices,
    /// normals and faces, grouped by object and material. Texture coordinates are read past and
    /// dropped (the models are flat-coloured). OBJ is right-handed and Unity is not, so X is
    /// mirrored and every face's winding reversed, which is what Unity's own importer does.
    /// </summary>
    public sealed class ObjModel
    {
        private ObjModel(IReadOnlyList<ObjGroup> groups)
        {
            Groups = groups;
        }

        public IReadOnlyList<ObjGroup> Groups { get; }

        public int TriangleCount
        {
            get
            {
                int count = 0;
                foreach (ObjGroup group in Groups) count += group.TriangleCount;
                return count;
            }
        }

        /// <summary>The whole model's bounds.</summary>
        public Bounds Bounds
        {
            get
            {
                Bounds? bounds = null;
                foreach (ObjGroup group in Groups)
                {
                    if (group.Vertices.Length == 0) continue;
                    if (bounds == null) bounds = group.Bounds;
                    else
                    {
                        Bounds b = bounds.Value;
                        b.Encapsulate(group.Bounds);
                        bounds = b;
                    }
                }

                return bounds ?? new Bounds();
            }
        }

        /// <summary>The first group with this material name, or null.</summary>
        public ObjGroup Find(string material)
        {
            foreach (ObjGroup group in Groups) if (group.Material == material) return group;
            return null;
        }

        /// <summary>Reads OBJ text. Throws <see cref="FormatException"/> naming the line when the text is not OBJ.</summary>
        public static ObjModel Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var groups = new List<GroupBuilder>();
            GroupBuilder current = null;
            string[] lines = text.Split('\n');
            for (int number = 0; number < lines.Length; number++)
            {
                string[] parts = lines[number].Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || parts[0].StartsWith("#", StringComparison.Ordinal)) continue;

                switch (parts[0])
                {
                    case "v":
                        positions.Add(ReadVector(parts, number, flipX: true));
                        break;
                    case "vn":
                        normals.Add(ReadVector(parts, number, flipX: true).normalized);
                        break;
                    case "o":
                    case "g":
                        current = new GroupBuilder(parts.Length > 1 ? parts[1] : $"group{groups.Count}");
                        groups.Add(current);
                        break;
                    case "usemtl":
                        if (current == null || current.HasFaces)
                        {
                            current = new GroupBuilder(parts.Length > 1 ? parts[1] : $"group{groups.Count}");
                            groups.Add(current);
                        }

                        current.Material = parts.Length > 1 ? parts[1] : current.Name;
                        break;
                    case "f":
                        if (parts.Length < 4) throw new FormatException($"Line {number + 1}: a face needs at least three corners.");
                        if (current == null)
                        {
                            current = new GroupBuilder("default");
                            groups.Add(current);
                        }

                        current.AddFace(parts, number, positions, normals);
                        break;
                    // vt, s, mtllib and anything else are not needed for flat-coloured models.
                }
            }

            var result = new List<ObjGroup>(groups.Count);
            foreach (GroupBuilder group in groups)
            {
                if (group.HasFaces) result.Add(group.Build());
            }

            return new ObjModel(result);
        }

        private static Vector3 ReadVector(string[] parts, int line, bool flipX)
        {
            if (parts.Length < 4) throw new FormatException($"Line {line + 1}: expected three numbers.");
            float x = ReadFloat(parts[1], line);
            float y = ReadFloat(parts[2], line);
            float z = ReadFloat(parts[3], line);
            return new Vector3(flipX ? -x : x, y, z);
        }

        private static float ReadFloat(string text, int line)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new FormatException($"Line {line + 1}: '{text}' is not a number.");
            }

            return value;
        }

        /// <summary>Collects one group's corners, giving every distinct position/normal pair its own vertex.</summary>
        private sealed class GroupBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();
            private readonly Dictionary<(int, int), int> _index = new Dictionary<(int, int), int>();
            private bool _anyNormal;
            private bool _missingNormal;

            public GroupBuilder(string name)
            {
                Name = name;
                Material = name;
            }

            public string Name { get; }
            public string Material { get; set; }
            public bool HasFaces => _triangles.Count > 0;

            public void AddFace(string[] parts, int line, List<Vector3> positions, List<Vector3> normals)
            {
                var corners = new int[parts.Length - 1];
                for (int i = 1; i < parts.Length; i++) corners[i - 1] = Corner(parts[i], line, positions, normals);

                // A polygon becomes a fan; the winding is reversed because X was mirrored.
                for (int i = 1; i + 1 < corners.Length; i++)
                {
                    _triangles.Add(corners[0]);
                    _triangles.Add(corners[i + 1]);
                    _triangles.Add(corners[i]);
                }
            }

            private int Corner(string text, int line, List<Vector3> positions, List<Vector3> normals)
            {
                string[] fields = text.Split('/');
                int position = Resolve(fields[0], positions.Count, line);
                int normal = fields.Length > 2 && fields[2].Length > 0 ? Resolve(fields[2], normals.Count, line) : -1;
                if (normal >= 0) _anyNormal = true;
                else _missingNormal = true;

                if (_index.TryGetValue((position, normal), out int index)) return index;
                index = _vertices.Count;
                _index[(position, normal)] = index;
                _vertices.Add(positions[position]);
                _normals.Add(normal >= 0 ? normals[normal] : Vector3.zero);
                return index;
            }

            /// <summary>OBJ indices count from 1; negative ones count back from the end.</summary>
            private static int Resolve(string text, int count, int line)
            {
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value == 0)
                {
                    throw new FormatException($"Line {line + 1}: '{text}' is not an index.");
                }

                int index = value > 0 ? value - 1 : count + value;
                if (index < 0 || index >= count) throw new FormatException($"Line {line + 1}: index {value} is out of range.");
                return index;
            }

            public ObjGroup Build()
            {
                Vector3[] normals = _anyNormal && !_missingNormal ? _normals.ToArray() : Array.Empty<Vector3>();
                return new ObjGroup(Name, Material, _vertices.ToArray(), normals, _triangles.ToArray());
            }
        }
    }
}
