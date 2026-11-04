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
    /// Reads the building pack Tools/Art/make_buildings.py writes (buildings.bytes): "LZB1", the number of
    /// models, then for each its name, vertex positions (float), normals (16-bit, in 1/32767) and
    /// 16-bit triangle indices, all little-endian.
    /// </summary>
    public static class BuildingPack
    {
        public static readonly byte[] Magic = Encoding.ASCII.GetBytes("LZB1");

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
                    if (count < 0 || count > 64) throw new FormatException($"{count} models");
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
            if (vertices <= 0 || vertices > ushort.MaxValue + 1) throw new FormatException($"{name}: {vertices} vertices");

            var positions = new Vector3[vertices];
            for (int i = 0; i < vertices; i++) positions[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            var normals = new Vector3[vertices];
            for (int i = 0; i < vertices; i++)
            {
                normals[i] = new Vector3(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()) / 32767f;
            }

            int indices = reader.ReadInt32();
            if (indices <= 0 || indices % 3 != 0) throw new FormatException($"{name}: {indices} indices");
            var triangles = new int[indices];
            for (int i = 0; i < indices; i++)
            {
                int index = reader.ReadUInt16();
                if (index >= vertices) throw new FormatException($"{name}: index {index} past {vertices} vertices");
                triangles[i] = index;
            }

            return new BuildingMesh(name, positions, normals, triangles);
        }
    }
}
