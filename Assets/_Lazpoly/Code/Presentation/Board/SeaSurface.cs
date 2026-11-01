using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The sea in the middle of the board: a flat grid whose vertices rise and fall with two slow
    /// crossing waves. No shader work, so it looks the same on every quality level; the grid is small
    /// enough that moving it every frame costs next to nothing.
    /// </summary>
    public sealed class SeaSurface : MonoBehaviour
    {
        /// <summary>Highest a wave rises above (or sinks below) the still water.</summary>
        public const float Amplitude = 0.03f;

        /// <summary>Quads along each side of the sea.</summary>
        public const int Grid = 26;

        private Mesh _mesh;
        private Vector3[] _rest;
        private Vector3[] _moved;
        private Color _dayColor;
        private Color _duskColor;
        private Renderer _renderer;

        /// <summary>A square sea of the given width at the parent's origin, its surface at local y = 0.</summary>
        public static SeaSurface Create(Transform parent, float size, Color color, Color duskColor)
        {
            Mesh mesh = BuildMesh(size, Grid);
            GameObject sea = ModelParts.Create("Sea", parent, mesh, color, 0.75f);
            var surface = sea.AddComponent<SeaSurface>();
            surface._mesh = mesh;
            surface._rest = mesh.vertices;
            surface._moved = new Vector3[surface._rest.Length];
            surface._dayColor = color;
            surface._duskColor = duskColor;
            surface._renderer = sea.GetComponent<Renderer>();
            return surface;
        }

        /// <summary>Water darkens towards dusk; 0 is day, 1 is evening.</summary>
        public void SetEvening(float evening)
        {
            if (_renderer != null) ModelParts.Paint(gameObject, Color.Lerp(_dayColor, _duskColor, Mathf.Clamp01(evening)), 0.75f);
        }

        /// <summary>Height of the water above rest at a point, at <paramref name="time"/> seconds.</summary>
        public static float Height(float x, float z, float time) =>
            Amplitude * (0.6f * Mathf.Sin(x * 1.7f + time * 1.1f) + 0.4f * Mathf.Sin(z * 2.3f + x * 0.5f - time * 0.8f));

        /// <summary>A flat square grid centred on the origin, facing up: <paramref name="grid"/> quads along each side.</summary>
        public static Mesh BuildMesh(float size, int grid)
        {
            int side = grid + 1;
            var vertices = new Vector3[side * side];
            var normals = new Vector3[side * side];
            for (int row = 0; row < side; row++)
            {
                for (int column = 0; column < side; column++)
                {
                    int i = row * side + column;
                    vertices[i] = new Vector3((column / (float)grid - 0.5f) * size, 0f, (row / (float)grid - 0.5f) * size);
                    normals[i] = Vector3.up;
                }
            }

            var triangles = new int[grid * grid * 6];
            int t = 0;
            for (int row = 0; row < grid; row++)
            {
                for (int column = 0; column < grid; column++)
                {
                    int a = row * side + column;
                    // Clockwise seen from above, which is the side Unity draws.
                    triangles[t++] = a;
                    triangles[t++] = a + side;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + side;
                    triangles[t++] = a + side + 1;
                }
            }

            var mesh = new Mesh { name = "sea" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Update()
        {
            float time = Time.time;
            for (int i = 0; i < _rest.Length; i++)
            {
                Vector3 v = _rest[i];
                v.y += Height(v.x, v.z, time);
                _moved[i] = v;
            }

            _mesh.vertices = _moved;
            _mesh.RecalculateNormals();
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
