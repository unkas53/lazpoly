using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lazpoly.Presentation.Models
{
    /// <summary>One level of detail of a textured figure: positions, normals, texture coordinates and triangles.</summary>
    public sealed class FigureMesh
    {
        public FigureMesh(string name, Vector3[] positions, Vector3[] normals, Vector2[] uvs, int[] triangles)
        {
            Name = name;
            Positions = positions;
            Normals = normals;
            Uvs = uvs;
            Triangles = triangles;
        }

        public string Name { get; }
        public Vector3[] Positions { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Uvs { get; }
        public int[] Triangles { get; }

        public int TriangleCount => Triangles.Length / 3;

        public Mesh ToMesh(string meshName)
        {
            var mesh = new Mesh { name = meshName };
            if (Positions.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = Positions;
            mesh.normals = Normals;
            mesh.uv = Uvs;
            mesh.triangles = Triangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }

    /// <summary>
    /// Reads a figure file Tools/Art/make_figures.py writes (figure_&lt;id&gt;.bytes): "LZF1", the number of levels,
    /// then for each its name ("High", "Low"), vertex positions (float), normals (16-bit, in 1/32767), texture
    /// coordinates (16-bit, in 1/65535), the index size (2 or 4 bytes) and the triangle indices, all little-endian.
    /// </summary>
    public static class FigurePack
    {
        public static readonly byte[] Magic = Encoding.ASCII.GetBytes("LZF1");

        public const string HighLevel = "High";
        public const string LowLevel = "Low";

        /// <exception cref="FormatException">The data is not a whole, valid figure.</exception>
        public static IReadOnlyList<FigureMesh> Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(data, false)))
                {
                    byte[] magic = reader.ReadBytes(Magic.Length);
                    for (int i = 0; i < Magic.Length; i++)
                    {
                        if (magic.Length != Magic.Length || magic[i] != Magic[i]) throw new FormatException("not a figure");
                    }

                    int count = reader.ReadByte();
                    if (count == 0) throw new FormatException("no levels");
                    var levels = new List<FigureMesh>(count);
                    for (int i = 0; i < count; i++) levels.Add(ReadLevel(reader));
                    if (reader.BaseStream.Position != data.Length) throw new FormatException("data after the last level");
                    return levels;
                }
            }
            catch (EndOfStreamException)
            {
                throw new FormatException("the figure ends in the middle of a level");
            }
        }

        private static FigureMesh ReadLevel(BinaryReader reader)
        {
            int nameLength = reader.ReadByte();
            string name = Encoding.ASCII.GetString(reader.ReadBytes(nameLength));
            int vertices = reader.ReadInt32();
            if (vertices <= 0 || vertices > 1 << 22) throw new FormatException($"{name}: {vertices} vertices");

            var coordinates = new float[vertices * 3];
            Buffer.BlockCopy(Read(reader, coordinates.Length * 4), 0, coordinates, 0, coordinates.Length * 4);
            var positions = new Vector3[vertices];
            for (int i = 0; i < vertices; i++) positions[i] = new Vector3(coordinates[3 * i], coordinates[3 * i + 1], coordinates[3 * i + 2]);

            var packed = new short[vertices * 3];
            Buffer.BlockCopy(Read(reader, packed.Length * 2), 0, packed, 0, packed.Length * 2);
            var normals = new Vector3[vertices];
            for (int i = 0; i < vertices; i++) normals[i] = new Vector3(packed[3 * i], packed[3 * i + 1], packed[3 * i + 2]) / 32767f;

            var texture = new ushort[vertices * 2];
            Buffer.BlockCopy(Read(reader, texture.Length * 2), 0, texture, 0, texture.Length * 2);
            var uvs = new Vector2[vertices];
            for (int i = 0; i < vertices; i++) uvs[i] = new Vector2(texture[2 * i], texture[2 * i + 1]) / 65535f;

            int indexSize = reader.ReadByte();
            if (indexSize != 2 && indexSize != 4) throw new FormatException($"{name}: {indexSize}-byte indices");
            int indices = reader.ReadInt32();
            if (indices <= 0 || indices % 3 != 0 || indices > 1 << 24) throw new FormatException($"{name}: {indices} indices");
            var triangles = new int[indices];
            if (indexSize == 4)
            {
                Buffer.BlockCopy(Read(reader, indices * 4), 0, triangles, 0, indices * 4);
            }
            else
            {
                var narrow = new ushort[indices];
                Buffer.BlockCopy(Read(reader, indices * 2), 0, narrow, 0, indices * 2);
                for (int i = 0; i < indices; i++) triangles[i] = narrow[i];
            }

            foreach (int index in triangles)
            {
                if (index < 0 || index >= vertices) throw new FormatException($"{name}: index {index} past {vertices} vertices");
            }

            return new FigureMesh(name, positions, normals, uvs, triangles);
        }

        private static byte[] Read(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException();
            return bytes;
        }
    }
}
