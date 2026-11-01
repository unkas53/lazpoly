using System.Collections.Generic;
using Lazpoly.Core.Board;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// Small signs that stand on the special squares so they read from across the table: a tea glass
    /// on the pot square, a bus at the bus station, binoculars on the lookout terrace and a folded
    /// newspaper on the card square. Each is one mesh about a unit wide with its base at y = 0.
    /// </summary>
    public static class SquareIcons
    {
        /// <summary>The sign for a square type, or null for squares that carry none.</summary>
        public static Mesh For(SquareType type)
        {
            switch (type)
            {
                case SquareType.Pot: return PawnShapes.TeaGlass();
                case SquareType.Travel: return Bus();
                case SquareType.Lookout: return Binoculars();
                case SquareType.Card: return Newspaper();
                default: return null;
            }
        }

        /// <summary>The colour each sign is painted.</summary>
        public static Color ColorFor(SquareType type)
        {
            switch (type)
            {
                case SquareType.Pot: return new Color(0.72f, 0.30f, 0.16f);
                case SquareType.Travel: return new Color(0.93f, 0.55f, 0.18f);
                case SquareType.Lookout: return new Color(0.22f, 0.24f, 0.30f);
                default: return new Color(0.97f, 0.96f, 0.92f);
            }
        }

        /// <summary>A minibus: a rounded body on four wheels, facing +Z.</summary>
        public static Mesh Bus()
        {
            Quaternion axle = Quaternion.Euler(0f, 0f, 90f); // The cylinder's Y becomes X: wheels turn on an axle across the bus.
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(0.6f, 0.5f, 1f), 0.08f, 3), Matrix4x4.TRS(new Vector3(0f, 0.4f, 0f), Quaternion.identity, Vector3.one)),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(0.5f, 0.06f, 0.5f), 0.02f, 2), Matrix4x4.TRS(new Vector3(0f, 0.68f, -0.1f), Quaternion.identity, Vector3.one)),
            };
            foreach (float z in new[] { -0.32f, 0.32f })
            {
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(12), Matrix4x4.TRS(new Vector3(0.36f, 0.15f, z), axle, new Vector3(0.15f, 0.72f, 0.15f))));
            }

            return MeshFactory.Combine("icon:bus", parts);
        }

        /// <summary>Binoculars lying on the terrace: two barrels joined by a bridge, looking along +Z.</summary>
        public static Mesh Binoculars()
        {
            Quaternion along = Quaternion.Euler(90f, 0f, 0f); // The cylinder's Y becomes Z.
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>();
            foreach (float x in new[] { -0.23f, 0.23f })
            {
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(14), Matrix4x4.TRS(new Vector3(x, 0.2f, -0.1f), along, new Vector3(0.2f, 0.7f, 0.2f))));
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(14), Matrix4x4.TRS(new Vector3(x, 0.2f, -0.4f), along, new Vector3(0.13f, 0.3f, 0.13f))));
            }

            parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(0.3f, 0.16f, 0.4f), 0.03f, 2), Matrix4x4.TRS(new Vector3(0f, 0.22f, 0.05f), Quaternion.identity, Vector3.one)));
            return MeshFactory.Combine("icon:binoculars", parts);
        }

        /// <summary>A folded newspaper: a flat sheet with its top half folded back on itself.</summary>
        public static Mesh Newspaper()
        {
            return MeshFactory.Combine("icon:newspaper", new[]
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(0.9f, 0.06f, 0.7f), 0.02f, 2), Matrix4x4.TRS(new Vector3(0f, 0.03f, 0f), Quaternion.Euler(0f, -12f, 0f), Vector3.one)),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(0.9f, 0.06f, 0.34f), 0.02f, 2), Matrix4x4.TRS(new Vector3(0f, 0.09f, 0.18f), Quaternion.Euler(0f, -12f, 0f), Vector3.one)),
            });
        }
    }
}
