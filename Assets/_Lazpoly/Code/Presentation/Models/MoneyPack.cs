using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lazpoly.Presentation.Models
{
    /// <summary>One part of the money set: a mesh in one material (a colour, metalness and smoothness, or the logo).</summary>
    public sealed class MoneyPart
    {
        public MoneyPart(string name, Color color, float metallic, float smoothness, bool logo, Vector3[] positions, Vector3[] normals, Vector2[] uvs, int[] triangles)
        {
            Name = name;
            Color = color;
            Metallic = metallic;
            Smoothness = smoothness;
            Logo = logo;
            Positions = positions;
            Normals = normals;
            Uvs = uvs;
            Triangles = triangles;
        }

        /// <summary>"Coin/&lt;material&gt;" or "Note/&lt;material&gt;".</summary>
        public string Name { get; }

        /// <summary>The model the part belongs to: the text before the slash.</summary>
        public string Model => Name.Substring(0, Math.Max(0, Name.IndexOf('/')));

        public Color Color { get; }
        public float Metallic { get; }
        public float Smoothness { get; }

        /// <summary>Whether the part is a plane that carries the logo, its clear pixels cut away.</summary>
        public bool Logo { get; }

        public Vector3[] Positions { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Uvs { get; }
        public int[] Triangles { get; }

        public Mesh ToMesh()
        {
            var mesh = new Mesh { name = $"money-{Name}" };
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
    /// Reads the money pack Tools/Art/make_money.py writes (money.bytes): "LZM1", the number of parts, then for each its
    /// name, colour (r, g, b), metalness and smoothness as floats, a byte that is 1 for the logo planes, the vertex
    /// positions (float), normals (16-bit, in 1/32767), UVs (16-bit, in 1/65535), the index size (2 or 4 bytes) and the
    /// triangle indices, all little-endian.
    /// </summary>
    public static class MoneyPack
    {
        public static readonly byte[] Magic = Encoding.ASCII.GetBytes("LZM1");

        /// <summary>The most parts a pack may hold.</summary>
        public const int MaxParts = 64;

        /// <exception cref="FormatException">The data is not a whole, valid pack.</exception>
        public static IReadOnlyList<MoneyPart> Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(data, false)))
                {
                    byte[] magic = reader.ReadBytes(Magic.Length);
                    for (int i = 0; i < Magic.Length; i++)
                    {
                        if (magic.Length != Magic.Length || magic[i] != Magic[i]) throw new FormatException("not a money pack");
                    }

                    int count = reader.ReadInt32();
                    if (count <= 0 || count > MaxParts) throw new FormatException($"{count} parts");
                    var parts = new List<MoneyPart>(count);
                    for (int p = 0; p < count; p++) parts.Add(ReadPart(reader));
                    if (reader.BaseStream.Position != data.Length) throw new FormatException("data after the last part");
                    return parts;
                }
            }
            catch (EndOfStreamException)
            {
                throw new FormatException("the pack ends in the middle of a part");
            }
        }

        private static MoneyPart ReadPart(BinaryReader reader)
        {
            int nameLength = reader.ReadByte();
            string name = Encoding.ASCII.GetString(reader.ReadBytes(nameLength));
            var color = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            float metallic = reader.ReadSingle();
            float smoothness = reader.ReadSingle();
            bool logo = reader.ReadByte() != 0;
            int vertices = reader.ReadInt32();
            if (vertices <= 0 || vertices > 1 << 20) throw new FormatException($"{name}: {vertices} vertices");

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
            if (indices <= 0 || indices % 3 != 0 || indices > 1 << 22) throw new FormatException($"{name}: {indices} indices");
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

            return new MoneyPart(name, color, metallic, smoothness, logo, positions, normals, uvs, triangles);
        }

        private static byte[] Read(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException();
            return bytes;
        }
    }
}
