using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lazpoly.Presentation.Models
{
    /// <summary>One model of the building pack: positions, normals and triangles, in the model's own units.</summary>
    public sealed class BuildingMesh
    {
        public BuildingMesh(string name, Vector3[] positions, Vector3[] normals, int[] triangles)
        {
            Name = name;
            Positions = positions;
            Normals = normals;
            Triangles = triangles;
        }

        public string Name { get; }
        public Vector3[] Positions { get; }
        public Vector3[] Normals { get; }
        public int[] Triangles { get; }

        public Mesh ToMesh()
        {
            var mesh = new Mesh { name = $"building-{Name}" };
            if (Positions.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = Positions;
            mesh.normals = Normals;
            mesh.triangles = Triangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }

    /// <summary>
    /// Reads the building pack Tools/Art/make_buildings.py writes (buildings.bytes): "LZB2", the number of
    /// meshes, then for each its name, vertex positions (float), normals (16-bit, in 1/32767), the index size
    /// (2 or 4 bytes) and the triangle indices, all little-endian.
    /// </summary>
    public static class BuildingPack
    {
        public static readonly byte[] Magic = Encoding.ASCII.GetBytes("LZB2");

        /// <summary>The most meshes a pack may hold: every model's slots, whole and light, with room to spare.</summary>
        public const int MaxMeshes = 256;

        /// <exception cref="FormatException">The data is not a whole, valid pack.</exception>
        public static IReadOnlyList<BuildingMesh> Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(data, false)))
                {
                    byte[] magic = reader.ReadBytes(Magic.Length);
                    for (int i = 0; i < Magic.Length; i++)
                    {
                        if (magic.Length != Magic.Length || magic[i] != Magic[i]) throw new FormatException("not a building pack");
                    }

                    int count = reader.ReadInt32();
                    if (count < 0 || count > MaxMeshes) throw new FormatException($"{count} meshes");
                    var meshes = new List<BuildingMesh>(count);
                    for (int m = 0; m < count; m++) meshes.Add(ReadMesh(reader));
                    if (reader.BaseStream.Position != data.Length) throw new FormatException("data after the last model");
                    return meshes;
                }
            }
            catch (EndOfStreamException)
            {
                throw new FormatException("the pack ends in the middle of a model");
            }
        }

        private static BuildingMesh ReadMesh(BinaryReader reader)
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

            return new BuildingMesh(name, positions, normals, triangles);
        }

        private static byte[] Read(BinaryReader reader, int count)
        {
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException();
            return bytes;
        }
    }
}
