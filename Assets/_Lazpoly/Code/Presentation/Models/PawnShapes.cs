using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The eight playing pieces, all from the seaside town the board describes: a tea glass, a
    /// lighthouse, a simit, a buoy, a bell, a boat, a teapot and a clock tower. Each is one mesh about
    /// 0.55 units tall with its base at y = 0, painted in the player's colour.
    /// </summary>
    public static class PawnShapes
    {
        public static readonly string[] Names = { "Çay Bardağı", "Fener", "Simit", "Şamandıra", "Çan", "Kayık", "Çaydanlık", "Saat Kulesi" };

        public static int Count => Names.Length;

        public static string NameOf(int seatIndex) => Names[Index(seatIndex)];

        public static Mesh For(int seatIndex)
        {
            switch (Index(seatIndex))
            {
                case 0: return TeaGlass();
                case 1: return Lighthouse();
                case 2: return Simit();
                case 3: return Buoy();
                case 4: return Bell();
                case 5: return Boat();
                case 6: return Teapot();
                default: return ClockTower();
            }
        }

        private static int Index(int seatIndex) => ((seatIndex % Count) + Count) % Count;

        private static Vector2 P(float radius, float height) => new Vector2(radius, height);

        /// <summary>The slim-waisted Turkish tea glass on its saucer.</summary>
        public static Mesh TeaGlass() => MeshFactory.Lathe("pawn:teaglass", new[]
        {
            P(0f, 0f), P(0.21f, 0f), P(0.22f, 0.02f), P(0.12f, 0.035f), P(0.125f, 0.05f), P(0.135f, 0.16f),
            P(0.10f, 0.30f), P(0.145f, 0.45f), P(0.15f, 0.53f), P(0.13f, 0.535f), P(0f, 0.535f),
        });

        public static Mesh Lighthouse() => MeshFactory.Lathe("pawn:lighthouse", new[]
        {
            P(0f, 0f), P(0.21f, 0f), P(0.21f, 0.04f), P(0.14f, 0.05f), P(0.095f, 0.40f), P(0.125f, 0.41f),
            P(0.125f, 0.49f), P(0.10f, 0.50f), P(0.12f, 0.53f), P(0.04f, 0.60f), P(0f, 0.61f),
        });

        /// <summary>A sesame ring standing on a small base so it reads from the table's eye level.</summary>
        public static Mesh Simit()
        {
            const int points = 16;
            var profile = new List<Vector2> { P(0f, 0f), P(0.2f, 0f), P(0.2f, 0.04f) };
            for (int i = 0; i <= points; i++)
            {
                float angle = (float)i / points * Mathf.PI * 2f;
                profile.Add(P(0.15f + 0.075f * Mathf.Cos(angle), 0.115f + 0.075f * Mathf.Sin(angle)));
            }

            profile.Add(P(0.07f, 0.04f));
            profile.Add(P(0f, 0.04f));
            // Lying flat it would vanish under the camera, so the ring stands upright on its base.
            Mesh flat = MeshFactory.Lathe("pawn:simit-flat", profile);
            Matrix4x4 upright = Matrix4x4.TRS(new Vector3(0f, 0.23f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            Mesh foot = MeshFactory.Disc(0.04f);
            return MeshFactory.Combine("pawn:simit", new[]
            {
                new KeyValuePair<Mesh, Matrix4x4>(flat, upright),
                new KeyValuePair<Mesh, Matrix4x4>(foot, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.21f, 1f, 0.21f))),
            });
        }

        public static Mesh Buoy() => MeshFactory.Lathe("pawn:buoy", new[]
        {
            P(0f, 0f), P(0.17f, 0f), P(0.18f, 0.05f), P(0.16f, 0.22f), P(0.09f, 0.30f), P(0.05f, 0.44f),
            P(0.075f, 0.49f), P(0.05f, 0.55f), P(0f, 0.56f),
        });

        public static Mesh Bell() => MeshFactory.Lathe("pawn:bell", new[]
        {
            P(0f, 0f), P(0.20f, 0f), P(0.21f, 0.025f), P(0.17f, 0.06f), P(0.13f, 0.30f), P(0.115f, 0.42f),
            P(0.06f, 0.46f), P(0.04f, 0.50f), P(0.055f, 0.55f), P(0f, 0.57f),
        });

        /// <summary>A rowing boat with a mast and a small sail.</summary>
        public static Mesh Boat()
        {
            Mesh hull = MeshFactory.Lathe("pawn:hull", new[]
            {
                P(0f, 0f), P(0.6f, 0f), P(1f, 0.55f), P(1f, 1f), P(0.85f, 1f), P(0.85f, 0.62f), P(0.5f, 0.2f), P(0f, 0.2f),
            }, 20);
            Matrix4x4 hullAt = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.14f, 0.18f, 0.30f));
            Matrix4x4 mastAt = Matrix4x4.TRS(new Vector3(0f, 0.1f, 0f), Quaternion.identity, new Vector3(0.018f, 0.45f, 0.018f));
            Matrix4x4 sailAt = Matrix4x4.TRS(new Vector3(0f, 0.36f, 0.1f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.2f, 0.3f, 0.015f));
            return MeshFactory.Combine("pawn:boat", new[]
            {
                new KeyValuePair<Mesh, Matrix4x4>(hull, hullAt),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cylinder(10), mastAt),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Prism(), sailAt),
            });
        }

        public static Mesh Teapot()
        {
            Mesh body = MeshFactory.Lathe("pawn:teapot-body", new[]
            {
                P(0f, 0f), P(0.13f, 0f), P(0.20f, 0.08f), P(0.21f, 0.20f), P(0.16f, 0.32f), P(0.10f, 0.35f),
                P(0.11f, 0.38f), P(0.05f, 0.42f), P(0.06f, 0.46f), P(0f, 0.47f),
            });
            Matrix4x4 spoutAt = Matrix4x4.TRS(new Vector3(0.17f, 0.14f, 0f), Quaternion.Euler(0f, 0f, -50f), new Vector3(0.035f, 0.26f, 0.035f));
            Matrix4x4 handleAt = Matrix4x4.TRS(new Vector3(-0.19f, 0.2f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.09f, 1f, 0.09f));
            return MeshFactory.Combine("pawn:teapot", new[]
            {
                new KeyValuePair<Mesh, Matrix4x4>(body, Matrix4x4.identity),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cone(12), spoutAt),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Ring(0.6f, 0.3f, 24), handleAt),
            });
        }

        public static Mesh ClockTower()
        {
            Matrix4x4 towerAt = Matrix4x4.TRS(new Vector3(0f, 0.24f, 0f), Quaternion.identity, Vector3.one);
            Matrix4x4 ledgeAt = Matrix4x4.TRS(new Vector3(0f, 0.47f, 0f), Quaternion.identity, new Vector3(0.24f, 0.03f, 0.24f));
            Matrix4x4 roofAt = Matrix4x4.TRS(new Vector3(0f, 0.50f, 0f), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.17f, 0.12f, 0.17f));
            return MeshFactory.Combine("pawn:clocktower", new[]
            {
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(new Vector3(0.18f, 0.48f, 0.18f), 0.02f, 3), towerAt),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.RoundedBox(Vector3.one, 0.1f, 2), ledgeAt),
                new KeyValuePair<Mesh, Matrix4x4>(MeshFactory.Cone(4), roofAt),
            });
        }
    }
}
