using System.Collections.Generic;
using Lazpoly.Core.Board;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// Small signs that stand on the special squares so they read from across the table: a tea glass
    /// on the pot square, a barred window at the jail, a bus at the bus station and binoculars on the lookout terrace.
    /// A card square carries none: the card corner has its kiosk scene and a Şans square a question mark printed flat
    /// on the tile (Faz 24). Each is one mesh about a unit wide with its base at y = 0.
    /// </summary>
    public static class SquareIcons
    {
        /// <summary>The sign for a square type, or null for squares that carry none.</summary>
        public static Mesh For(SquareType type)
        {
            switch (type)
            {
                case SquareType.Pot: return PawnShapes.TeaGlass();
                case SquareType.Jail: return Bars();
                case SquareType.Travel: return Bus();
                case SquareType.Lookout: return Binoculars();
                default: return null;
            }
        }

        /// <summary>The colour each sign is painted.</summary>
        public static Color ColorFor(SquareType type)
        {
            switch (type)
            {
                case SquareType.Pot: return new Color(0.72f, 0.30f, 0.16f);
                case SquareType.Jail: return new Color(0.22f, 0.24f, 0.28f);
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

        /// <summary>A barred window standing upright: a frame with five bars, facing +Z.</summary>
        public static Mesh Bars()
        {
            var parts = new List<KeyValuePair<Mesh, Matrix4x4>>
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(0.8f, 0.08f, 0.12f), 0.02f, 2), Matrix4x4.TRS(new Vector3(0f, 0.04f, 0f), Quaternion.identity, Vector3.one)),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(0.8f, 0.08f, 0.12f), 0.02f, 2), Matrix4x4.TRS(new Vector3(0f, 0.76f, 0f), Quaternion.identity, Vector3.one)),
            };
            for (int i = -2; i <= 2; i++)
            {
                parts.Add(new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(10), Matrix4x4.TRS(new Vector3(i * 0.17f, 0.08f, 0f), Quaternion.identity, new Vector3(0.035f, 0.64f, 0.035f))));
            }

            return MeshFactory.Combine("icon:bars", parts);
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
    }
}
